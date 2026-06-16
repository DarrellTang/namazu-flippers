namespace NamazuFlippers.Data;

public sealed class FlipLedgerEnvelope
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<FlipPosition> Positions { get; set; } = [];
}
