namespace NamazuFlippers.Core;

public sealed class RouteStop
{
    public string PurchaseSource { get; set; } = "";

    public string? DataCenter { get; set; }

    public bool IsVendorStop { get; set; }

    public int TravelFriction { get; set; }

    public int TotalExpectedDailyProfit { get; set; }

    public List<RankedOpportunity> Items { get; set; } = [];
}
