namespace NamazuFlippers.API.Models;

/// <summary>
/// One arbitrage opportunity from POST /api/scan.
/// Contains only the fields Phase 3 (ScanEngine) needs for ranking, grouping, and route building.
/// </summary>
public sealed class ScanItem
{
    /// <summary>FFXIV item ID (Universalis item ID).</summary>
    public int ItemId { get; set; }

    /// <summary>Item display name (e.g., "Expanse Barding").</summary>
    public string Name { get; set; } = "";

    /// <summary>Market board price on the player's home server.</summary>
    public int HomePrice { get; set; }

    /// <summary>Name of the server with the cheapest listing.</summary>
    public string CheapestServer { get; set; } = "";

    /// <summary>Price of the cheapest listing across all servers.</summary>
    public int CheapestPrice { get; set; }

    /// <summary>Average sales per day over the configured window.</summary>
    public double SalesPerDay { get; set; }

    /// <summary>Expected profit in gil: margin × sales_per_day.</summary>
    public int ExpectedDailyProfit { get; set; }

    /// <summary>Profit per unit in gil after FFXIV's 5% market tax. Used by ScanEngine.IsUsable
    /// to enforce Configuration.MinProfitAmount as a true local minimum (the API treats it
    /// softly for OOS items and computes it from home_server_price; we use our conservative
    /// expectedSellPrice so the displayed profit always meets the user's threshold).</summary>
    public int ProfitPerUnit { get; set; }

    /// <summary>ROI as a percentage (e.g., 25.0 = 25%). Used by ScanEngine.IsUsable
    /// to enforce Configuration.PreferredRoi as a true local minimum.</summary>
    public double RoiPercent { get; set; }

    /// <summary>True if the item has zero listings on the home server (priority item).</summary>
    public bool OutOfStock { get; set; }
}
