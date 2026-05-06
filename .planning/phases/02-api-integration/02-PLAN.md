# Phase 2: API Integration — Plan

**Phase:** 2 — API Integration
**Goal:** Plugin can call Saddlebag Exchange API and receive typed responses
**Requirements addressed:** API-01, API-02, API-03
**Plans:** 2 plans in 2 waves
**Generated:** 2026-05-05

---

## must_haves

> **Goal-backward verification.** Phase 2 is complete when ALL of these are true:

1. `SaddlebagClient.ScanAsync(CancellationToken)` exists, sends `POST /api/scan`, and returns a typed `ScanResponse` with `List<ScanItem>` items
2. `RateLimiter.WaitAsync(CancellationToken)` enforces a minimum delay between API calls
3. Network errors produce `IPluginLog.Error("/nflip: ...")` messages and set `LastApiError` on the plugin class
4. All three requirement IDs (API-01, API-02, API-03) are satisfied

---

## Wave 1

> Both Wave 1 plans are independent of each other — they operate on disjoint files.
> Can be executed in parallel.

### Plan 02-01: API Models, HTTP Client & ApiException

```
---
wave: 1
depends_on: []
files_modified: []
autonomous: true
requirements: [API-01, API-03]
---
```

#### Objective
Create typed C# request/response models for `POST /api/scan`, the `SaddlebagClient` HTTP wrapper, the `ApiException` type, and a System.Text.Json source-generation context. These types form the contract that Phase 3's ScanEngine consumes.

#### Tasks

**Task 1: Create `API/Models/ScanRequest.cs` — the request DTO**

<read_first>
- `SPEC.md` §Scan Endpoint Parameters — full parameter table with types, descriptions, and defaults
- `SPEC.md` §Scan Endpoint Parameters — the example JSON request body with exact parameter names in snake_case
- `NamazuFlippers/Configuration.cs` — config properties that map to ScanRequest fields (PreferredRoi, MinProfitAmount, MinDesiredAvgPpu, MinSalesPerWeek, RegionWide, CategoryFilters, IncludeVendors, ShowOutOfStock, HomeWorld)
- `NamazuFlippers/NamazuFlippers.csproj` — confirms net10.0-windows, ImplicitUsings, Nullable enabled; no existing NuGet dependencies beyond SDK
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 7 — mapping table: Configuration property → ScanRequest field → mapping (direct string, direct int, direct bool, direct int[], plus hardcoded defaults for hours_ago=168, min_stack_size=1, hq=false)
</read_first>

<action>
Create directory `NamazuFlippers/API/Models/`.

Create `NamazuFlippers/API/Models/ScanRequest.cs` in namespace `NamazuFlippers.API.Models` with the following concrete structure:

```csharp
namespace NamazuFlippers.API.Models;

/// <summary>
/// Serializable request body for POST /api/scan.
/// Maps 1:1 to the Saddlebag Exchange scan endpoint parameters.
/// Built from <see cref="Configuration"/> values by SaddlebagClient.
/// </summary>
public sealed class ScanRequest
{
    // --- Required: from Configuration.HomeWorld ---
    public string HomeServer { get; set; } = "";

    // --- From Configuration: direct mappings ---
    public int PreferredRoi { get; set; } = 25;
    public int MinProfitAmount { get; set; } = 10000;
    public int MinDesiredAvgPpu { get; set; } = 10000;
    public int MinSales { get; set; } = 2;
    public bool RegionWide { get; set; } = false;
    public bool IncludeVendor { get; set; } = true;
    public bool ShowOutStock { get; set; } = true;
    public int[] Filters { get; set; } = [];

    // --- Hardcoded defaults (not yet in Configuration) ---
    public int MinStackSize { get; set; } = 1;
    public int HoursAgo { get; set; } = 168;
    public bool Hq { get; set; } = false;
}
```

⛔ DO NOT add [JsonPropertyName] attributes. System.Text.Json uses `JsonNamingPolicy.SnakeCaseLower` which converts PascalCase `HomeServer` → `home_server`, `PreferredRoi` → `preferred_roi`, etc. The ApiJsonContext source generator handles this mapping.

⛔ DO NOT model unused API parameters (e.g., no `world_id`, no `data_center`, no page/total fields). Only fields that Phase 3 needs per D-04/D-05.

Add a static factory method:

```csharp
public static ScanRequest FromConfiguration(Configuration config) => new()
{
    HomeServer = config.HomeWorld,
    PreferredRoi = config.PreferredRoi,
    MinProfitAmount = config.MinProfitAmount,
    MinDesiredAvgPpu = config.MinDesiredAvgPpu,
    MinSales = config.MinSalesPerWeek,
    RegionWide = config.RegionWide,
    IncludeVendor = config.IncludeVendors,
    ShowOutStock = config.ShowOutOfStock,
    Filters = [..config.CategoryFilters], // defensive copy — CategoryFilters is mutable array
    MinStackSize = 1,
    HoursAgo = 168,
    Hq = false,
};
```
</action>

<acceptance_criteria>
- `grep -c "class ScanRequest" NamazuFlippers/API/Models/ScanRequest.cs` outputs `1`
- `grep -c "JsonPropertyName" NamazuFlippers/API/Models/ScanRequest.cs` outputs `0` (snake_case handled by naming policy, not attributes)
- `grep "FromConfiguration" NamazuFlippers/API/Models/ScanRequest.cs` returns the static factory method
- `grep "HomeServer" NamazuFlippers/API/Models/ScanRequest.cs` returns the string property
- `grep "HoursAgo" NamazuFlippers/API/Models/ScanRequest.cs` returns `public int HoursAgo { get; set; } = 168;`
- `grep "Hq" NamazuFlippers/API/Models/ScanRequest.cs` returns `public bool Hq { get; set; } = false;`
- `grep "Filters" NamazuFlippers/API/Models/ScanRequest.cs` returns `public int[] Filters { get; set; } = [];`
- Build succeeds: `dotnet build NamazuFlippers/NamazuFlippers.csproj` exits 0
</acceptance_criteria>

---

**Task 2: Create `API/Models/ScanItem.cs` — individual scan result**

<read_first>
- `SPEC.md` §Session Persistence — the JSON example showing item fields: item_id, name, home_price, cheapest_server, cheapest_price, sales_per_day, expected_daily_profit, out_of_stock
- `SPEC.md` §The Big Optimization — response shape description: "home server price, cheapest server + price, sales velocity, profit per item, ROI%, OOS flag"
- `.planning/phases/02-api-integration/02-CONTEXT.md` §D-05 — exact fields to model: item name, item ID, home price, cheapest server, cheapest price, sales velocity, expected daily profit, OOS flag
- `NamazuFlippers/NamazuFlippers.csproj` — confirms Nullable enabled; all reference type properties MUST be nullable-annotated
- `API/Models/ScanRequest.cs` — the just-created request model; use the same namespace and pattern
</read_first>

<action>
Create `NamazuFlippers/API/Models/ScanItem.cs` in namespace `NamazuFlippers.API.Models`:

```csharp
namespace NamazuFlippers.API.Models;

/// <summary>
/// One arbitrage opportunity from POST /api/scan.
/// Contains only the fields Phase 3 (ScanEngine) needs for ranking, grouping, and route building.
/// </summary>
public sealed class ScanItem
{
    /// <summary>FFXIV item ID (Universalis item ID).</summary>
    public int ItemId { get; set; }

    /// <summary>Item display name (e.g., "Expanse Barding").</summary>
    public string Name { get; set; } = "";

    /// <summary>Market board price on the player's home server.</summary>
    public int HomePrice { get; set; }

    /// <summary>Name of the server with the cheapest listing.</summary>
    public string CheapestServer { get; set; } = "";

    /// <summary>Price of the cheapest listing across all servers.</summary>
    public int CheapestPrice { get; set; }

    /// <summary>Average sales per day over the configured window.</summary>
    public double SalesPerDay { get; set; }

    /// <summary>Expected profit in gil: margin × sales_per_day.</summary>
    public int ExpectedDailyProfit { get; set; }

    /// <summary>True if the item has zero listings on the home server (priority item).</summary>
    public bool OutOfStock { get; set; }
}
```

⛔ DO NOT add fields for `bought` or `listed` — those are session state, not API response fields. Phase 5 adds them.
⛔ DO NOT add ROI field unless the API response includes a top-level `roi` or `preferred_roi` value — the ScanEngine can compute margin from home_price and cheapest_price.
</action>

<acceptance_criteria>
- `grep -c "class ScanItem" NamazuFlippers/API/Models/ScanItem.cs` outputs `1`
- `grep "ItemId" NamazuFlippers/API/Models/ScanItem.cs` returns `public int ItemId { get; set; }`
- `grep "Name" NamazuFlippers/API/Models/ScanItem.cs` returns `public string Name { get; set; } = "";`
- `grep "HomePrice" NamazuFlippers/API/Models/ScanItem.cs` returns `public int HomePrice { get; set; }`
- `grep "CheapestServer" NamazuFlippers/API/Models/ScanItem.cs` returns `public string CheapestServer { get; set; } = "";`
- `grep "CheapestPrice" NamazuFlippers/API/Models/ScanItem.cs` returns `public int CheapestPrice { get; set; }`
- `grep "SalesPerDay" NamazuFlippers/API/Models/ScanItem.cs` returns `public double SalesPerDay { get; set; }`
- `grep "ExpectedDailyProfit" NamazuFlippers/API/Models/ScanItem.cs` returns `public int ExpectedDailyProfit { get; set; }`
- `grep "OutOfStock" NamazuFlippers/API/Models/ScanItem.cs` returns `public bool OutOfStock { get; set; }`
- `grep -c "bought\|listed" NamazuFlippers/API/Models/ScanItem.cs` outputs `0` (session state, not API model)
- Build succeeds: `dotnet build NamazuFlippers/NamazuFlippers.csproj` exits 0
</acceptance_criteria>

---

**Task 3: Create `API/Models/ScanResponse.cs` — response wrapper**

<read_first>
- `SPEC.md` §Session Persistence — the `items` array in the JSON session example shows how scan results are structured
- `SPEC.md` §The Big Optimization — response is "ranked list of items"
- `API/Models/ScanItem.cs` — the ScanItem type this wraps
- `NamazuFlippers/NamazuFlippers.csproj` — Nullable enabled
</read_first>

<action>
Create `NamazuFlippers/API/Models/ScanResponse.cs` in namespace `NamazuFlippers.API.Models`:

```csharp
namespace NamazuFlippers.API.Models;

/// <summary>
/// Wrapper for the POST /api/scan JSON response.
/// Contains the ranked list of arbitrage items.
/// </summary>
public sealed class ScanResponse
{
    /// <summary>Ranked list of arbitrage opportunities (top N by expected_daily_profit).</summary>
    public List<ScanItem> Items { get; set; } = [];
}
```

The property name `Items` is a best-effort guess at the API response shape. The actual JSON key will be determined when the API is tested (the OpenAPI spec at https://docs.saddlebagexchange.com/openapi.json should be checked to confirm). If the real response uses a different key (e.g., `results`, `data`, `scan_results`), the property can be renamed or annotated with `[JsonPropertyName("...")]` at that time.

⛔ DO NOT add pagination fields (`page`, `total`, `total_pages`). Phase 3 only needs the items list.
</action>

<acceptance_criteria>
- `grep -c "class ScanResponse" NamazuFlippers/API/Models/ScanResponse.cs` outputs `1`
- `grep "List<ScanItem> Items" NamazuFlippers/API/Models/ScanResponse.cs` returns the property declaration
- Build succeeds: `dotnet build NamazuFlippers/NamazuFlippers.csproj` exits 0
</acceptance_criteria>

---

**Task 4: Create `API/Models/ApiJsonContext.cs` — System.Text.Json source generator**

<read_first>
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 2 — explains why source generation is important for Dalamud's trimmed runtime; shows the [JsonSourceGenerationOptions] and [JsonSerializable] pattern with `JsonKnownNamingPolicy.SnakeCaseLower`
- `NamazuFlippers/NamazuFlippers.csproj` — confirms net10.0-windows target; System.Text.Json source generators are a compile-time BCL feature requiring no NuGet packages
- `API/Models/ScanRequest.cs`, `API/Models/ScanResponse.cs`, `API/Models/ScanItem.cs` — the three types to register for serialization
</read_first>

<action>
Create `NamazuFlippers/API/Models/ApiJsonContext.cs` in namespace `NamazuFlippers.API.Models`:

```csharp
using System.Text.Json.Serialization;

namespace NamazuFlippers.API.Models;

/// <summary>
/// Source-generated JSON serializer context for Saddlebag API types.
/// Uses SnakeCaseLower naming policy to map PascalCase C# properties
/// to snake_case JSON fields (HomeServer → home_server).
/// Registered as partial class — the source generator creates the implementation.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScanRequest))]
[JsonSerializable(typeof(ScanResponse))]
[JsonSerializable(typeof(ScanItem))]
internal sealed partial class ApiJsonContext : JsonSerializerContext
{
}
```

⛔ DO NOT add `[JsonSerializable]` for types that don't exist yet. The source generator will fail the build.
</action>

<acceptance_criteria>
- `grep -c "partial class ApiJsonContext" NamazuFlippers/API/Models/ApiJsonContext.cs` outputs `1`
- `grep "JsonSourceGenerationOptions" NamazuFlippers/API/Models/ApiJsonContext.cs` returns a line with `PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower`
- `grep -c "\[JsonSerializable" NamazuFlippers/API/Models/ApiJsonContext.cs` outputs `3` (ScanRequest, ScanResponse, ScanItem)
- `grep "ScanRequest" NamazuFlippers/API/Models/ApiJsonContext.cs` returns `[JsonSerializable(typeof(ScanRequest))]`
- `grep "ScanResponse" NamazuFlippers/API/Models/ApiJsonContext.cs` returns `[JsonSerializable(typeof(ScanResponse))]`
- `grep "ScanItem" NamazuFlippers/API/Models/ApiJsonContext.cs` returns `[JsonSerializable(typeof(ScanItem))]`
- Build succeeds: `dotnet build NamazuFlippers/NamazuFlippers.csproj` exits 0
</acceptance_criteria>

---

**Task 5: Create `API/ApiException.cs` — custom exception for API failures**

<read_first>
- `.planning/phases/02-api-integration/02-CONTEXT.md` §D-09 — non-transient failures surface immediately; retryable vs non-retryable distinction
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 6 — ApiException has Message, StatusCode, IsRetryable
- `NamazuFlippers/NamazuFlippers.csproj` — Nullable enabled; no special dependencies needed for exceptions
</read_first>

<action>
Create `NamazuFlippers/API/ApiException.cs` in namespace `NamazuFlippers.API`:

```csharp
namespace NamazuFlippers.API;

/// <summary>
/// Exception thrown when the Saddlebag Exchange API returns an error
/// or when the HTTP request fails after all retries are exhausted.
/// </summary>
public sealed class ApiException : Exception
{
    /// <summary>HTTP status code, if the server returned one. Null for network errors.</summary>
    public int? StatusCode { get; }

    /// <summary>True if the error is transient and a retry might succeed (5xx, network timeout).</summary>
    public bool IsRetryable { get; }

    public ApiException(string message, int? statusCode = null, bool isRetryable = false)
        : base(message)
    {
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }

    public ApiException(string message, Exception innerException, int? statusCode = null, bool isRetryable = false)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }
}
```

This exception is thrown by `SaddlebagClient.ScanAsync()` for:
- HTTP 4xx responses (non-retryable, StatusCode set)
- HTTP 5xx responses after all retries exhausted (retryable was true, but retries failed)
- Network timeouts / DNS failures after all retries exhausted (retryable, no StatusCode)
</action>

<acceptance_criteria>
- `grep -c "class ApiException" NamazuFlippers/API/ApiException.cs` outputs `1`
- `grep "StatusCode" NamazuFlippers/API/ApiException.cs` returns `public int? StatusCode { get; }`
- `grep "IsRetryable" NamazuFlippers/API/ApiException.cs` returns `public bool IsRetryable { get; }`
- `grep ": Exception" NamazuFlippers/API/ApiException.cs` returns `public sealed class ApiException : Exception`
- Build succeeds: `dotnet build NamazuFlippers/NamazuFlippers.csproj` exits 0
</acceptance_criteria>

---

**Task 6: Create `API/SaddlebagClient.cs` — the HTTP client**

<read_first>
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 1 — static readonly HttpClient singleton pattern (no IHttpClientFactory in Dalamud), BaseAddress = "https://api.saddlebagexchange.com", Timeout = 30s
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 3 — manual retry loop with exponential backoff (MaxRetries=3, backoff 1s→2s→4s); 5xx → retry, 4xx → surface immediately
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 7 — mapping table: Configuration → ScanRequest fields
- `.planning/phases/02-api-integration/02-CONTEXT.md` §D-07/D-08 — auto-retry 2-3 times with exponential backoff; non-transient failures surface immediately
- `SPEC.md` §Scan Endpoint Parameters — the exact `POST /api/scan` JSON body shape: home_server, preferred_roi, min_profit_amount, min_desired_avg_ppu, min_stack_size, hours_ago, min_sales, hq, filters, region_wide, include_vendor, show_out_stock
- `API/Models/ScanRequest.cs`, `API/Models/ScanResponse.cs`, `API/Models/ScanItem.cs`, `API/Models/ApiJsonContext.cs`, `API/ApiException.cs` — all the types this client uses
- `NamazuFlippers/Configuration.cs` — ScanRequest.FromConfiguration() reads from this; SaddlebagClient constructor takes Configuration
- `NamazuFlippers/NamazuFlippers.cs` — IPluginLog usage pattern; client receives IPluginLog via constructor
</read_first>

<action>
Create `NamazuFlippers/API/SaddlebagClient.cs` in namespace `NamazuFlippers.API`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
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

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.saddlebagexchange.com"),
        Timeout = TimeSpan.FromSeconds(30),
    };

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
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ScanEndpoint)
        {
            Content = JsonContent.Create(request, typeof(ScanRequest),
                sourceGenContext: ApiJsonContext.Default.Options)
            // ⚠️ First attempt: use source-gen context. If build fails due to API mismatch, fall back to:
            // Content = JsonContent.Create(request, options: new JsonSerializerOptions
            //     { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
        };

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var response = await Http.SendAsync(httpRequest, ct);

                if (response.IsSuccessStatusCode)
                {
                    var scanResponse = await response.Content
                        .ReadFromJsonAsync<ScanResponse>(ApiJsonContext.Default.Options, ct);
                    
                    if (scanResponse == null)
                        throw new ApiException("API returned null response.", 
                            (int)response.StatusCode, isRetryable: false);

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
                    _log.Warning("/nflip: API server error {(int)response.StatusCode}, retrying... (attempt {attempt + 1}/{MaxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    // Clone the request for retry (HttpRequestMessage can only be sent once)
                    httpRequest.Dispose();
                    httpRequest = await CloneHttpRequestAsync(httpRequest);
                    continue;
                }

                throw new ApiException(
                    $"API unavailable after {MaxRetries + 1} attempts (last status: {(int)response.StatusCode}).",
                    (int)response.StatusCode, isRetryable: true);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _log.Warning("/nflip: Network error, retrying... (attempt {attempt + 1}/{MaxRetries}): {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
            catch (TaskCanceledException ex) when (attempt < MaxRetries && !ct.IsCancellationRequested)
            {
                // Timeout (not user cancellation)
                _log.Warning("/nflip: Request timed out, retrying... (attempt {attempt + 1}/{MaxRetries})");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        throw new ApiException(
            $"API call failed after {MaxRetries + 1} attempts.",
            statusCode: null, isRetryable: true);
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
}
```
</action>

<acceptance_criteria>
- `grep -c "class SaddlebagClient" NamazuFlippers/API/SaddlebagClient.cs` outputs `1`
- `grep "ScanAsync" NamazuFlippers/API/SaddlebagClient.cs` returns the method declaration `public async Task<ScanResponse> ScanAsync(CancellationToken ct = default)`
- `grep "MaxRetries = 3" NamazuFlippers/API/SaddlebagClient.cs` returns the constant
- `grep "api.saddlebagexchange.com" NamazuFlippers/API/SaddlebagClient.cs` returns the BaseAddress URI
- `grep "TimeSpan.FromSeconds(30)" NamazuFlippers/API/SaddlebagClient.cs` returns the timeout
- `grep "ScanRequest.FromConfiguration" NamazuFlippers/API/SaddlebagClient.cs` returns the factory call
- `grep "Math.Pow(2, attempt)" NamazuFlippers/API/SaddlebagClient.cs` returns the exponential backoff
- `grep "ApiException" NamazuFlippers/API/SaddlebagClient.cs` returns at least 3 occurrences (null response, 4xx, exhausted retries)
- `grep "/nflip:" NamazuFlippers/API/SaddlebagClient.cs` returns at least 3 log messages with the `/nflip:` prefix
- Build succeeds: `dotnet build NamazuFlippers/NamazuFlippers.csproj` exits 0
</acceptance_criteria>

---

### Plan 02-02: Rate Limiter & Plugin Integration

```
---
wave: 2
depends_on: ["02-01"]
files_modified: ["NamazuFlippers/NamazuFlippers.cs"]
autonomous: false
requirements: [API-02, API-01]
---
```

#### Objective
Create the timestamp-based rate limiter, wire `SaddlebagClient` into the plugin entry point, and expose error state (`LastApiError`) for Phase 4's in-window error banner.

#### Why This Must Wait Until Wave 2
Plan 02-02 modifies `NamazuFlippers.cs` to instantiate `SaddlebagClient` and `RateLimiter`. It also adds `LastApiError` to the plugin class. These require `SaddlebagClient` and the model types to exist (created in 02-01).

#### Tasks

**Task 1: Create `API/RateLimiter.cs` — timestamp-based delay enforcement**

<read_first>
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 4 — recommended RateLimiter design: timestamp-based, lock-protected, `WaitAsync(CancellationToken)`, minimum delay of 1000ms
- `.planning/phases/02-api-integration/02-CONTEXT.md` §D-10/D-11 — simple minimum delay, politeness safeguard, all endpoints Universalis-safe
</read_first>

<action>
Create `NamazuFlippers/API/RateLimiter.cs` in namespace `NamazuFlippers.API`:

```csharp
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
```

⛔ DO NOT use SemaphoreSlim or token bucket — over-engineering per D-10 and RESEARCH §Finding 4.
</action>

<acceptance_criteria>
- `grep -c "class RateLimiter" NamazuFlippers/API/RateLimiter.cs` outputs `1`
- `grep "TimeSpan.FromMilliseconds(1000)" NamazuFlippers/API/RateLimiter.cs` returns the default minDelay
- `grep "WaitAsync" NamazuFlippers/API/RateLimiter.cs` returns `public async Task WaitAsync(CancellationToken ct = default)`
- `grep "lock (_lock)" NamazuFlippers/API/RateLimiter.cs` returns the lock usage (thread-safe)
- `grep "Task.Delay" NamazuFlippers/API/RateLimiter.cs` returns the async delay call
- Build succeeds: `dotnet build NamazuFlippers/NamazuFlippers.csproj` exits 0
</acceptance_criteria>

---

**Task 2: Wire SaddlebagClient into NamazuFlippers.cs — instantiation, error state, and LastApiError**

<read_first>
- `NamazuFlippers/NamazuFlippers.cs` — current entry point: constructor initializes Configuration, FirstRunWindow; has isVisible field; OnDraw calls firstRunWindow.Draw(); Dispose cleans up
- `API/SaddlebagClient.cs` — the type to instantiate; constructor signature: `SaddlebagClient(Configuration, IPluginLog, RateLimiter?)`
- `API/RateLimiter.cs` — the RateLimiter type; constructor: `RateLimiter(TimeSpan?)`
- `.planning/phases/02-api-integration/02-CONTEXT.md` §D-01/D-02/D-03 — error surfacing: both chat (IPluginLog) and in-window banner (LastApiError string property); chat prefix: "/nflip: "
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 5 — fire-and-forget Task.Run pattern for async API calls from UI thread; catch ApiException, log via IPluginLog, set LastApiError for OnDraw
- `.planning/phases/02-api-integration/02-RESEARCH.md` §Finding 8 — instantiation pattern: _rateLimiter = new RateLimiter(TimeSpan.FromMilliseconds(1000)); _apiClient = new SaddlebagClient(Configuration, log, _rateLimiter)
</read_first>

<action>
Modify `NamazuFlippers/NamazuFlippers.cs`. Add three things to the existing class:

**1. Add fields after existing fields:**
```csharp
private readonly RateLimiter rateLimiter;
private readonly SaddlebagClient apiClient;

/// <summary>
/// Set when an API call fails. Rendered as an in-window error banner by Phase 4's OnDraw.
/// Cleared on successful scan or user dismiss.
/// </summary>
public string? LastApiError { get; private set; }
```

**2. In the constructor, after `Configuration = ...` and before `firstRunWindow = ...`, add:**
```csharp
rateLimiter = new RateLimiter(TimeSpan.FromMilliseconds(1000));
apiClient = new SaddlebagClient(Configuration, log, rateLimiter);
```

Add `using NamazuFlippers.API;` to the top of the file (after the existing `using` statements).

**3. In the constructor, AFTER the `firstRunWindow = ...` line, add a placeholder fire-and-forget scan trigger (the actual trigger comes in Phase 3; this is a hook to verify the client is correctly instantiated and functional):**
```csharp
// Phase 2: SaddlebagClient is instantiated and ready.
// Phase 3 ScanEngine will call apiClient.ScanAsync().
// For now, a placeholder demonstrates the fire-and-forget error surfacing pattern:
// _ = Task.Run(async () => {
//     try { var result = await apiClient.ScanAsync(CancellationToken.None); LastApiError = null; }
//     catch (ApiException ex) { log.Error($"/nflip: {ex.Message}"); LastApiError = ex.Message; }
// });
```

⛔ Do NOT add an actual call to `apiClient.ScanAsync()` — keep the fire-and-forget block commented out. Only instantiate the client and rate limiter. Phase 3 owns the scan trigger logic.
⛔ Do NOT modify the `Dispose()` method's existing cleanup.
⛔ Do NOT change the `OnCommand` or `OnDraw` methods.
</action>

<acceptance_criteria>
- `grep "rateLimiter = new RateLimiter" NamazuFlippers/NamazuFlippers.cs` returns the instantiation line
- `grep "apiClient = new SaddlebagClient" NamazuFlippers/NamazuFlippers.cs` returns the instantiation with `(Configuration, log, rateLimiter)`
- `grep "LastApiError" NamazuFlippers/NamazuFlippers.cs` returns `public string? LastApiError { get; private set; }`
- `grep "using NamazuFlippers.API" NamazuFlippers/NamazuFlippers.cs` returns the using statement
- `grep "ScanAsync" NamazuFlippers/NamazuFlippers.cs` returns only in the commented-out block (no active scan call)
- `grep -c "private readonly RateLimiter rateLimiter" NamazuFlippers/NamazuFlippers.cs` outputs `1`
- `grep -c "private readonly SaddlebagClient apiClient" NamazuFlippers/NamazuFlippers.cs` outputs `1`
- Build succeeds: `dotnet build NamazuFlippers/NamazuFlippers.csproj` exits 0
</acceptance_criteria>

---

## Verification

### Goal-Backward Verification

1. **SC-01: `POST /api/scan` returns parsed, typed response objects**
   - `SaddlebagClient.ScanAsync()` sends POST to `https://api.saddlebagexchange.com/api/scan`
   - Response is deserialized to `ScanResponse` containing `List<ScanItem>`
   - `ScanItem` has all fields Phase 3 needs: ItemId, Name, HomePrice, CheapestServer, CheapestPrice, SalesPerDay, ExpectedDailyProfit, OutOfStock
   - Verify: `grep "ScanAsync" NamazuFlippers/API/SaddlebagClient.cs` returns the method; `grep "ReadFromJsonAsync<ScanResponse>" NamazuFlippers/API/SaddlebagClient.cs` returns the deserialization call

2. **SC-02: Rate limiter prevents excessive calls**
   - `RateLimiter.WaitAsync()` enforced with minimum 1000ms delay
   - `SaddlebagClient.ScanAsync()` calls `_rateLimiter?.WaitAsync(ct)` before sending the HTTP request
   - Verify: `grep "WaitAsync" NamazuFlippers/API/RateLimiter.cs` and `grep "_rateLimiter?.WaitAsync" NamazuFlippers/API/SaddlebagClient.cs`

3. **SC-03: Network errors handled gracefully with user feedback**
   - `SaddlebagClient` retries 3 times with exponential backoff (1s, 2s, 4s) on 5xx and network errors
   - `ApiException` thrown on 4xx immediately; on 5xx/network after retries exhausted
   - `NamazuFlippers.LastApiError` set for in-window error banner
   - All log messages use `/nflip:` prefix
   - Verify: `grep "MaxRetries = 3"` and `grep "Math.Pow"` in SaddlebagClient.cs; `grep "LastApiError"` in NamazuFlippers.cs

### Requirements Coverage

| REQ-ID | Covered By | How |
|--------|-----------|-----|
| API-01 | Plan 02-01, Task 6 | `SaddlebagClient.ScanAsync()` calls `POST /api/scan`, deserializes to `ScanResponse` |
| API-02 | Plan 02-02, Task 1 | `RateLimiter.WaitAsync()` enforces 1000ms minimum delay between calls |
| API-03 | Plan 02-01, Tasks 1–4 | `ScanRequest`, `ScanResponse`, `ScanItem`, `ApiJsonContext` — all typed C# models |

### Build Verification

```bash
dotnet build NamazuFlippers/NamazuFlippers.csproj
# Must exit 0 — all types resolve, no compilation errors.
```

---

## Files Created/Modified

### New Files (all in `NamazuFlippers/API/`)

| File | Plan | Purpose |
|------|------|---------|
| `API/Models/ScanRequest.cs` | 02-01 Task 1 | Request DTO mapping to `/api/scan` body |
| `API/Models/ScanItem.cs` | 02-01 Task 2 | Single arbitrage opportunity result |
| `API/Models/ScanResponse.cs` | 02-01 Task 3 | Response wrapper with items list |
| `API/Models/ApiJsonContext.cs` | 02-01 Task 4 | System.Text.Json source-gen context |
| `API/ApiException.cs` | 02-01 Task 5 | Custom exception for API failures |
| `API/SaddlebagClient.cs` | 02-01 Task 6 | HTTP client with retry and deserialization |
| `API/RateLimiter.cs` | 02-02 Task 1 | Timestamp-based delay enforcement |

### Modified Files

| File | Plan | Change |
|------|------|--------|
| `NamazuFlippers/NamazuFlippers.cs` | 02-02 Task 2 | Add `using NamazuFlippers.API;`, instantiate `RateLimiter` and `SaddlebagClient`, add `LastApiError` property, add commented-out scan trigger placeholder |

---

## Risks

1. **Saddlebag API response shape unknown.** The `ScanResponse.Items` property name is a best guess. If the actual JSON uses a different key (e.g., `results`, `data`), deserialization will silently produce an empty list. **Mitigation:** Task 3 notes this risk; executor should test with a real API call or check the OpenAPI spec.

2. **JsonContent.Create with source-gen context may fail.** `JsonContent.Create(request, typeof(ScanRequest), sourceGenContext: ...)` is API that changed between .NET versions. **Mitigation:** Task 6 includes a fallback comment using `new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }` if the source-gen approach doesn't compile.

3. **HttpRequestMessage reuse in retry loop.** `HttpRequestMessage` cannot be sent twice. **Mitigation:** Task 6 includes `CloneHttpRequestAsync()` called before each retry. The initial `using` is disposed after first send; cloned requests use the same method.

4. **Dalamud async context.** `SaddlebagClient.ScanAsync()` is async. Phase 3 must call it via `Task.Run()` to avoid blocking the game's main thread. Phase 2 only instantiates the client — it does not call `ScanAsync()` from UI code.

---

*Phase: 02-api-integration*
*Plan generated: 2026-05-05*
