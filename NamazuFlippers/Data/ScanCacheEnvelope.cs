using NamazuFlippers.API.Models;
using NamazuFlippers.Core;

namespace NamazuFlippers.Data;

public sealed class ScanCacheEnvelope
{
    // Schema version + staleness rule live in CacheSchema (dependency-free, so the rule is
    // unit-testable); this alias keeps existing references working. v2 → v3: opportunities now
    // carry capital-efficiency rank, Universalis depth/price-confidence, absorption cap, and a
    // recommended Kelly quantity. v2 envelopes lack these, so IsValid treats them as stale and
    // forces one fresh scan (criterion 11) — no crash, no silent misread of the old shape.
    public const int CurrentSchemaVersion = CacheSchema.CurrentVersion;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string ConfigFingerprint { get; set; } = "";

    public ScanResponse RawResponse { get; set; } = new();

    public ScanEngineResult DerivedResult { get; set; } = new();

    public SessionState SessionState { get; set; } = new();
}
