namespace NamazuFlippers.Core;

public enum ScanEngineStatus
{
    Success,
    Empty,
    Error,
    UsingCache,
    UsingStaleCache,
}

/// <summary>
/// Structured scan outcome consumed by route, cache, and UI layers.
/// </summary>
public sealed class ScanEngineResult
{
    public ScanEngineStatus Status { get; set; }

    public string UserMessage { get; set; } = "";

    public string? TechnicalDetails { get; set; }

    public List<RankedOpportunity> Opportunities { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsFresh { get; set; }
}
