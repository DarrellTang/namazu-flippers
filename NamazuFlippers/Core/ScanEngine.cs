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
    private readonly SaddlebagClient client;
    private readonly Configuration configuration;
    private readonly RouteOptimizer? routeOptimizer;
    private readonly ScanCacheStore? cacheStore;
    private readonly IPluginLog log;

    public ScanEngine(SaddlebagClient client, Configuration configuration, IPluginLog log)
        : this(client, configuration, log, routeOptimizer: null, cacheStore: null)
    {
    }

    public ScanEngine(
        SaddlebagClient client,
        Configuration configuration,
        IPluginLog log,
        RouteOptimizer? routeOptimizer,
        ScanCacheStore? cacheStore)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.routeOptimizer = routeOptimizer;
        this.cacheStore = cacheStore;
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<ScanEngineResult> GetRouteAsync(bool forceRefresh, CancellationToken ct = default)
    {
        if (!forceRefresh && cacheStore != null)
        {
            var validCache = await cacheStore.LoadValidAsync(ct);
            if (validCache != null)
            {
                validCache.DerivedResult.Status = ScanEngineStatus.UsingCache;
                validCache.DerivedResult.IsFresh = true;
                validCache.DerivedResult.UserMessage = "Using cached route.";
                log.Information("/nflip: loaded valid scan cache.");
                return validCache.DerivedResult;
            }
        }

        var fresh = await ScanFreshCoreAsync(ct);

        if (fresh.Result.Status == ScanEngineStatus.Success)
        {
            var routeStops = routeOptimizer?.Optimize(fresh.Result.Opportunities, configuration).ToList() ?? [];
            fresh.Result.RouteStops = routeStops;
            fresh.Result.TotalExpectedDailyProfit = routeStops.Sum(stop => stop.TotalExpectedDailyProfit);

            if (fresh.RawResponse != null)
                await TrySaveCacheAsync(fresh.RawResponse, fresh.Result, ct);
        }
        else if (fresh.Result.Status == ScanEngineStatus.Empty)
        {
            if (fresh.RawResponse != null)
                await TrySaveCacheAsync(fresh.RawResponse, fresh.Result, ct);
        }
        else if (cacheStore != null)
        {
            var staleCache = await cacheStore.LoadAnyAsync(ct);
            if (staleCache != null)
            {
                staleCache.DerivedResult.Status = ScanEngineStatus.UsingStaleCache;
                staleCache.DerivedResult.IsFresh = false;
                staleCache.DerivedResult.UserMessage =
                    "Refresh failed, so I am keeping the last saved route for now.";
                staleCache.DerivedResult.TechnicalDetails = fresh.Result.TechnicalDetails;
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
            await cacheStore.SaveAsync(rawResponse, result, ct);
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
        (await ScanFreshCoreAsync(ct)).Result;

    private async Task<(ScanResponse? RawResponse, ScanEngineResult Result)> ScanFreshCoreAsync(CancellationToken ct)
    {
        try
        {
            var response = await client.ScanAsync(ct);
            var items = response.Items ?? [];

            if (items.Count == 0)
                return (response, EmptyResult("No opportunities matched your current settings."));

            // Final item-count cap is enforced by RouteOptimizer.TrimItemsPreservingStopOrder
            // after the cumulative-budget filter. Truncating here would block the budget
            // filter from skipping past too-expensive top-rank items to find affordable
            // ones lower in the list.
            var opportunities = items
                .Where(item => IsUsable(item, configuration))
                .OrderByDescending(item => item.ExpectedDailyProfit)
                .ThenByDescending(item => item.SalesPerDay)
                .ThenBy(item => item.CheapestPrice)
                .Select(ToOpportunity)
                .ToList();

            if (opportunities.Count == 0)
                return (response, EmptyResult("No opportunities matched your current settings."));

            var totalExpectedProfit = opportunities.Sum(item => item.ExpectedDailyProfit);
            log.Information(
                "/nflip: fresh scan ranked {Count} opportunities, expected daily profit {Profit:n0} gil.",
                opportunities.Count,
                totalExpectedProfit);

            return (response, new ScanEngineResult
            {
                Status = ScanEngineStatus.Success,
                UserMessage = "Route ready.",
                Opportunities = opportunities,
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

    private static RankedOpportunity ToOpportunity(ScanItem item) => new()
    {
        ItemId = item.ItemId,
        Name = item.Name,
        HomePrice = item.HomePrice,
        PurchaseSource = item.CheapestServer,
        PurchasePrice = item.CheapestPrice,
        SalesPerDay = item.SalesPerDay,
        ExpectedDailyProfit = item.ExpectedDailyProfit,
        OutOfStock = item.OutOfStock,
        IsVendorSource = IsVendorSource(item.CheapestServer),
    };

    private static bool IsVendorSource(string purchaseSource) =>
        purchaseSource.Equals("Vendor", StringComparison.OrdinalIgnoreCase) ||
        purchaseSource.StartsWith("Vendor:", StringComparison.OrdinalIgnoreCase);
}
