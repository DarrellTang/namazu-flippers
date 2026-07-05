using System.Net.Http;
using System.Text.Json;
using Dalamud.Plugin.Services;
using NamazuFlippers.API.Models;

namespace NamazuFlippers.API;

/// <summary>
/// HTTP client for the Universalis market-data API.
/// Makes a single batched GET against /api/v2/{world}/{itemIds} to fetch home-world
/// listing depth and recent-sale history for up to 100 items at once.
/// Never throws (except genuine cancellation) — any failure degrades to an empty/partial
/// result so the caller's scan can proceed with velocity-only data (ADR-0003).
/// </summary>
public sealed class UniversalisClient
{
    private const int MaxItemIds = 100;

    // Total attempts for a single enrichment request (1 try + 2 retries) on transient failures.
    private const int MaxAttempts = 3;

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://universalis.app"),
            Timeout = TimeSpan.FromSeconds(20),
        };
        var version = typeof(UniversalisClient).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"NamazuFlippers/{version} (+https://github.com/DarrellTang/namazu-flippers)");
        return client;
    }

    private readonly Configuration _config;
    private readonly IPluginLog _log;
    private readonly RateLimiter? _rateLimiter;

    public UniversalisClient(Configuration config, IPluginLog log, RateLimiter? rateLimiter = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Fetch home-world depth + recent-sale corroboration for up to ~100 item ids in ONE
    /// batched call. Degrades gracefully on any failure: logs a warning and returns whatever
    /// has been parsed so far (possibly empty), never throwing except on real cancellation.
    /// </summary>
    /// <param name="homeWorld">Player's home world for the listing/sale lookup.</param>
    /// <param name="itemIds">Item ids to enrich; distinct ids are capped at 100.</param>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>Map of item id to enrichment data. Empty when input is invalid or the call fails.</returns>
    public async Task<IReadOnlyDictionary<int, UniversalisItemData>> FetchAsync(
        string homeWorld,
        IReadOnlyList<int> itemIds,
        CancellationToken ct = default)
    {
        var result = new Dictionary<int, UniversalisItemData>();

        if (itemIds == null || itemIds.Count == 0 || string.IsNullOrWhiteSpace(homeWorld))
            return result;

        var ids = itemIds.Distinct().Take(MaxItemIds).ToList();
        if (ids.Count == 0)
            return result;

        try
        {
            if (_rateLimiter != null)
                await _rateLimiter.WaitAsync(ct).ConfigureAwait(false);

            var idsPath = string.Join(",", ids);
            var requestUri = $"/api/v2/{Uri.EscapeDataString(homeWorld)}/{idsPath}?listings=100&entries=50";

            // Universalis intermittently returns 504 (gateway timeout) on the batched endpoint; a
            // couple of bounded retries recover most of them so a transient blip doesn't cost the
            // whole scan its enrichment. A null body means all attempts were exhausted — degrade.
            var body = await GetWithRetryAsync(requestUri, ct).ConfigureAwait(false);
            if (body == null)
                return result;

            if (ids.Count == 1)
            {
                var item = JsonSerializer.Deserialize(body, UniversalisJsonContext.Default.UniversalisItem);
                if (item != null)
                    result[ids[0]] = ToItemData(item);
            }
            else
            {
                var multi = JsonSerializer.Deserialize(body, UniversalisJsonContext.Default.UniversalisMultiResponse);
                if (multi != null)
                {
                    foreach (var (key, item) in multi.Items)
                    {
                        if (item == null)
                            continue;
                        if (!int.TryParse(key, out var itemId))
                            continue;
                        result[itemId] = ToItemData(item);
                    }
                }
            }

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Warning("/nflip: Universalis enrichment failed: {Message}", ex.Message);
            return result;
        }
    }

    // Issues the GET with bounded retries via the Dalamud-free TransientHttpRetry policy (which is
    // unit-tested in isolation). Returns the body on success, or null when attempts are exhausted or
    // the server returns a non-retryable (4xx) status. Retries transient 5xx (e.g. 504) and
    // network/timeout errors with exponential backoff; rethrows only genuine cancellation, so every
    // other failure resolves to null and the caller degrades gracefully.
    private Task<string?> GetWithRetryAsync(string requestUri, CancellationToken ct) =>
        TransientHttpRetry.GetStringAsync(
            send: token => Http.GetAsync(requestUri, token),
            maxAttempts: MaxAttempts,
            backoff: attempt => TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)),
            onRetryStatus: (status, next) => _log.Information(
                "/nflip: Universalis returned {StatusCode}; retrying enrichment (attempt {Next}/{Max}).",
                status, next, MaxAttempts),
            onRetryError: (next, message) => _log.Information(
                "/nflip: Universalis request error; retrying enrichment (attempt {Next}/{Max}): {Message}",
                next, MaxAttempts, message),
            onStatusGiveUp: status => _log.Warning(
                "/nflip: Universalis enrichment returned {StatusCode}, skipping enrichment.", status),
            onErrorGiveUp: message => _log.Warning(
                "/nflip: Universalis enrichment failed after {Max} attempts: {Message}",
                MaxAttempts, message),
            ct: ct);

    private static UniversalisItemData ToItemData(UniversalisItem item)
    {
        var depth = Math.Max(item.ListingsCount, item.Listings?.Count ?? 0);
        var history = item.RecentHistory ?? [];
        var prices = history.Select(h => h.PricePerUnit).ToList();

        return new UniversalisItemData
        {
            Depth = depth,
            RecentSalesCount = history.Count,
            RecentMedianSalePrice = Median(prices),
        };
    }

    private static double Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
            return 0.0;

        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;

        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}
