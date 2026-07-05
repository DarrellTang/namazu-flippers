using Dalamud.Plugin.Services;
using NamazuFlippers.API;
using NamazuFlippers.API.Models;
using NamazuFlippers.Data;

namespace NamazuFlippers.Core;

/// <summary>
/// Business layer that turns raw Saddlebag scan rows into ranked opportunities.
/// </summary>
public sealed class ScanEngine
{
    private const int ApiRetryCount = 3;

    // Universalis enriches at most this many top survivors per scan (criterion 7).
    private const int MaxEnrichItems = 100;

    private readonly SaddlebagClient client;
    private readonly Configuration configuration;
    private readonly RouteOptimizer? routeOptimizer;
    private readonly ScanCacheStore? cacheStore;
    private readonly UniversalisClient? universalisClient;
    private readonly IPluginLog log;

    public ScanEngine(SaddlebagClient client, Configuration configuration, IPluginLog log)
        : this(client, configuration, log, routeOptimizer: null, cacheStore: null, universalisClient: null)
    {
    }

    public ScanEngine(
        SaddlebagClient client,
        Configuration configuration,
        IPluginLog log,
        RouteOptimizer? routeOptimizer,
        ScanCacheStore? cacheStore,
        UniversalisClient? universalisClient = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.routeOptimizer = routeOptimizer;
        this.cacheStore = cacheStore;
        this.universalisClient = universalisClient;
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<ScanEngineResult> GetRouteAsync(bool forceRefresh, CancellationToken ct = default)
    {
        if (!forceRefresh && cacheStore != null)
        {
            var validCache = await cacheStore.LoadValidAsync(ct).ConfigureAwait(false);
            if (validCache != null)
            {
                validCache.DerivedResult.Status = ScanEngineStatus.UsingCache;
                validCache.DerivedResult.IsFresh = true;
                validCache.DerivedResult.UserMessage = "Using cached route.";
                log.Information("/nflip: loaded valid scan cache.");
                return validCache.DerivedResult;
            }
        }

        var fresh = await ScanFreshCoreAsync(ct).ConfigureAwait(false);

        if (fresh.Result.Status == ScanEngineStatus.Success)
        {
            var routeStops = routeOptimizer?.Optimize(fresh.Result.Opportunities, configuration).ToList() ?? [];
            fresh.Result.RouteStops = routeStops;
            fresh.Result.TotalExpectedDailyProfit = routeStops.Sum(stop => stop.TotalExpectedDailyProfit);

            if (fresh.RawResponse != null)
                await TrySaveCacheAsync(fresh.RawResponse, fresh.Result, ct).ConfigureAwait(false);
        }
        else if (fresh.Result.Status == ScanEngineStatus.Empty)
        {
            if (fresh.RawResponse != null)
                await TrySaveCacheAsync(fresh.RawResponse, fresh.Result, ct).ConfigureAwait(false);
        }
        else if (cacheStore != null)
        {
            var staleCache = await cacheStore.LoadAnyAsync(ct).ConfigureAwait(false);
            // Only the refresh-failure fallback path reaches LoadAnyAsync, which (unlike the
            // valid-cache path) does not vet the schema version. A pre-v3 envelope lacks the
            // capital-efficiency / Kelly-quantity / absorption fields, so serving its DerivedResult
            // would silently misread the old shape (criterion 11). Treat any non-current schema as
            // no usable cache and surface the refresh error instead.
            if (staleCache != null && staleCache.SchemaVersion != ScanCacheEnvelope.CurrentSchemaVersion)
            {
                log.Warning(
                    "/nflip: ignoring stale scan cache with schema v{Version} (current v{Current}); not serving it.",
                    staleCache.SchemaVersion,
                    ScanCacheEnvelope.CurrentSchemaVersion);
                staleCache = null;
            }

            if (staleCache != null)
            {
                staleCache.DerivedResult.Status = ScanEngineStatus.UsingStaleCache;
                staleCache.DerivedResult.IsFresh = false;
                staleCache.DerivedResult.UserMessage =
                    "Refresh failed, so I am keeping the last saved route for now.";
                staleCache.DerivedResult.TechnicalDetails = fresh.Result.TechnicalDetails;
                staleCache.DerivedResult.Warnings = fresh.Result.Warnings.Count > 0
                    ? fresh.Result.Warnings
                    :
                    [
                        CreateScanWarning(
                            "RefreshFailedStaleCache",
                            staleCache.DerivedResult.UserMessage,
                            fresh.Result.TechnicalDetails)
                    ];
                log.Warning("/nflip: refresh failed; using stale scan cache.");
                return staleCache.DerivedResult;
            }
        }

        return fresh.Result;
    }

    private async Task TrySaveCacheAsync(ScanResponse rawResponse, ScanEngineResult result, CancellationToken ct)
    {
        if (cacheStore == null)
            return;

        try
        {
            await cacheStore.SaveAsync(rawResponse, result, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.Warning("/nflip: scan succeeded but cache save failed: {Message}", ex.Message);
        }
    }

    public async Task<ScanEngineResult> ScanFreshAsync(CancellationToken ct = default) =>
        (await ScanFreshCoreAsync(ct).ConfigureAwait(false)).Result;

    private async Task<(ScanResponse? RawResponse, ScanEngineResult Result)> ScanFreshCoreAsync(CancellationToken ct)
    {
        try
        {
            var response = await client.ScanAsync(ct).ConfigureAwait(false);
            var items = response.Items ?? [];

            if (items.Count == 0)
                return (response, EmptyResult("No opportunities matched your current settings."));

            // Admissibility floors are flat (criterion 2). Rank by capital efficiency, not absolute
            // profit (criterion 1 / ADR-0001). The final item-count cap is enforced later by
            // RouteOptimizer; keeping the full admissible pool lets Kelly sizing and the route see
            // affordable lower-ranked items rather than truncating here.
            var opportunities = items
                .Where(item => IsUsable(item, configuration))
                .Select(ToOpportunity)
                .OrderByDescending(opportunity => opportunity.CapitalEfficiency)
                .ThenByDescending(opportunity => opportunity.SalesPerDay)
                .ThenByDescending(opportunity => opportunity.ExpectedDailyProfit)
                .ThenBy(opportunity => opportunity.PurchasePrice)
                .ToList();

            if (opportunities.Count == 0)
                return (response, EmptyResult("No opportunities matched your current settings."));

            // Tier 2/3: enrich the top survivors with Universalis depth + recent sales, then score
            // sell/price confidence, absorption cap, and the final rank. Enrichment also corrects the
            // expected sell price to the outlier-robust recent-sale median. Degrades to velocity-only
            // (depth = 0, PriceConfidence = 1, price unverified) when Universalis is unavailable.
            var warnings = await EnrichAndScoreAsync(opportunities, ct).ConfigureAwait(false);

            // Re-apply the profit/ROI floors on the corrected prices so that opportunities whose
            // Saddlebag average was an outlier-inflated fluke (real median far lower) are dropped
            // rather than surfaced with a fake sell price.
            opportunities = opportunities
                .Where(opportunity => IsStillAdmissible(opportunity, configuration))
                .ToList();

            if (opportunities.Count == 0)
                return (response, EmptyResult("No opportunities matched your current settings."));

            // Final ranking key = CapitalEfficiency × SellConfidence × PriceConfidence (criterion 3),
            // tiebroken by raw velocity, then ExpectedDailyProfit, then ascending CheapestPrice
            // (criterion 1).
            opportunities = opportunities
                .OrderByDescending(opportunity => opportunity.FinalRank)
                .ThenByDescending(opportunity => opportunity.SalesPerDay)
                .ThenByDescending(opportunity => opportunity.ExpectedDailyProfit)
                .ThenBy(opportunity => opportunity.PurchasePrice)
                .ToList();

            // Absorption-capped half-Kelly sizing assigns each opportunity a recommended quantity
            // (criterion 6 / ADR-0002). The budget pool is MaxBudgetPerSession.
            KellySizer.AssignQuantities(opportunities, configuration.MaxBudgetPerSession, configuration.KellyFraction);

            var deployedGil = KellySizer.TotalDeployedGil(opportunities);
            log.Information(
                "/nflip: fresh scan ranked {Count} opportunities; Kelly deploys {Deployed:n0} of {Budget:n0} gil.",
                opportunities.Count,
                deployedGil,
                configuration.MaxBudgetPerSession);

            return (response, new ScanEngineResult
            {
                Status = ScanEngineStatus.Success,
                UserMessage = "Route ready.",
                Opportunities = opportunities,
                Warnings = warnings,
                IsFresh = true,
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            log.Information("/nflip: scan cancelled.");
            throw;
        }
        catch (ApiException ex)
        {
            log.Warning("/nflip: API scan failed: {Message}", ex.Message);
            return (null, new ScanEngineResult
            {
                Status = ScanEngineStatus.Error,
                UserMessage = "I could not refresh market data right now. Try again in a bit.",
                TechnicalDetails = ex.Message,
                Warnings =
                [
                    CreateScanWarning(
                        "ApiException",
                        "Market data refresh failed after bounded retries.",
                        ex.Message)
                ],
                IsFresh = true,
            });
        }
        catch (Exception ex)
        {
            log.Error("/nflip: unexpected scan failure: {Message}", ex.Message);
            return (null, new ScanEngineResult
            {
                Status = ScanEngineStatus.Error,
                UserMessage = "Something went wrong while scanning. Try again in a bit.",
                TechnicalDetails = ex.Message,
                Warnings =
                [
                    CreateScanWarning(
                        "UnexpectedException",
                        "Market data refresh failed unexpectedly.",
                        ex.Message)
                ],
                IsFresh = true,
            });
        }
    }

    private ScanEngineResult EmptyResult(string message)
    {
        log.Information("/nflip: scan returned no usable opportunities.");
        return new ScanEngineResult
        {
            Status = ScanEngineStatus.Empty,
            UserMessage = message,
            IsFresh = true,
        };
    }

    // Local hard filters for MinProfitAmount and PreferredRoi: Saddlebag's API treats both
    // as soft preferences for OOS items, and uses home_server_price for its calc while we
    // use the more conservative min(home_server_price, avg_ppu). Re-applying locally on
    // ProfitPerUnit / RoiPercent guarantees the user-visible numbers honor the configured floor.
    private static bool IsUsable(ScanItem item, Configuration config) =>
        item.ItemId > 0 &&
        !string.IsNullOrWhiteSpace(item.Name) &&
        !string.IsNullOrWhiteSpace(item.CheapestServer) &&
        item.HomePrice > 0 &&
        item.CheapestPrice > 0 &&
        item.ExpectedDailyProfit > 0 &&
        item.ProfitPerUnit >= config.MinProfitAmount &&
        item.RoiPercent >= config.PreferredRoi &&
        item.SalesPerDay >= Math.Max(config.MinSalesPerDay, double.Epsilon);

    // Re-check the profit and ROI floors after Universalis price correction. Velocity already
    // passed in IsUsable and is unchanged, so only the price-derived floors are re-evaluated.
    private static bool IsStillAdmissible(RankedOpportunity opportunity, Configuration config)
    {
        if (opportunity.HomePrice <= 0 || opportunity.PurchasePrice <= 0)
            return false;
        if (opportunity.ProfitPerUnit < config.MinProfitAmount || opportunity.ExpectedDailyProfit <= 0)
            return false;

        var roiPercent = (opportunity.ProfitPerUnit / (double)opportunity.PurchasePrice) * 100.0;
        return roiPercent >= config.PreferredRoi;
    }

    private static RankedOpportunity ToOpportunity(ScanItem item) => new()
    {
        ItemId = item.ItemId,
        Name = item.Name,
        HomePrice = item.HomePrice,
        PurchaseSource = item.CheapestServer,
        PurchasePrice = item.CheapestPrice,
        SalesPerDay = item.SalesPerDay,
        ExpectedDailyProfit = item.ExpectedDailyProfit,
        ProfitPerUnit = item.ProfitPerUnit,
        OutOfStock = item.OutOfStock,
        IsVendorSource = IsVendorSource(item.CheapestServer),
        CapitalEfficiency = OpportunityScoring.CapitalEfficiency(item.ProfitPerUnit, item.CheapestPrice, item.SalesPerDay),
    };

    /// <summary>
    /// Enriches the top <see cref="MaxEnrichItems"/> opportunities with Universalis home-world depth
    /// and recent sales, then computes sell confidence, price confidence, absorption cap, and final
    /// rank for every opportunity. Always completes: a disabled, empty, or failed Universalis call
    /// leaves depth = 0 / PriceConfidence = 1 (criterion 8). Returns any non-fatal warnings.
    /// </summary>
    private async Task<List<ScanWarning>> EnrichAndScoreAsync(
        IReadOnlyList<RankedOpportunity> opportunities,
        CancellationToken ct)
    {
        var warnings = new List<ScanWarning>();
        IReadOnlyDictionary<int, UniversalisItemData> enrichment = new Dictionary<int, UniversalisItemData>();

        if (configuration.EnableUniversalis && universalisClient != null)
        {
            var topIds = opportunities
                .Take(MaxEnrichItems)
                .Select(opportunity => opportunity.ItemId)
                .ToList();

            try
            {
                enrichment = await universalisClient
                    .FetchAsync(configuration.HomeWorld, topIds, ct)
                    .ConfigureAwait(false);

                if (enrichment.Count == 0 && topIds.Count > 0)
                {
                    log.Information("/nflip: Universalis returned no enrichment; using velocity-only scoring.");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Never fail a scan because Universalis failed (criterion 8 / ADR-0003).
                log.Warning("/nflip: Universalis enrichment failed; degrading to velocity-only: {Message}", ex.Message);
                warnings.Add(CreateScanWarning(
                    "UniversalisEnrichmentFailed",
                    "Competition and price data were unavailable, so the route uses velocity only.",
                    ex.Message));
                enrichment = new Dictionary<int, UniversalisItemData>();
            }
        }

        foreach (var opportunity in opportunities)
        {
            enrichment.TryGetValue(opportunity.ItemId, out var data);
            ApplyScoring(opportunity, data);
        }

        return warnings;
    }

    // Computes confidence multipliers, absorption cap, and final rank for one opportunity. When
    // data is null (not enriched / degraded) depth = 0 and recent-sales count = 0, which yields
    // SellConfidence = 1, PriceConfidence = 1, and an unverified price — today's velocity-only behavior.
    private void ApplyScoring(RankedOpportunity opportunity, UniversalisItemData? data)
    {
        var depth = data?.Depth ?? 0;
        var recentMedian = data?.RecentMedianSalePrice ?? 0.0;
        var recentCount = data?.RecentSalesCount ?? 0;

        // Price correction: replace Saddlebag's outlier-prone average with the recent-sale median
        // when there are enough recent home-world sales, and recompute the price-derived numbers.
        // A single 1M-gil misclick sale can't move the median, so fluke flips collapse to reality.
        var (sellPrice, verified) = OpportunityScoring.ResolveSellPrice(
            opportunity.HomePrice,
            recentMedian,
            recentCount,
            configuration.MinRecentSalesToJudge);
        opportunity.PriceVerified = verified;
        if (verified && sellPrice != opportunity.HomePrice)
        {
            opportunity.HomePrice = sellPrice;
            opportunity.ProfitPerUnit = OpportunityScoring.NetProfitPerUnit(sellPrice, opportunity.PurchasePrice);
            var dailyProfit = opportunity.ProfitPerUnit > 0 && opportunity.SalesPerDay > 0
                ? opportunity.ProfitPerUnit * opportunity.SalesPerDay
                : 0.0;
            opportunity.ExpectedDailyProfit = dailyProfit >= int.MaxValue ? int.MaxValue : (int)dailyProfit;
            opportunity.CapitalEfficiency = OpportunityScoring.CapitalEfficiency(
                opportunity.ProfitPerUnit, opportunity.PurchasePrice, opportunity.SalesPerDay);
        }

        var expectedDemand = OpportunityScoring.ExpectedDemand(opportunity.SalesPerDay, configuration.HoldingWindowDays);

        opportunity.Depth = depth;
        opportunity.SellConfidence = OpportunityScoring.SellConfidence(expectedDemand, depth);
        opportunity.AbsorptionCap = OpportunityScoring.AbsorptionCap(expectedDemand, depth);
        opportunity.PriceConfidence = OpportunityScoring.PriceConfidence(
            recentMedian,
            recentCount,
            opportunity.HomePrice,
            configuration.PriceCorroborationThreshold,
            configuration.MinRecentSalesToJudge);
        opportunity.FinalRank = OpportunityScoring.FinalRank(
            opportunity.CapitalEfficiency,
            opportunity.SellConfidence,
            opportunity.PriceConfidence);
    }

    private static bool IsVendorSource(string purchaseSource) =>
        purchaseSource.Equals("Vendor", StringComparison.OrdinalIgnoreCase) ||
        purchaseSource.StartsWith("Vendor:", StringComparison.OrdinalIgnoreCase);

    private static ScanWarning CreateScanWarning(
        string failureType,
        string userMessage,
        string? technicalDetails) =>
        new()
        {
            FailureType = failureType,
            TimestampUtc = DateTimeOffset.UtcNow,
            RetryCount = ApiRetryCount,
            UserMessage = userMessage,
            TechnicalDetails = technicalDetails,
        };
}
