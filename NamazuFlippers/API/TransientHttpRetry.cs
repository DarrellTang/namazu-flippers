using System.Net.Http;

namespace NamazuFlippers.API;

/// <summary>
/// Bounded-retry wrapper for a single HTTP GET that returns its body as a string.
/// Deliberately Dalamud-free so the retry policy is unit-testable in isolation
/// (NamazuFlippers.Tests): it retries transient 5xx and network/timeout failures with
/// caller-supplied backoff, never retries 4xx (client-side, won't self-heal), returns
/// <c>null</c> when attempts are exhausted or a non-retryable status is seen, and rethrows
/// only genuine cancellation. See docs/dual-agent-review/VERIFICATION-POLICY.md.
/// </summary>
public static class TransientHttpRetry
{
    /// <param name="send">Issues one attempt. Called once per attempt.</param>
    /// <param name="maxAttempts">Total attempts (1 try + N-1 retries).</param>
    /// <param name="backoff">Delay before the retry at 1-based attempt index (1, 2, …).</param>
    /// <param name="onRetryStatus">Before retrying a transient 5xx: (status, nextAttempt).</param>
    /// <param name="onRetryError">Before retrying a network/timeout error: (nextAttempt, message).</param>
    /// <param name="onStatusGiveUp">Giving up on a status (4xx, or 5xx on the last attempt).</param>
    /// <param name="onErrorGiveUp">Giving up after a network/timeout error on the last attempt.</param>
    public static async Task<string?> GetStringAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        int maxAttempts,
        Func<int, TimeSpan> backoff,
        Action<int, int>? onRetryStatus = null,
        Action<int, string>? onRetryError = null,
        Action<int>? onStatusGiveUp = null,
        Action<string>? onErrorGiveUp = null,
        CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(backoff(attempt), ct).ConfigureAwait(false);

            var lastAttempt = attempt == maxAttempts - 1;

            try
            {
                using var response = await send(ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                var status = (int)response.StatusCode;

                // 4xx is a client-side problem that won't fix itself; only retry transient 5xx.
                if (status < 500 || lastAttempt)
                {
                    onStatusGiveUp?.Invoke(status);
                    return null;
                }

                onRetryStatus?.Invoke(status, attempt + 2);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Network error or per-request timeout (ct not cancelled).
                if (lastAttempt)
                {
                    onErrorGiveUp?.Invoke(ex.Message);
                    return null;
                }

                onRetryError?.Invoke(attempt + 2, ex.Message);
            }
        }

        return null;
    }
}
