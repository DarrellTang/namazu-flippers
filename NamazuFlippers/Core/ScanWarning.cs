namespace NamazuFlippers.Core;

public sealed class ScanWarning
{
    public string FailureType { get; set; } = "";

    public string? AffectedItemName { get; set; }

    public string? AffectedWorld { get; set; }

    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    public int RetryCount { get; set; }

    public string UserMessage { get; set; } = "";

    public string? TechnicalDetails { get; set; }
}
