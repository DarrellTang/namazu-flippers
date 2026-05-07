namespace NamazuFlippers.API.Models;

/// <summary>
/// Wire-shape DTO for the Saddlebag /api/scan response envelope.
/// The endpoint returns {"data": [...]} — kept separate from ScanResponse
/// so we can translate field names and types without leaking wire format
/// into the rest of the plugin.
/// </summary>
internal sealed class RawScanResponse
{
    public List<RawScanItem> Data { get; set; } = [];
}

/// <summary>
/// Wire-shape DTO for a single row inside the Saddlebag /api/scan response.
/// Matches the live API: item_id is a string, sale_rates is a string,
/// home_server_price uses 999_999_999 as an out-of-stock sentinel,
/// profit_amount is per-unit and already accounts for the 5% market tax.
/// </summary>
internal sealed class RawScanItem
{
    public string ItemId { get; set; } = "";
    public string RealName { get; set; } = "";
    public string Server { get; set; } = "";
    public int Ppu { get; set; }
    public int HomeServerPrice { get; set; }
    public int AvgPpu { get; set; }
    public long ProfitAmount { get; set; }
    public string SaleRates { get; set; } = "";
}
