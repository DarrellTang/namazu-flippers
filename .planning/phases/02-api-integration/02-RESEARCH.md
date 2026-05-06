# Research: Phase 2 — API Integration for Dalamud Plugin (C#, .NET 10)

## Summary

Phase 2 requires building a typed HTTP client for `POST /api/scan` against the Saddlebag Exchange API, with a simple rate limiter and resilient error handling. The recommended approach is: a manually-managed singleton `HttpClient`, System.Text.Json with source generators for serialization, Polly (v8+) or a manual retry loop for exponential backoff, and a timestamp-based rate limiter. The `API/` folder should house models, the client, the rate limiter, and a custom exception type — all consumable by Phase 3's ScanEngine via a clean public interface.

## Findings

### 1. HttpClient lifecycle: singleton vs IHttpClientFactory

Dalamud plugins run inside the game process as class libraries. They do **not** have access to the standard ASP.NET `IServiceCollection` / `IHttpClientFactory` infrastructure. IHttpClientFactory requires `Microsoft.Extensions.Http`, which is a separate NuGet package that may or may not resolve correctly inside the Dalamud sandbox (Dalamud ships its own trimmed runtime).

**Recommendation:** Use a manually-managed `static readonly HttpClient` as a singleton. This is the correct pattern for long-lived applications that are not using the Microsoft DI container — exactly the Dalamud plugin case. Socket exhaustion is avoided by reusing the single instance; DNS changes are not a concern for a plugin that makes 1–2 API calls per session.

```csharp
// In SaddlebagClient.cs
private static readonly HttpClient _http = new()
{
    BaseAddress = new Uri("https://api.saddlebagexchange.com"),
    Timeout = TimeSpan.FromSeconds(30)
};
```

[Source: Microsoft docs — HttpClient guidelines for .NET; long-lived applications should use a single static/shared HttpClient](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)

### 2. Serialization: System.Text.Json with source generators (over Newtonsoft.Json)

.NET 10's System.Text.Json is mature and performant. For Dalamud plugins specifically, **source generation** is important: Dalamud's plugin loader may apply trimming, and reflection-based serializers can break under trimmed assemblies. Source-generated System.Text.Json serializers avoid this entirely.

Additionally, the API response uses `snake_case` field names (`home_server`, `min_profit_amount`, etc.). System.Text.Json's `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` handles this natively. No need for `[JsonPropertyName]` on every field (though explicit attributes are safer for trim-compatibility).

Models should live in `API/Models/` and use `[JsonSerializable]` with a generated context:

```csharp
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ScanRequest))]
[JsonSerializable(typeof(ScanResponse))]
internal partial class ApiJsonContext : JsonSerializerContext { }
```

**Why not Newtonsoft.Json:** While Dalamud itself bundles Newtonsoft.Json for `IPluginConfiguration` serialization, System.Text.Json is now the BCL default, has better performance, and supports source generation for trim safety. Adding Newtonsoft.Json as an explicit dependency for the API layer is unnecessary when System.Text.Json is already in the BCL.

[Source: Microsoft docs — System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)

### 3. Retry + resilience: Polly (recommended) or manual backoff loop

**Polly v8+** (`Polly.Core` NuGet package) is the standard .NET resilience library. It provides:
- `RetryStrategy` with exponential backoff
- Configurable `DelayBackoffType.Exponential`
- `OnRetry` callbacks for logging
- Composability with timeout and circuit breaker strategies

However, adding a NuGet dependency to a Dalamud plugin has risks: the package must be compatible with Dalamud's trimmed runtime and the plugin's `net10.0-windows` TFM. Polly is generally safe (it's pure managed code, no native interop), but testing is needed.

**If Polly is available**, the pattern is:

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder()
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
            .HandleResult<HttpResponseMessage>(r => 
                r.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
    })
    .Build();
```

**If Polly cannot be loaded**, a manual retry loop is straightforward and avoids any dependency risk:

```csharp
private static async Task<HttpResponseMessage> SendWithRetryAsync(
    HttpRequestMessage request, CancellationToken ct)
{
    for (int attempt = 0; attempt <= MaxRetries; attempt++)
    {
        try
        {
            var response = await _http.SendAsync(request, ct);
            if ((int)response.StatusCode < 500)
                return response;
            // 5xx → retry
        }
        catch (HttpRequestException) when (attempt < MaxRetries) { }
        catch (TaskCanceledException) when (attempt < MaxRetries) { }

        if (attempt < MaxRetries)
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
    }
    throw new ApiException("API unavailable after retries.");
}
```

**Recommended values** (agent discretion per D-07/D-08):
- **Max retries:** 3 (so 1 initial + 3 retries = 4 total attempts)
- **Backoff multiplier:** 2× (1s → 2s → 4s)
- **Total retry window:** ~7 seconds
- **Per-request timeout:** 30 seconds (set on HttpClient)

### 4. Rate limiter: simple timestamp-based delay

Per D-10, the rate limiter should be a "simple minimum delay between API calls — no token bucket or complex throttling." Since all Phase 2 endpoints are Universalis-safe (per CONTEXT), this is purely a politeness mechanism.

**Recommended design:**

```csharp
public class RateLimiter
{
    private readonly TimeSpan _minDelay;
    private DateTime _lastCall = DateTime.MinValue;
    private readonly object _lock = new();

    public RateLimiter(TimeSpan minDelay) => _minDelay = minDelay;

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

**Recommended minimum delay:** 1000ms (1 second). This is generous (Phase 2 at most makes 1 API call per session), and the Phase 3 ScanEngine may trigger a rescan with cached results. If the shortage predictor (Phase 6) is added later, the same rate limiter instance covers both endpoints.

**Why not SemaphoreSlim / token bucket:** Over-engineering for a plugin that makes at most 1–2 calls per session. A simple async delay between calls is correct and auditable.

### 5. Dalamud async and threading model

Dalamud plugins have specific threading constraints:
- **UI callbacks** (`OnDraw`, command handlers) run on the game's main thread. They must not block.
- **Async HTTP calls** must not be `.Result` or `.Wait()` on the main thread (game freeze). Use `Task.Run` or fire-and-forget with proper error surfacing.
- **`IPluginLog`** is the logging abstraction (not `ILogger<T>`). All API errors should log via `IPluginLog`.
- **Chat messages** can be sent via `IChatGui.Print()` or `IPluginLog` (which may route to chat depending on Dalamud config). Per D-01, error messages go to **both** the chat log and the plugin window.

**Pattern for fire-and-forget API calls from UI:**

```csharp
// In the plugin entry point or a coordinating service
_ = Task.Run(async () =>
{
    try
    {
        var result = await _client.ScanAsync(request, cancellationToken);
        // Update state → next OnDraw will render results
    }
    catch (ApiException ex)
    {
        _log.Error($"/nflip: {ex.Message}");
        _lastError = ex.Message; // picked up by OnDraw for in-window banner
    }
});
```

### 6. Architecture: file and class structure

Based on SPEC.md's suggested layout and the existing codebase patterns:

```
NamazuFlippers/
├── API/                              ← NEW directory
│   ├── Models/
│   │   ├── ScanRequest.cs            ← Request DTO (maps to POST /api/scan body)
│   │   ├── ScanResponse.cs           ← Response wrapper (list of items + metadata)
│   │   └── ScanItem.cs               ← Individual item result (the fields Phase 3 needs)
│   ├── SaddlebagClient.cs            ← HTTP client: builds requests, calls API, returns typed responses
│   ├── RateLimiter.cs                ← Timestamp-based delay enforcement
│   └── ApiException.cs               ← Custom exception for API failures
├── Configuration.cs                  ← Already exists; SaddlebagClient reads from this
├── NamazuFlippers.cs                 ← Entry point; instantiates SaddlebagClient
└── ...
```

**Class responsibilities:**

| Class | Responsibility | Public surface |
|-------|---------------|----------------|
| `ScanRequest` | Serializable DTO for `POST /api/scan` body. All properties camelCase (System.Text.Json snake_case policy handles conversion). | Properties only |
| `ScanResponse` | Root response wrapper. Contains `List<ScanItem>? items` and any pagination/total fields if present. | `Items` property |
| `ScanItem` | One arbitrage opportunity. **Only includes fields Phase 3 needs** (D-04/D-05): `ItemId`, `Name`, `HomePrice`, `CheapestServer`, `CheapestPrice`, `SalesPerDay`, `ExpectedDailyProfit`, `OutOfStock`. Add ROI and stack size if ScanEngine needs them for ranking. | Properties only |
| `SaddlebagClient` | Main API surface. Constructor takes `Configuration`, `IPluginLog`, and a `RateLimiter`. Exposes `Task<ScanResponse> ScanAsync(CancellationToken)`. Internally builds the request from config, waits on rate limiter, sends with retry, deserializes. | `ScanAsync(CancellationToken)` |
| `RateLimiter` | Thread-safe async delay gate. Instantiated once, shared across all API calls. | `WaitAsync(CancellationToken)` |
| `ApiException` | Custom exception with message, optional HTTP status code, and whether the error is retryable. | `Message`, `StatusCode`, `IsRetryable` |

### 7. Request model — mapping Configuration → ScanRequest

The `ScanRequest` class must map from the existing `Configuration` properties. Some are direct 1:1 mappings; others need transformation:

| Configuration Property | ScanRequest Field | Mapping |
|------------------------|-------------------|---------|
| `HomeWorld` | `home_server` | Direct string |
| `PreferredRoi` | `preferred_roi` | Direct int |
| `MinProfitAmount` | `min_profit_amount` | Direct int |
| `MinDesiredAvgPpu` | `min_desired_avg_ppu` | Direct int |
| `MinSalesPerWeek` | `min_sales` | Direct int |
| `RegionWide` | `region_wide` | Direct bool |
| `IncludeVendors` | `include_vendor` | Direct bool |
| `ShowOutOfStock` | `show_out_stock` | Direct bool |
| `CategoryFilters` | `filters` | Direct int[] |
| — | `min_stack_size` | Hardcoded to 1 (per SPEC.md default) |
| — | `hours_ago` | Hardcoded to 168 (7 days, per SPEC.md default) |
| — | `hq` | Hardcoded to false (NQ + HQ) |

**Note:** The `hours_ago`, `min_stack_size`, and `hq` parameters in the scan endpoint are not currently in `Configuration`. They should be added as static defaults in `ScanRequest` (or added to Configuration now and surfaced in Phase 4's ConfigWindow). Per D-06, don't pre-model unused data — but `hours_ago` could reasonably become a user-facing setting (1 day vs 7 day sales window).

### 8. Dalamud DI integration — how to instantiate and share the client

The plugin entry point (`NamazuFlippers.cs`) uses Dalamud constructor injection for framework services (`IDalamudPluginInterface`, `ICommandManager`, `IPluginLog`). Custom services like `SaddlebagClient` cannot be injected via Dalamud's container — the plugin manages them.

**Recommended instantiation pattern in `NamazuFlippers.cs`:**

```csharp
public class NamazuFlippers : IDalamudPlugin
{
    private readonly SaddlebagClient _apiClient;
    private readonly RateLimiter _rateLimiter;

    public NamazuFlippers(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        // ... existing setup ...
        
        _rateLimiter = new RateLimiter(TimeSpan.FromMilliseconds(1000));
        _apiClient = new SaddlebagClient(Configuration, log, _rateLimiter);
    }

    // Phase 3 will consume _apiClient directly
    // public SaddlebagClient ApiClient => _apiClient;
}
```

The `SaddlebagClient` is a plain class — no DI container needed. Phase 3's `ScanEngine` receives it via constructor injection from the entry point. This keeps the design simple and avoids pulling in a third-party DI library.

### 9. Error surfacing — chat + window pattern

Per D-01 and D-02, errors must appear in **both** channels:
1. **Dalamud chat log:** Via `IPluginLog.Error()` with the `/nflip:` prefix
2. **In-window error banner:** Via a string property on the plugin that `OnDraw` renders

Since Phase 4 builds the windows, Phase 2 should expose error state so Phase 4 can consume it. A simple pattern:

```csharp
// On the plugin class (or a shared state object)
public string? LastApiError { get; private set; }
```

The `SaddlebagClient` can expose errors through exceptions; the calling code catches and sets this state. Or the client itself can push errors to a shared status object.

**Recommended:** The client throws `ApiException` on failure. The caller (in Phase 2, a test harness; in Phase 3, `ScanEngine`) catches, logs to chat via `IPluginLog`, and sets `LastApiError` for the UI.

### 10. .NET 10 specific considerations

- **`ImplicitUsings`** is enabled in the `.csproj` — `System.Net.Http`, `System.Text.Json`, `System.Threading.Tasks` are all available without explicit `using` directives.
- **`Nullable`** is enabled — all model properties should be nullable-annotated correctly. Response fields from the API may be null for edge cases (e.g., `cheapest_server` could be null if no alternative server is found).
- **`net10.0-windows`** TFM: All standard .NET BCL APIs are available. No restrictions beyond what Dalamud's sandbox imposes.
- **Source generators** (`[JsonSerializable]`) are a compile-time feature available in .NET 10 without extra packages.

## Sources

- Kept: Microsoft HttpClient guidelines (learn.microsoft.com) — authoritative guidance on HttpClient lifecycle management for .NET applications
- Kept: Microsoft System.Text.Json source generation docs (learn.microsoft.com) — authoritative guidance on trim-safe JSON serialization
- Kept: Polly v8 documentation (thepollyproject.org) — standard .NET resilience library for retry/circuit-breaker patterns
- Kept: SPEC.md §Plugin Architecture — provides the target file structure (API/ folder with SaddlebagClient.cs, Endpoints.cs, RateLimiter.cs)
- Kept: SPEC.md §Scan Endpoint Parameters — defines exact request shape and all parameter types/defaults
- Kept: SPEC.md §Session Persistence — response item shape reference with field names and types
- Kept: CONTEXT.md §Implementation Decisions (D-01 through D-11) — all user-confirmed decisions that constrain implementation
- Kept: Configuration.cs — existing typed config properties, category presets, and defaults used by the HTTP client
- Kept: .csproj — confirms net10.0-windows, ImplicitUsings, Nullable, and no existing NuGet references beyond SDK
- Kept: NamazuFlippers.cs — establishes the Dalamud DI pattern used for framework services; guides how custom services should be instantiated

## Gaps

1. **Polly availability in Dalamud sandbox.** Polly.Core (v8+) is a NuGet package. It needs to be tested inside the Dalamud plugin loading environment. If the NuGet restore fails or the assembly fails to load at runtime, fall back to the manual retry loop described in Finding 3. **Next step:** attempt `dotnet add package Polly.Core` and verify the plugin loads in XIV Launcher.

2. **Saddlebag API response schema — exact field names and nullability.** The OpenAPI spec (https://docs.saddlebagexchange.com/openapi.json) should be fetched to confirm the exact JSON property names, types, and which fields are nullable. SPEC.md describes the fields at a high level but may not capture every edge case. **Next step:** fetch the OpenAPI spec and/or make a test `POST /api/scan` call with curl to inspect the real response shape.

3. **`hours_ago` configurability.** The scan endpoint accepts `hours_ago` (168 = 7 days by default). This is not in `Configuration.cs`. Should it be added now (so Phase 4 can expose it) or hardcoded? Affects the ScanRequest model design. **Next step:** decide whether to add `HoursAgo` and `MinStackSize` to Configuration now (easy) or defer.

4. **Dalamud chat integration.** The existing codebase uses `IPluginLog` for logging, but the requirement (D-01) specifies chat messages. `IPluginLog` writes to Dalamud's internal log, not necessarily the in-game chat. The actual chat output may require `IChatGui` (from `Dalamud.Plugin.Services`). **Next step:** verify whether `IPluginLog.Error()` is visible in the player's chat window, or if `IChatGui.Print()` is needed.

## Supervisor coordination

No blocking decisions needed. Research is complete based on available sources. The gaps listed above are implementation-verification tasks that can be resolved during plan execution (02-01 and 02-02).
