# Phase 3: Scan Engine & Route Optimizer - Context

**Gathered:** 2026-05-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the business-logic layer that turns the Phase 2 Saddlebag API client into a route-ready daily arbitrage result. This phase calls `/api/scan`, ranks usable opportunities, groups them by purchase source, orders route stops, manages the latest route/cache, and exposes structured success/empty/error results that Phase 4 can render. It may add command and login hooks needed to exercise the engine before the UI exists. It does not build the full route UI, buy/list checkboxes, profit tally UI, broad session state, shortage predictor, or game integration hooks beyond character-login scan startup.

</domain>

<decisions>
## Implementation Decisions

### Scan Trigger Behavior
- **D-01:** Keep `/nflip` as the UI toggle.
- **D-02:** Add an explicit manual scan command such as `/nflip scan` for Phase 3 testing before Phase 4 UI exists.
- **D-03:** Normal scan output should be concise: scan status, summary, and useful high-level counts. Richer diagnostic detail belongs in logs on failure, not normal success output.
- **D-04:** Ignore duplicate scans while one is already running and log a clear "scan already running" style message.
- **D-05:** Store the latest successful route in memory until replaced so Phase 4 can render it without re-scanning.

### Character Login Auto-Scan
- **D-06:** Treat Dalamud character login as the meaningful "login" event, using `IClientState.Login`; do nothing at game launch, title screen, or character select.
- **D-07:** Also check `IClientState.IsLoggedIn` during plugin startup in case the plugin loads while the character is already logged in.
- **D-08:** Shortly after character login/plugin-ready, load a valid cache if present; otherwise auto-start a scan when required config exists.
- **D-09:** If home world or required config is missing, do not auto-scan; surface/log the setup requirement.
- **D-10:** `/nflip scan` remains a manual refresh command and always bypasses cache.

### Ranking and Caps
- **D-11:** Do not rely on Saddlebag API response order as a ranking contract. Official OpenAPI docs do not document response ordering, a score field, or a stable ranking formula.
- **D-12:** Rank locally and deterministically after filtering invalid rows.
- **D-13:** Primary sort: `ExpectedDailyProfit` descending.
- **D-14:** Tie-breaks: `SalesPerDay` descending, then `CheapestPrice` ascending.
- **D-15:** Preserve `OutOfStock` as metadata for Phase 4 visual priority, but do not let OOS override profit or velocity.
- **D-16:** Optimize caps for route willingness rather than theoretical maximum profit. The selected route should be something the player is willing to actually run.
- **D-17:** Choose useful servers/stops first, then items, so `MaxServersToVisit` prevents routes that send the user to too many worlds for one item each.
- **D-18:** Profitable one-item server stops are allowed; do not require multiple items per server.
- **D-19:** Include vendor opportunities like normal, but preserve them distinctly so Phase 4 can render them as non-world purchase stops.
- **D-20:** If fewer opportunities meet thresholds than the desired route size, return the smaller route. Do not relax thresholds automatically in Phase 3.

### Route Ordering
- **D-21:** Order route stops by highest total expected value first so a user who stops early still hits the best stops.
- **D-22:** Ignore data center boundaries when the value difference is meaningful.
- **D-23:** Use hop/friction minimization only as a tie-breaker: if stop values are within about 20%, prefer the lower-friction or same-DC stop.
- **D-24:** Add a hardcoded FFXIV world-to-data-center map in Phase 3. This is enough for same-DC tie-breaking and avoids dynamic dependency risk.

### Scan Cache Boundary
- **D-25:** Implement file-backed scan cache in Phase 3, even though broader session persistence is Phase 5.
- **D-26:** Cache both the raw scan response and the derived route.
- **D-27:** Invalidate cache by age, scan-affecting config fingerprint, and cache/plugin schema version changes.
- **D-28:** Store the cache in the plugin-local/config data directory via Dalamud path APIs, not inside the main `Configuration` object and not in a repository-local path.
- **D-29:** `/nflip scan` always fetches fresh results and writes a fresh cache on success.
- **D-30:** Phase 4 UI can read a valid latest cache later without forcing an API call. This supports the workflow where the user logs in, lets the scan run, and does the shopping route later in the session.

### Error and Empty-Result Behavior
- **D-31:** If refresh fails but a previous route exists, keep showing/using the previous cache and log that refresh failed.
- **D-32:** Stale cache is better than nothing if refresh fails, but it must be clearly marked/logged as stale.
- **D-33:** If the API succeeds but filtering/ranking yields zero usable opportunities, return a structured empty route with a simple friendly message.
- **D-34:** Do not reuse previous cache for a successful-but-empty scan.
- **D-35:** Do not automatically relax thresholds in Phase 3.
- **D-36:** If failure leaves no usable cache, return a structured error result rather than raw exceptions or null.
- **D-37:** User-facing UI messages should be friendly and plain language; logs keep technical details such as status codes, exception messages, retry behavior, and diagnostics.

### the agent's Discretion
- Exact command parsing shape for `/nflip scan`, as long as `/nflip` remains the UI toggle.
- Exact summary fields logged on successful scan, as long as normal output stays concise.
- Exact cache schema and config fingerprint implementation.
- Exact route/result model class names and folder structure, as long as they are easy for Phase 4 to consume.
- Exact login delay mechanism after `IClientState.Login`, as long as scanning does not run at game launch/title/character select.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase and Requirements
- `.planning/ROADMAP.md` - Phase 3 goal, requirements, success criteria, and planned split between ScanEngine and RouteOptimizer.
- `.planning/REQUIREMENTS.md` - SCAN-01 through SCAN-04 plus related UI/session requirements that consume Phase 3 output.
- `.planning/PROJECT.md` - Core value, daily-session workflow, technical constraints, and existing key decisions.

### Prior Phase Context
- `.planning/phases/01-plugin-shell/01-CONTEXT.md` - Plugin identity, `/nflip` command, configuration model decisions, and minimal scaffold approach.
- `.planning/phases/02-api-integration/02-CONTEXT.md` - API client boundary, modeled response fields, retry/rate-limit decisions, and deferred shortage predictor.
- `.planning/phases/02-api-integration/02-SUMMARY.md` - Files and public API produced by Phase 2.
- `.planning/phases/02-api-integration/02-UAT.md` - Phase 2 runtime UAT was blocked because no scan trigger/UI surface existed; Phase 3 must provide a testable trigger.

### Product and API Specification
- `SPEC.md` - Overall route workflow, `/api/scan` strategy, example session/cache shape, suggested folder layout, and UI/session expectations.
- `SPEC.md` §Scan Endpoint Parameters - Request parameters and defaults for `/api/scan`.
- `SPEC.md` §MVP Flow - One scan, route grouping, and cached daily-session behavior.
- `SPEC.md` §Session Persistence - Reference JSON shape for route/cache/session state.
- Saddlebag OpenAPI: `https://docs.saddlebagexchange.com/openapi.json` - Official endpoint docs. Important caveat: response ordering/scoring is not documented as a contract, so Phase 3 ranks locally.
- Dalamud `IClientState` API docs: `https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState` - Character login semantics for auto-scan.

### Existing Code
- `NamazuFlippers/NamazuFlippers.cs` - Plugin entry point, command handler, API client construction, `LastApiError` property, and future window integration point.
- `NamazuFlippers/Configuration.cs` - Scan-affecting config values: home world, thresholds, category filters, vendor/OOS toggles, max items/servers, cache duration.
- `NamazuFlippers/API/SaddlebagClient.cs` - `ScanAsync(CancellationToken)` and Phase 2 retry/error behavior.
- `NamazuFlippers/API/RateLimiter.cs` - Existing API call rate limiter.
- `NamazuFlippers/API/Models/ScanRequest.cs` - Configuration-to-request mapping.
- `NamazuFlippers/API/Models/ScanResponse.cs` - Raw API response wrapper.
- `NamazuFlippers/API/Models/ScanItem.cs` - Fields available for ranking, grouping, OOS metadata, and route construction.
- `NamazuFlippers/API/ApiException.cs` - Existing structured API failure type.
- `NamazuFlippers/FirstRunWindow.cs` - Existing hardcoded world list; Phase 3 should add/centralize world-to-DC data without breaking first-run behavior.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SaddlebagClient.ScanAsync(CancellationToken)` already performs the API call and returns `ScanResponse`.
- `ScanItem` exposes the local ranking fields Phase 3 needs: `ExpectedDailyProfit`, `SalesPerDay`, `CheapestPrice`, `CheapestServer`, and `OutOfStock`.
- `Configuration` already contains `MaxItemsPerSession`, `MaxServersToVisit`, `CacheDurationHours`, thresholds, filters, and vendor/OOS toggles for ranking, caps, and cache fingerprinting.
- `RateLimiter` is already wired into `SaddlebagClient`.
- `NamazuFlippers.LastApiError` exists as a future UI-facing error surface.

### Established Patterns
- Keep the plugin entry point thin and delegate feature logic to dedicated classes.
- Add directories only when needed; Phase 3 can add a `Core/`, `Data/`, or similar folder for scan engine, route optimizer, cache models, and world/DC data.
- Use Dalamud service injection through the plugin constructor for runtime services such as `IClientState` and plugin-local file path access.
- Prefer simple local JSON for route/cache data; no database.

### Integration Points
- `NamazuFlippers.OnCommand` should continue toggling UI for bare `/nflip`, while parsing a scan subcommand for `/nflip scan`.
- Plugin startup should subscribe to character-login state through `IClientState` and clean up subscriptions in `Dispose`.
- Phase 3 should expose route/latest-result state through a clean API that Phase 4 can read.
- Cache load/save should use plugin-local paths available through Dalamud APIs, not repository paths.
- Route output should distinguish world stops from vendor-like purchase sources so the UI can render them differently.

</code_context>

<specifics>
## Specific Ideas

- The plugin should feel proactive: when the character logs in and no valid cache exists, it should scan shortly after login so the route is ready when the user opens the UI.
- `/nflip scan` means "refresh now"; it should not silently reuse cache.
- The route should be something the user is willing to run, not a theoretical max-profit list that requires too much travel.
- If the user stops mid-route, value-first stop ordering should make the completed portion still worthwhile.
- Empty result messaging should stay simple for now: no opportunities matched current settings.

</specifics>

<deferred>
## Deferred Ideas

- Future two-tier scan mode: first run strict "priority" settings, then optionally use a more relaxed fallback configuration if no opportunities are found.
- Full DailyRouteWindow UI, buy/list checkboxes, progress, and profit tally remain Phase 4.
- Broader session state for bought/listed/current stop remains Phase 5.
- Shortage predictor supplement remains Phase 6.
- Market board and server travel hooks remain later optional/polish phases.

</deferred>

---

*Phase: 03-scan-engine-route-optimizer*
*Context gathered: 2026-05-07*
