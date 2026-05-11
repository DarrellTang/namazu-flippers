using NamazuFlippers.API.Models;
using NamazuFlippers.Core;

namespace NamazuFlippers.Data;

public sealed class ScanCacheEnvelope
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string ConfigFingerprint { get; set; } = "";

    public ScanResponse RawResponse { get; set; } = new();

    public ScanEngineResult DerivedResult { get; set; } = new();

    public SessionState SessionState { get; set; } = new();
}
