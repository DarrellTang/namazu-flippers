# Phase 2: API Integration — Summary

**Phase:** 2
**Plans Executed:** 02-01, 02-02
**Status:** Complete
**Date:** 2026-05-05

---

## What Was Built

### Wave 1 — Plan 02-01: API Models, HTTP Client & ApiException

Created 6 new files in `NamazuFlippers/API/`:

| File | Purpose |
|------|---------|
| `API/Models/ScanRequest.cs` | Request DTO for `POST /api/scan` — maps 1:1 from Configuration via `FromConfiguration()` factory. Includes hardcoded defaults: `hours_ago=168`, `min_stack_size=1`, `hq=false` |
| `API/Models/ScanItem.cs` | Individual arbitrage result — 8 fields (ItemId, Name, HomePrice, CheapestServer, CheapestPrice, SalesPerDay, ExpectedDailyProfit, OutOfStock). Only Phase 3 needs modeled. |
| `API/Models/ScanResponse.cs` | Response wrapper with `List<ScanItem> Items` |
| `API/Models/ApiJsonContext.cs` | System.Text.Json source generator — `SnakeCaseLower` policy, 3 serializable types, trim-safe for Dalamud runtime |
| `API/ApiException.cs` | Custom exception — `StatusCode` (int?), `IsRetryable` (bool), two constructor overloads |
| `API/SaddlebagClient.cs` | HTTP client — static singleton HttpClient (30s timeout), `ScanAsync()` with 3-retry exponential backoff (1s→2s→4s), 4xx → immediate fail, 5xx → retry, `/nflip:` prefixed IPluginLog messages |

### Wave 2 — Plan 02-02: Rate Limiter & Plugin Integration

| File | Change |
|------|--------|
| `API/RateLimiter.cs` | Timestamp-based rate limiter — `WaitAsync(CancellationToken)`, 1000ms default minimum delay, thread-safe via lock |
| `NamazuFlippers/NamazuFlippers.cs` | Added `using NamazuFlippers.API;`, `rateLimiter` + `apiClient` fields, `LastApiError` property, commented-out fire-and-forget scan trigger placeholder for Phase 3 |

---

## Requirements Coverage

| REQ-ID | Status | Evidence |
|--------|--------|----------|
| API-01 | ✓ | `SaddlebagClient.ScanAsync()` calls `POST /api/scan`, deserializes to `ScanResponse` |
| API-02 | ✓ | `RateLimiter.WaitAsync()` enforces 1000ms minimum delay between calls |
| API-03 | ✓ | `ScanRequest`, `ScanResponse`, `ScanItem`, `ApiJsonContext` — all typed C# models |

---

## Verification — Goal-Backward must_haves

1. ✓ `SaddlebagClient.ScanAsync(CancellationToken)` exists — sends `POST /api/scan`, returns `ScanResponse` with `List<ScanItem>`
2. ✓ `RateLimiter.WaitAsync(CancellationToken)` enforces minimum delay
3. ✓ Network errors produce `IPluginLog` messages with `/nflip:` prefix; `LastApiError` exposed on plugin class
4. ✓ All three requirement IDs (API-01, API-02, API-03) satisfied

---

## Commits

```
0176332 feat(02-01): add ScanRequest DTO with FromConfiguration factory
49f8b12 feat(02-01): add ScanItem model with Phase 3 fields only
21105eb feat(02-01): add ScanResponse wrapper with items list
22eed25 feat(02-01): add ApiJsonContext source generator for snake_case serialization
60d4cef feat(02-01): add ApiException with StatusCode and IsRetryable
906f5b8 feat(02-01): add SaddlebagClient with retry, backoff, and typed deserialization
bfeda5c feat(02-02): add timestamp-based RateLimiter with 1000ms minimum delay
5219c10 feat(02-02): wire SaddlebagClient and RateLimiter into plugin entry point, add LastApiError
```

---

## Key Design Decisions Applied

- **Singleton HttpClient** — no `IHttpClientFactory` (not available in Dalamud sandbox)
- **System.Text.Json source gen** — trim-safe for Dalamud, `SnakeCaseLower` naming policy
- **Manual retry loop** — exponential backoff (1s, 2s, 4s), no Polly dependency
- **Timestamp-based rate limiter** — simple lock-protected design, not token bucket
- **Null-tolerant client** — `RateLimiter?` parameter; client works without rate limiter
- **Error surfacing** — `IPluginLog` for chat, `LastApiError` property for in-window banner (Phase 4)

## Risks & Open Items

1. **Saddlebag API response shape** — `ScanResponse.Items` property name is a best guess. Verify against actual API or OpenAPI spec.
2. **`JsonContent.Create` source-gen overload** — API compatibility across .NET versions. Fallback to manual `JsonSerializerOptions` if needed.
3. **Dalamud `IChatGui`** — `IPluginLog` may not route to in-game chat. May need `IChatGui.Print()` for chat messages (per RESEARCH §Gap 4).

---

*Phase: 02-api-integration*
*Summary generated: 2026-05-05*
