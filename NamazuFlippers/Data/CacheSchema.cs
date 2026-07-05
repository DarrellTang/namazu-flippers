namespace NamazuFlippers.Data;

/// <summary>
/// Single source of truth for the scan-cache schema version and the "is this envelope's
/// schema current?" rule. Kept dependency-free so the staleness rule is unit-testable in
/// isolation: a v2 envelope must be rejected as stale and re-scanned (criterion 11), never
/// misread as if it were current. See docs/dual-agent-review/VERIFICATION-POLICY.md.
/// </summary>
public static class CacheSchema
{
    // v2 → v3: opportunities gained capital-efficiency rank, Universalis depth/price confidence,
    // absorption cap, and a recommended Kelly quantity. Envelopes written under an older schema
    // lack these fields, so they are treated as stale and re-scanned rather than misread.
    public const int CurrentVersion = 3;

    public static bool IsCurrent(int schemaVersion) => schemaVersion == CurrentVersion;
}
