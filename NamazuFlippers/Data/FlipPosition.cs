namespace NamazuFlippers.Data;

public sealed class FlipPosition
{
    public string Id { get; set; } = "";

    public int ItemId { get; set; }

    public string ItemName { get; set; } = "";

    public DateTimeOffset BuyTimestampUtc { get; set; }

    public string SourceWorld { get; set; } = "";

    public int ActualUnitBuyPrice { get; set; }

    public int ExpectedUnitSellPrice { get; set; }

    public int PlannedUnitProfit { get; set; }

    public int BoughtQuantity { get; set; }

    public int ListedQuantity { get; set; }

    public int SoldQuantity { get; set; }

    public int RemainingQuantity { get; set; }

    public FlipPositionStatus Status { get; set; } = FlipPositionStatus.Open;

    public DateTimeOffset RouteCreatedAtUtc { get; set; }

    public string RouteSessionId { get; set; } = "";

    public string HomeWorld { get; set; } = "";

    public bool OutOfStock { get; set; }

    public bool IsVendorSource { get; set; }

    public string Notes { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
