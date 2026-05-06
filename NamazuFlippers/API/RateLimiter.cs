namespace NamazuFlippers.API;

/// <summary>
/// Enforces a minimum delay between API calls.
/// Thread-safe via lock. Used as a politeness safeguard — all Phase 2
/// Saddlebag Exchange endpoints are Universalis-safe per SPEC.md.
/// </summary>
public sealed class RateLimiter
{
    private readonly TimeSpan _minDelay;
    private DateTime _lastCall = DateTime.MinValue;
    private readonly object _lock = new();

    /// <param name="minDelay">Minimum time between consecutive API calls. Default 1 second.</param>
    public RateLimiter(TimeSpan? minDelay = null)
    {
        _minDelay = minDelay ?? TimeSpan.FromMilliseconds(1000);
    }

    /// <summary>
    /// Waits until the minimum delay since the last call has elapsed.
    /// If no delay is needed, returns immediately.
    /// </summary>
    /// <param name="ct">Token to cancel the delay.</param>
    public async Task WaitAsync(CancellationToken ct = default)
    {
        TimeSpan wait;
        lock (_lock)
        {
            var elapsed = DateTime.UtcNow - _lastCall;
            wait = _minDelay - elapsed;
            if (wait <= TimeSpan.Zero)
            {
                _lastCall = DateTime.UtcNow;
                return;
            }
            _lastCall = DateTime.UtcNow.Add(wait);
        }
        await Task.Delay(wait, ct);
    }
}
