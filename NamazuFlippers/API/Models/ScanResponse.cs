namespace NamazuFlippers.API.Models;

/// <summary>
/// Wrapper for the POST /api/scan JSON response.
/// Contains the ranked list of arbitrage items.
/// </summary>
public sealed class ScanResponse
{
    /// <summary>Ranked list of arbitrage opportunities (top N by expected_daily_profit).</summary>
    public List<ScanItem> Items { get; set; } = [];
}
