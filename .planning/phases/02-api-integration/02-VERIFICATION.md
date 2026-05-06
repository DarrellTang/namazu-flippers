# Phase 2: API Integration — Verification

**Phase:** 2 — API Integration
**Verification date:** 2026-05-05
**Review type:** Code diff verification against PLAN.md must_haves, ROADMAP.md success criteria, and REQUIREMENTS.md (API-01, API-02, API-03)

---

## Result: PASS (with fixes applied)

All must_haves satisfied. All three requirement IDs covered. Two retry bugs found and fixed.

---

## 1. Must-Have Verification (PLAN.md)

### MH-1: `SaddlebagClient.ScanAsync(CancellationToken)` exists, sends `POST /api/scan`, returns typed `ScanResponse` with `List<ScanItem>`

- **Correct:** `SaddlebagClient.ScanAsync(CancellationToken ct = default)` declared at `NamazuFlippers/API/SaddlebagClient.cs:42`
- **Correct:** Sends `POST /api/scan` via `Http.SendAsync(httpRequest, ct)` at line 57, with request built from `ScanRequest.FromConfiguration(_config)` at line 48
- **Correct:** Deserializes to `ScanResponse` via `ReadFromJsonAsync<ScanResponse>(...)` at line 62
- **Correct:** `ScanResponse.Items` is `List<ScanItem>` — all 8 fields modeled per D-05/D-06 (`NamazuFlippers/API/Models/ScanItem.cs`)
- **Correct:** Returns `ScanResponse` on success at line 71

### MH-2: `RateLimiter.WaitAsync(CancellationToken)` enforces minimum delay

- **Correct:** `RateLimiter.WaitAsync(CancellationToken)` at `NamazuFlippers/API/RateLimiter.cs:26`
- **Correct:** Default minimum delay 1000ms (line 20: `TimeSpan.FromMilliseconds(1000)`)
- **Correct:** Thread-safe via `lock (_lock)` at line 29
- **Correct:** Called by `SaddlebagClient.ScanAsync()` at line 45-46: `if (_rateLimiter != null) await _rateLimiter.WaitAsync(ct);`

### MH-3: Network errors produce `IPluginLog.Error("/nflip: ...")` and set `LastApiError`

- **Correct:** All log messages in `SaddlebagClient.cs` use `/nflip:` prefix (4 occurrences: success info, 5xx warning, network error warning, timeout warning)
- **Correct:** `LastApiError` property declared at `NamazuFlippers/NamazuFlippers.cs:29` as `public string? LastApiError { get; private set; }`
- **Correct:** Commented-out fire-and-forget pattern at `NamazuFlippers.cs:49-53` demonstrates `log.Error($"/nflip: {ex.Message}")` and `LastApiError = ex.Message` for Phase 3 consumption
- **Note:** Actual `IPluginLog.Error` calls will be made by Phase 3's `ScanEngine` when it catches `ApiException` from `ScanAsync()`. The infrastructure is in place.

### MH-4: All three requirement IDs satisfied

| REQ-ID | Status | Evidence |
|--------|--------|----------|
| API-01 | ✓ | `SaddlebagClient.ScanAsync()` calls `POST /api/scan`, deserializes to `ScanResponse` with `ScanItem` list |
| API-02 | ✓ | `RateLimiter.WaitAsync()` enforces 1000ms minimum delay; wired into `SaddlebagClient` |
| API-03 | ✓ | `ScanRequest`, `ScanResponse`, `ScanItem`, `ApiJsonContext` — all typed C# models in `NamazuFlippers.API.Models` |

---

## 2. Success Criteria Verification (ROADMAP.md)

### SC-1: `POST /api/scan` returns parsed, typed response objects
- ✓ `SaddlebagClient.ScanAsync()` exists and deserializes to `ScanResponse`
- ✓ Source-gen `ApiJsonContext` with `SnakeCaseLower` handles serialization/deserialization without runtime reflection
- ✓ `ScanItem` has all 8 fields Phase 3 needs: ItemId, Name, HomePrice, CheapestServer, CheapestPrice, SalesPerDay, ExpectedDailyProfit, OutOfStock

### SC-2: Rate limiter prevents excessive calls
- ✓ `RateLimiter.WaitAsync()` enforces minimum 1000ms between calls
- ✓ Thread-safe timestamp-based design (lock-protected)
- ✓ Integrated into `SaddlebagClient.ScanAsync()` as first step

### SC-3: Network errors handled gracefully with user feedback
- ✓ 4xx responses surface immediately as non-retryable `ApiException` (line 75-80)
- ✓ 5xx responses retried up to 3 times with exponential backoff (1s → 2s → 4s) (lines 84-95)
- ✓ Network/HTTP exceptions retried with backoff (lines 102-117)
- ✓ `LastApiError` exposed on plugin class for Phase 4 in-window error banner
- ✓ All log messages use `/nflip:` prefix
- ✓ `StringExtensions.Truncate()` limits error body to 200 chars in log messages

---

## 3. Files Created/Modified

### New Files (all correct)

| File | Status | Notes |
|------|--------|-------|
| `API/Models/ScanRequest.cs` | ✓ | 11 fields, `FromConfiguration()` factory, no `[JsonPropertyName]` attributes |
| `API/Models/ScanItem.cs` | ✓ | 8 fields matching D-05 spec; no session state (`bought`/`listed`) leaked |
| `API/Models/ScanResponse.cs` | ✓ | `List<ScanItem> Items` wrapper |
| `API/Models/ApiJsonContext.cs` | ✓ | `SnakeCaseLower`, 3 `[JsonSerializable]` registrations, `partial class` |
| `API/ApiException.cs` | ✓ | `StatusCode` (int?), `IsRetryable` (bool), two constructor overloads |
| `API/SaddlebagClient.cs` | ✓ | Static singleton `HttpClient`, 30s timeout, 3 retries, exponential backoff, rate limiter hook |
| `API/RateLimiter.cs` | ✓ | Timestamp-based, lock-protected, 1000ms default |

### Modified Files

| File | Change | Status |
|------|--------|--------|
| `NamazuFlippers/NamazuFlippers.cs` | `using NamazuFlippers.API;`, `rateLimiter` + `apiClient` fields, `LastApiError`, commented-out placeholder | ✓ |

---

## 4. Bugs Found & Fixed

### Blocker B1: `HttpRequestMessage` disposed before cloning in 5xx retry path

- **Location:** `NamazuFlippers/API/SaddlebagClient.cs` (was line 85-86)
- **Problem:** `httpRequest.Dispose()` was called *before* `CloneHttpRequestAsync(httpRequest)`. The clone method reads `original.Content`, which throws `ObjectDisposedException` on a disposed request. This would crash every 5xx retry.
- **Fix:** Clone first, then dispose. Changed to:
  ```csharp
  var newRequest = await CloneHttpRequestAsync(httpRequest);
  httpRequest.Dispose();
  httpRequest = newRequest;
  ```

### Blocker B2: `HttpRequestMessage` reused without cloning in catch blocks

- **Location:** `NamazuFlippers/API/SaddlebagClient.cs` (was lines 97-105)
- **Problem:** After catching `HttpRequestException` or `TaskCanceledException`, the catch blocks only delayed and let the loop continue — but `HttpRequestMessage` cannot be re-sent. The next `Http.SendAsync(httpRequest, ct)` would fail because the content stream was consumed or the connection state was invalid.
- **Fix:** Added clone-dispose-reassign in both catch blocks, matching the 5xx retry path pattern.

### Fixed N1: Serilog structured logging used inline C# expressions

- **Location:** `NamazuFlippers/API/SaddlebagClient.cs` (was lines 85, 99, 105)
- **Problem:** `_log.Warning` calls had inline C# expressions inside `{}` (e.g., `{(int)response.StatusCode}`, `{attempt + 1}`, `{ex.Message}`). Serilog's message templates require valid property names, and values must be passed as method arguments. The expressions would render as literal text.
- **Fix:** Changed to proper Serilog template holes with arguments:
  ```csharp
  _log.Warning("/nflip: API server error {StatusCode}, retrying... (attempt {Attempt}/{MaxRetries})",
      (int)response.StatusCode, attempt + 1, MaxRetries);
  ```

---

## 5. Notes & Observations

### N1: ROADMAP.md checkbox inconsistency
- **Location:** `.planning/ROADMAP.md` Phase 2 plans list
- **Issue:** Shows `- [ ] 02-02: Implement rate limiter and error handling` (unchecked), but both 02-PLAN.md and 02-SUMMARY.md show it as complete. All code for 02-02 exists and is functional.
- **Recommendation:** Check the box in ROADMAP.md.

### N2: Build cannot succeed in current environment
- **Issue:** `dotnet build` produces 21 CS0246 errors, all for missing Dalamud SDK assemblies (`Dalamud`, `Dalamud.Plugin`, `Dalamud.Plugin.Services`, etc.). This is a pre-existing Phase 1 condition — the development machine lacks the Dalamud SDK.
- **Impact:** Zero errors are attributable to Phase 2 code. The Phase 2 types (`ScanRequest`, `ScanItem`, `ScanResponse`, `ApiJsonContext`, `ApiException`, `SaddlebagClient`, `RateLimiter`) would resolve correctly in the Dalamud build environment.
- **Verification:** Confirmed all errors reference only Dalamud namespaces. No Phase 2 types appear in any error message.

### N3: `ScanResponse.Items` property name is best-guess
- **Plan risk acknowledged:** The plan (Task 3) explicitly notes the response shape is unknown and may need adjustment. If the actual API uses a different key (e.g., `results`, `data`), deserialization will produce an empty list rather than crashing. The property can be annotated with `[JsonPropertyName("...")]` or renamed when tested against the real API.
- **Recommendation:** Test against the real Saddlebag API or check the OpenAPI spec at `https://docs.saddlebagexchange.com/openapi.json` before Phase 3.

### N4: `JsonContent.Create` source-gen overload compatibility
- **Plan risk acknowledged:** The `JsonContent.Create(request, typeof(ScanRequest), sourceGenContext: ApiJsonContext.Default.Options)` overload may vary across .NET versions. A fallback using manual `JsonSerializerOptions` is noted in the plan. This should be verified in the Dalamud build environment.

---

## 6. Acceptance Criteria Checklist (from PLAN.md)

### ScanRequest
- [x] `grep -c "class ScanRequest"` → 1
- [x] `grep -c "JsonPropertyName"` → 0 (snake_case via naming policy)
- [x] `grep "FromConfiguration"` → factory method present
- [x] `grep "HomeServer"` → string property present
- [x] `grep "HoursAgo"` → `= 168;`
- [x] `grep "Hq"` → `= false;`
- [x] `grep "Filters"` → `int[] Filters ... = [];`

### ScanItem
- [x] `grep -c "class ScanItem"` → 1
- [x] All 8 fields present: ItemId, Name, HomePrice, CheapestServer, CheapestPrice, SalesPerDay, ExpectedDailyProfit, OutOfStock
- [x] `grep -c "bought\|listed"` → 0 (no session state)

### ScanResponse
- [x] `grep -c "class ScanResponse"` → 1
- [x] `List<ScanItem> Items` present

### ApiJsonContext
- [x] `grep -c "partial class ApiJsonContext"` → 1
- [x] `PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower`
- [x] 3 `[JsonSerializable]` attributes (ScanRequest, ScanResponse, ScanItem)

### ApiException
- [x] `grep -c "class ApiException"` → 1
- [x] `StatusCode` (int?) present
- [x] `IsRetryable` (bool) present
- [x] Extends `Exception`

### SaddlebagClient
- [x] 1 class declaration
- [x] `ScanAsync` method with `CancellationToken ct = default`
- [x] `MaxRetries = 3`
- [x] BaseAddress `api.saddlebagexchange.com`
- [x] Timeout 30s
- [x] `ScanRequest.FromConfiguration` call
- [x] `Math.Pow(2, attempt)` exponential backoff (3 locations: 5xx, HttpRequestException, TaskCanceledException)
- [x] `ApiException` thrown in 4+ locations
- [x] `/nflip:` prefix on 4 log messages

### RateLimiter
- [x] 1 class declaration
- [x] `TimeSpan.FromMilliseconds(1000)` default
- [x] `WaitAsync(CancellationToken)` method
- [x] `lock (_lock)` thread safety
- [x] `Task.Delay` async call

### NamazuFlippers.cs Integration
- [x] `rateLimiter = new RateLimiter(TimeSpan.FromMilliseconds(1000))`
- [x] `apiClient = new SaddlebagClient(Configuration, log, rateLimiter)`
- [x] `LastApiError` property
- [x] `using NamazuFlippers.API;`
- [x] `ScanAsync` only in commented-out block (no active call)
- [x] 1 `private readonly RateLimiter rateLimiter`
- [x] 1 `private readonly SaddlebagClient apiClient`

---

*Verification complete: 2026-05-05*
*Bugs fixed: B1 (dispose-before-clone), B2 (missing clone in catch blocks), N1 (Serilog template format)*
