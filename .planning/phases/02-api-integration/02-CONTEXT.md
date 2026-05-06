# Phase 2: API Integration - Context

**Gathered:** 2026-05-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the HTTP client layer that talks to the Saddlebag Exchange API. Deliver typed request/response models for `POST /api/scan`, an HTTP client that constructs and sends requests, a rate limiter, and error handling with user feedback. This is infrastructure — Phase 3 (Scan Engine & Route Optimizer) consumes this layer. The `POST /api/ffxiv/shortagefutures` endpoint is out of scope until Phase 6.

</domain>

<decisions>
## Implementation Decisions

### Error handling UX
- **D-01:** Both channels — print an error to the Dalamud chat log AND show an error state in the plugin window
- **D-02:** Chat message follows a consistent prefix pattern (e.g., `/nflip: <message>`)
- **D-03:** Window shows inline error text/banner when API is unavailable

### Model fidelity
- **D-04:** Model only the fields Phase 3 (Scan Engine) needs — not the full API response schema
- **D-05:** Fields to model: item name, item ID, home price, cheapest server, cheapest price, sales velocity, expected daily profit, OOS flag — plus any fields the Scan Engine ranking/grouping logic requires
- **D-06:** Add fields later if future phases need them; don't pre-model unused data

### HTTP client resilience
- **D-07:** Auto-retry on transient failures with exponential backoff
- **D-08:** Retry 2-3 times before surfacing an error to the user
- **D-09:** Non-transient failures (4xx, 5xx not related to temporary server issues) surface immediately

### Rate limiter design
- **D-10:** Simple minimum delay between API calls — no token bucket or complex throttling
- **D-11:** All endpoints are Universalis-safe, so enforcement is a politeness safeguard, not a hard constraint

### the agent's Discretion
- Exact retry count, backoff multiplier, and timeout duration
- Minimum delay value for rate limiter
- Model class naming and namespace structure (request DTO, response DTO, shared types)
- HTTP client construction approach (raw `HttpClient`, `IHttpClientFactory`, etc.)
- Whether to make the client injectable as a Dalamud service or use a simpler static/singleton pattern
- Deserialization library choice (System.Text.Json vs Newtonsoft.Json)
</decisions>

<specifics>
## Specific Ideas

- API design is fully specified in `SPEC.md` — request shape, parameters with defaults, response fields are all documented
- `/api/scan` is the sole endpoint for Phase 2; shortage predictor endpoint (`/api/ffxiv/shortagefutures`) is deferred to Phase 6 per ROADMAP.md
- Configuration values for API parameters already exist in `Configuration.cs` (preferred_roi, min_profit_amount, region_wide, include_vendors, show_out_stock, category_filters, etc.)

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### API specification
- `SPEC.md` §Scan Endpoint Parameters — Full parameter table with types, descriptions, and suggested defaults for `POST /api/scan`
- `SPEC.md` §API Strategy — Which endpoints are used (primary: `/api/scan`), response shape description (home price, cheapest server, sales velocity, ROI%, OOS flag)
- `SPEC.md` §Session Persistence — Response item shape reference with field names and types
- `SPEC.md` §Plugin Architecture — Suggested folder layout (`API/` directory with `SaddlebagClient.cs`, `Endpoints.cs`, `RateLimiter.cs`)

### API documentation (external)
- Saddlebag ReDoc (full spec): https://docs.saddlebagexchange.com/redoc
- OpenAPI JSON: https://docs.saddlebagexchange.com/openapi.json

### Requirements
- `.planning/REQUIREMENTS.md` §API Integration — API-01 (HTTP client with typed models), API-02 (rate limiter), API-03 (typed request/response models)
- `.planning/REQUIREMENTS.md` §Traceability — Phase 2 requirement status

### Existing code
- `NamazuFlippers/Configuration.cs` — All API-relevant config values already typed and defaulted (PreferredRoi, MinProfitAmount, MinDesiredAvgPpu, MinSalesPerWeek, RegionWide, CategoryFilters, IncludeVendors, ShowOutOfStock, MaxItemsPerSession, MaxServersToVisit, CacheDurationHours)
- `NamazuFlippers/NamazuFlippers.cs` — Entry point with Dalamud DI pattern (`IDalamudPluginInterface`, `ICommandManager`, `IPluginLog`)

### Project context
- `.planning/PROJECT.md` — Technical constraints (Dalamud API, .NET 8+, ImGui, single-player), existing key decisions (single `/api/scan` endpoint, JSON session persistence)
- `.planning/ROADMAP.md` §Phase 2 — Success criteria (3 items), plan descriptions (02-01 HTTP client + models, 02-02 rate limiter + errors)
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Configuration.cs`: All API parameters are already typed C# properties with sensible defaults — the HTTP client can read directly from the config instance
- Named category presets (`FurnitureIds`, `CollectibleIds`, `GlamourIds`, `DefaultCategoryFilters`) — reuse these when building the scan request body

### Established Patterns
- Dalamud dependency injection: services accessed via constructor injection (`IDalamudPluginInterface`, `IPluginLog`, `ICommandManager`)
- Minimal scaffold approach: Phase 1 added only what it needed; Phase 2 adds `API/` directory for client, models, and rate limiter
- Namespace: `NamazuFlippers` — new code goes under this namespace or sub-namespaces

### Integration Points
- Phase 3 `ScanEngine` will consume the HTTP client from `API/` — the client's public interface is the contract
- Config values for API calls come from `Configuration` instance, passed via the plugin entry point
- No existing API/, Core/, or Data/ folders — Phase 2 creates `API/`
</code_context>

<deferred>
## Deferred Ideas

- Shortage predictor endpoint (`POST /api/ffxiv/shortagefutures`) — Phase 6 (plan 06-01)
- Scan engine that consumes this client — Phase 3
- Scan result caching — Phase 3 (builds on top of this client)
- Session persistence — Phase 5

</deferred>

---

*Phase: 02-api-integration*
*Context gathered: 2026-05-05*
