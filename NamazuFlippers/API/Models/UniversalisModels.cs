namespace NamazuFlippers.API.Models;

/// <summary>
/// Wire DTO for Universalis's multi-item response: <c>"items"</c> object keyed by
/// stringified item id, returned only when MULTIPLE ids are requested in one call.
/// </summary>
internal sealed class UniversalisMultiResponse
{
    public Dictionary<string, UniversalisItem> Items { get; set; } = new();
}

/// <summary>
/// Wire DTO for a single Universalis item entry (also the bare response shape when
/// exactly one item id is requested).
/// </summary>
internal sealed class UniversalisItem
{
    /// <summary>Total competing listings count, maps to JSON <c>"listingsCount"</c>.</summary>
    public int ListingsCount { get; set; }

    public List<UniversalisListing> Listings { get; set; } = [];

    /// <summary>Recent sale history, maps to JSON <c>"recentHistory"</c>.</summary>
    public List<UniversalisHistory> RecentHistory { get; set; } = [];
}

/// <summary>Wire DTO for a single current market board listing.</summary>
internal sealed class UniversalisListing
{
    public int PricePerUnit { get; set; }

    public int Quantity { get; set; }
}

/// <summary>Wire DTO for a single recent sale entry.</summary>
internal sealed class UniversalisHistory
{
    public int PricePerUnit { get; set; }

    public int Quantity { get; set; }

    public long Timestamp { get; set; }
}

/// <summary>
/// Output type consumed by the scan engine: home-world listing depth plus recent-sale
/// price corroboration for a single item, derived from <see cref="UniversalisItem"/>.
/// </summary>
public sealed class UniversalisItemData
{
    /// <summary>Competing-listing count on the home world.</summary>
    public int Depth { get; set; }

    /// <summary>Median price-per-unit across recent sales; 0.0 when no sale history.</summary>
    public double RecentMedianSalePrice { get; set; }

    /// <summary>Number of recent sales used to compute <see cref="RecentMedianSalePrice"/>.</summary>
    public int RecentSalesCount { get; set; }
}
