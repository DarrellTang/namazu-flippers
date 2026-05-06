namespace NamazuFlippers.API.Models;

/// <summary>
/// Serializable request body for POST /api/scan.
/// Maps 1:1 to the Saddlebag Exchange scan endpoint parameters.
/// Built from <see cref="Configuration"/> values by SaddlebagClient.
/// </summary>
public sealed class ScanRequest
{
    // --- Required: from Configuration.HomeWorld ---
    public string HomeServer { get; set; } = "";

    // --- From Configuration: direct mappings ---
    public int PreferredRoi { get; set; } = 25;
    public int MinProfitAmount { get; set; } = 10000;
    public int MinDesiredAvgPpu { get; set; } = 10000;
    public int MinSales { get; set; } = 2;
    public bool RegionWide { get; set; } = false;
    public bool IncludeVendor { get; set; } = true;
    public bool ShowOutStock { get; set; } = true;
    public int[] Filters { get; set; } = [];

    // --- Hardcoded defaults (not yet in Configuration) ---
    public int MinStackSize { get; set; } = 1;
    public int HoursAgo { get; set; } = 168;
    public bool Hq { get; set; } = false;

    public static ScanRequest FromConfiguration(Configuration config) => new()
    {
        HomeServer = config.HomeWorld,
        PreferredRoi = config.PreferredRoi,
        MinProfitAmount = config.MinProfitAmount,
        MinDesiredAvgPpu = config.MinDesiredAvgPpu,
        MinSales = config.MinSalesPerWeek,
        RegionWide = config.RegionWide,
        IncludeVendor = config.IncludeVendors,
        ShowOutStock = config.ShowOutOfStock,
        Filters = [..config.CategoryFilters], // defensive copy — CategoryFilters is mutable array
        MinStackSize = 1,
        HoursAgo = 168,
        Hq = false,
    };
}
