using System.Net;
using System.Net.Http;
using NamazuFlippers.API;
using Xunit;

namespace NamazuFlippers.Tests;

public class TransientHttpRetryTests
{
    private static readonly Func<int, TimeSpan> NoBackoff = _ => TimeSpan.Zero;

    private static HttpResponseMessage Resp(HttpStatusCode code, string body = "")
        => new(code) { Content = new StringContent(body) };

    // Turns a sequence of per-attempt outcomes into a send delegate, counting attempts.
    private static Func<CancellationToken, Task<HttpResponseMessage>> Sender(
        Func<HttpResponseMessage>[] steps, Action? onSend = null)
    {
        var queue = new Queue<Func<HttpResponseMessage>>(steps);
        return _ =>
        {
            onSend?.Invoke();
            return Task.FromResult(queue.Dequeue()());
        };
    }

    [Fact]
    public async Task Returns_body_on_first_success()
    {
        var attempts = 0;
        var body = await TransientHttpRetry.GetStringAsync(
            Sender([() => Resp(HttpStatusCode.OK, "ok")], () => attempts++),
            maxAttempts: 3, backoff: NoBackoff);

        Assert.Equal("ok", body);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Retries_transient_5xx_then_succeeds()
    {
        var attempts = 0;
        var body = await TransientHttpRetry.GetStringAsync(
            Sender(
            [
                () => Resp(HttpStatusCode.InternalServerError),
                () => Resp(HttpStatusCode.GatewayTimeout),
                () => Resp(HttpStatusCode.OK, "recovered"),
            ], () => attempts++),
            maxAttempts: 3, backoff: NoBackoff);

        Assert.Equal("recovered", body);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Does_not_retry_4xx()
    {
        var attempts = 0;
        var body = await TransientHttpRetry.GetStringAsync(
            Sender(
            [
                () => Resp(HttpStatusCode.BadRequest),
                () => Resp(HttpStatusCode.OK, "should-not-reach"),
            ], () => attempts++),
            maxAttempts: 3, backoff: NoBackoff);

        Assert.Null(body);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Returns_null_when_5xx_persists_to_exhaustion()
    {
        var attempts = 0;
        var body = await TransientHttpRetry.GetStringAsync(
            Sender(
            [
                () => Resp(HttpStatusCode.InternalServerError),
                () => Resp(HttpStatusCode.InternalServerError),
                () => Resp(HttpStatusCode.InternalServerError),
            ], () => attempts++),
            maxAttempts: 3, backoff: NoBackoff);

        Assert.Null(body);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Retries_network_error_then_succeeds()
    {
        var attempts = 0;
        var body = await TransientHttpRetry.GetStringAsync(
            Sender(
            [
                () => throw new HttpRequestException("boom"),
                () => Resp(HttpStatusCode.OK, "after-network-error"),
            ], () => attempts++),
            maxAttempts: 3, backoff: NoBackoff);

        Assert.Equal("after-network-error", body);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Propagates_genuine_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TransientHttpRetry.GetStringAsync(
                _ => throw new OperationCanceledException(cts.Token),
                maxAttempts: 3, backoff: NoBackoff, ct: cts.Token));
    }

    [Fact]
    public async Task Applies_backoff_before_each_retry()
    {
        var backoffCalls = new List<int>();
        await TransientHttpRetry.GetStringAsync(
            Sender(
            [
                () => Resp(HttpStatusCode.InternalServerError),
                () => Resp(HttpStatusCode.InternalServerError),
                () => Resp(HttpStatusCode.OK, "ok"),
            ]),
            maxAttempts: 3,
            backoff: attempt => { backoffCalls.Add(attempt); return TimeSpan.Zero; });

        // Backoff is applied before the 2nd and 3rd attempts, never before the 1st.
        Assert.Equal([1, 2], backoffCalls);
    }
}
