using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Dalamud.Plugin.Services;
using NamazuFlippers.API.Models;

namespace NamazuFlippers.API;

/// <summary>
/// HTTP client for the Saddlebag Exchange API.
/// Makes a single POST /api/scan call with retry and returns typed ScanResponse.
/// </summary>
public sealed class SaddlebagClient
{
    private const int MaxRetries = 3;
    private const string ScanEndpoint = "/api/scan";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://api.saddlebagexchange.com"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        var version = typeof(SaddlebagClient).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"NamazuFlippers/{version} (+https://github.com/DarrellTang/namazu-flippers)");
        return client;
    }

    private readonly Configuration _config;
    private readonly IPluginLog _log;
    private readonly RateLimiter? _rateLimiter; // null until Plan 02-02 wires it in

    public SaddlebagClient(Configuration config, IPluginLog log, RateLimiter? rateLimiter = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Calls POST /api/scan with parameters from the current Configuration.
    /// Retries on transient failures with exponential backoff.
    /// Returns the typed, ranked list of arbitrage items.
    /// </summary>
    /// <param name="ct">Cancellation token for the async operation.</param>
    /// <returns>ScanResponse containing the ranked list of ScanItem results.</returns>
    /// <exception cref="ApiException">Thrown when the API fails after all retries.</exception>
    public async Task<ScanResponse> ScanAsync(CancellationToken ct = default)
    {
        if (_rateLimiter != null)
            await _rateLimiter.WaitAsync(ct);

        var request = ScanRequest.FromConfiguration(_config);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, ScanEndpoint)
        {
            Content = JsonContent.Create(request, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            })
        };

        try
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var response = await Http.SendAsync(httpRequest, ct);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    var scanResponse = NormalizeScanResponse(body, (int)response.StatusCode);

                    _log.Information("/nflip: Scan completed — {Count} items found.",
                        scanResponse.Items?.Count ?? 0);
                    return scanResponse;
                }

                // 4xx → non-transient, surface immediately
                if ((int)response.StatusCode < 500)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    throw new ApiException(
                        $"API error {(int)response.StatusCode}: {body.Truncate(200)}",
                        (int)response.StatusCode, isRetryable: false);
                }

                // 5xx → transient, retry if attempts remain
                if (attempt < MaxRetries)
                {
                    _log.Warning("/nflip: API server error {StatusCode}, retrying... (attempt {Attempt}/{MaxRetries})",
                        (int)response.StatusCode, attempt + 1, MaxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    var newRequest = await CloneHttpRequestAsync(httpRequest);
                    httpRequest.Dispose();
                    httpRequest = newRequest;
                    continue;
                }

                throw new ApiException(
                    $"API unavailable after {MaxRetries + 1} attempts (last status: {(int)response.StatusCode}).",
                    (int)response.StatusCode, isRetryable: true);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _log.Warning("/nflip: Network error, retrying... (attempt {Attempt}/{MaxRetries}): {Message}",
                    attempt + 1, MaxRetries, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                var newRequest = await CloneHttpRequestAsync(httpRequest);
                httpRequest.Dispose();
                httpRequest = newRequest;
            }
            catch (TaskCanceledException) when (attempt < MaxRetries && !ct.IsCancellationRequested)
            {
                // Timeout (not user cancellation)
                _log.Warning("/nflip: Request timed out, retrying... (attempt {Attempt}/{MaxRetries})",
                    attempt + 1, MaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                var newRequest = await CloneHttpRequestAsync(httpRequest);
                httpRequest.Dispose();
                httpRequest = newRequest;
            }
        }

        throw new ApiException(
            $"API call failed after {MaxRetries + 1} attempts.",
            statusCode: null, isRetryable: true);
        }
        finally
        {
            httpRequest.Dispose();
        }
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        if (original.Content != null)
        {
            var contentBytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);
            if (original.Content.Headers.ContentType != null)
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
        }
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }

    // home_server_price uses 999_999_999 as a sentinel for out-of-stock items.
    private const int OutOfStockSentinel = 900_000_000;

    private static ScanResponse NormalizeScanResponse(string body, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ApiException("API returned an empty response.", statusCode, isRetryable: false);

        RawScanResponse? raw;
        try
        {
            raw = JsonSerializer.Deserialize(body, ApiJsonContext.Default.RawScanResponse);
        }
        catch (JsonException ex)
        {
            throw new ApiException("API returned invalid JSON.", ex, statusCode, isRetryable: false);
        }

        if (raw == null)
            throw new ApiException("API response was null.", statusCode, isRetryable: false);

        var items = (raw.Data ?? []).Select(MapItem).ToList();
        return new ScanResponse { Items = items };
    }

    // FFXIV market board takes 5% in retainer fees on every sale.
    private const double MarketTaxRate = 0.95;

    private static ScanItem MapItem(RawScanItem raw)
    {
        int.TryParse(raw.ItemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId);

        // sale_rates is sales/day on the home server averaged over hours_ago.
        // Cross-checked against regionWeekly* counts on both high- and low-velocity items.
        double.TryParse(raw.SaleRates, NumberStyles.Float, CultureInfo.InvariantCulture, out var salesPerDay);

        var isOutOfStock = raw.HomeServerPrice >= OutOfStockSentinel;

        // Expected sell price: lower of current home listing and historical average.
        // home_server_price is just the cheapest current listing — when it's far above
        // avg_ppu, someone listed unrealistically high and you'd be undercut before
        // selling. avg_ppu is what the item actually clears at.
        // For OOS items, only avg_ppu is available.
        var expectedSellPrice = isOutOfStock
            ? raw.AvgPpu
            : Math.Min(raw.HomeServerPrice, raw.AvgPpu);

        var sellNet = (long)Math.Floor(expectedSellPrice * MarketTaxRate);
        var profitPerUnit = sellNet - raw.Ppu;

        var expectedDailyProfit = 0;
        if (profitPerUnit > 0 && salesPerDay > 0)
        {
            var product = profitPerUnit * salesPerDay;
            expectedDailyProfit = product >= int.MaxValue ? int.MaxValue : (int)product;
        }

        return new ScanItem
        {
            ItemId = itemId,
            Name = raw.RealName,
            HomePrice = expectedSellPrice,
            CheapestServer = raw.Server,
            CheapestPrice = raw.Ppu,
            SalesPerDay = salesPerDay,
            ExpectedDailyProfit = expectedDailyProfit,
            OutOfStock = isOutOfStock,
        };
    }
}

/// <summary>
/// Extension method to truncate strings for log messages.
/// </summary>
internal static class StringExtensions
{
    public static string Truncate(this string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
