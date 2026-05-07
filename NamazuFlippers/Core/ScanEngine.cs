using Dalamud.Plugin.Services;
using NamazuFlippers.API;
using NamazuFlippers.API.Models;

namespace NamazuFlippers.Core;

/// <summary>
/// Business layer that turns raw Saddlebag scan rows into ranked opportunities.
/// </summary>
public sealed class ScanEngine
{
    private readonly SaddlebagClient client;
    private readonly Configuration configuration;
    private readonly IPluginLog log;

    public ScanEngine(SaddlebagClient client, Configuration configuration, IPluginLog log)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<ScanEngineResult> ScanFreshAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await client.ScanAsync(ct);
            var items = response.Items ?? [];

            if (items.Count == 0)
                return EmptyResult("No opportunities matched your current settings.");

            var opportunities = items
                .Where(IsUsable)
                .OrderByDescending(item => item.ExpectedDailyProfit)
                .ThenByDescending(item => item.SalesPerDay)
                .ThenBy(item => item.CheapestPrice)
                .Take(Math.Max(1, configuration.MaxItemsPerSession))
                .Select(ToOpportunity)
                .ToList();

            if (opportunities.Count == 0)
                return EmptyResult("No opportunities matched your current settings.");

            var totalExpectedProfit = opportunities.Sum(item => item.ExpectedDailyProfit);
            log.Information(
                "/nflip: fresh scan ranked {Count} opportunities, expected daily profit {Profit:n0} gil.",
                opportunities.Count,
                totalExpectedProfit);

            return new ScanEngineResult
            {
                Status = ScanEngineStatus.Success,
                UserMessage = "Route ready.",
                Opportunities = opportunities,
                IsFresh = true,
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            log.Information("/nflip: scan cancelled.");
            throw;
        }
        catch (ApiException ex)
        {
            log.Warning("/nflip: API scan failed: {Message}", ex.Message);
            return new ScanEngineResult
            {
                Status = ScanEngineStatus.Error,
                UserMessage = "I could not refresh market data right now. Try again in a bit.",
                TechnicalDetails = ex.Message,
                IsFresh = true,
            };
        }
        catch (Exception ex)
        {
            log.Error("/nflip: unexpected scan failure: {Message}", ex.Message);
            return new ScanEngineResult
            {
                Status = ScanEngineStatus.Error,
                UserMessage = "Something went wrong while scanning. Try again in a bit.",
                TechnicalDetails = ex.Message,
                IsFresh = true,
            };
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

    private static bool IsUsable(ScanItem item) =>
        item.ItemId > 0 &&
        !string.IsNullOrWhiteSpace(item.Name) &&
        !string.IsNullOrWhiteSpace(item.CheapestServer) &&
        item.HomePrice > 0 &&
        item.CheapestPrice > 0 &&
        item.ExpectedDailyProfit > 0 &&
        item.SalesPerDay > 0;

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
