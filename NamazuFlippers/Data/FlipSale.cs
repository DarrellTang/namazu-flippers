namespace NamazuFlippers.Data;

public sealed class FlipSale
{
    public string Id { get; set; } = "";

    public DateTimeOffset SoldAtUtc { get; set; }

    public int Quantity { get; set; }

    public int ActualUnitSalePrice { get; set; }

    public int NetUnitSalePrice { get; set; }

    public int UnitBuyPrice { get; set; }

    public int RealizedUnitProfit { get; set; }

    public int TotalRealizedProfit { get; set; }

    public string Notes { get; set; } = "";
}
