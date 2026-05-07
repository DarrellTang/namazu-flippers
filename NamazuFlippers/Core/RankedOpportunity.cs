namespace NamazuFlippers.Core;

/// <summary>
/// Ranked arbitrage opportunity selected from a Saddlebag scan row.
/// </summary>
public sealed class RankedOpportunity
{
    public int ItemId { get; set; }

    public string Name { get; set; } = "";

    public int HomePrice { get; set; }

    public string PurchaseSource { get; set; } = "";

    public int PurchasePrice { get; set; }

    public double SalesPerDay { get; set; }

    public int ExpectedDailyProfit { get; set; }

    public bool OutOfStock { get; set; }

    public bool IsVendorSource { get; set; }
}
