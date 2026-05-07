# Phase 3: Scan Engine & Route Optimizer - Research

**Phase:** 03 - Scan Engine & Route Optimizer
**Date:** 2026-05-07
**Status:** Complete

## Research Question

What do we need to know to plan Phase 3 well: implementing a scan engine that consumes the Phase 2 Saddlebag API client, ranks usable opportunities, builds route-ready output, and manages scan cache/refresh behavior without building the Phase 4 UI?

## Findings

### 1. The Saddlebag scan request is explicit, but response ordering is not a contract

The official OpenAPI document exposes `POST /api/scan` with a concrete request body schema named `FFXIVResellingParams`. The schema includes the fields this project already modeled: `home_server`, `preferred_roi`, `min_profit_amount`, `min_desired_avg_ppu`, `min_stack_size`, `hours_ago`, `min_sales`, `hq`, `filters`, `region_wide`, `include_vendor`, and `show_out_stock`.

The same operation does not provide a detailed typed 200-response schema. That means Phase 3 should not treat response order as a durable ranking contract. It should filter invalid rows and sort locally using the Phase 3 decisions:

1. `ExpectedDailyProfit` descending.
2. `SalesPerDay` descending.
3. `CheapestPrice` ascending.

Source: https://docs.saddlebagexchange.com/openapi.json

### 2. Phase 2's API model boundary is intentionally thin

Phase 2 created:

- `SaddlebagClient.ScanAsync(CancellationToken)` returning `ScanResponse`.
- `ScanResponse.Items`.
- `ScanItem` fields needed for Phase 3 ranking: `ItemId`, `Name`, `HomePrice`, `CheapestServer`, `CheapestPrice`, `SalesPerDay`, `ExpectedDailyProfit`, `OutOfStock`.
- `ApiJsonContext` with `JsonKnownNamingPolicy.SnakeCaseLower`.

The Phase 2 summary explicitly flags that the response wrapper key is a best guess. Phase 3 planning should include a small guard task to verify the deserialized shape against a live or captured response. If the API returns an array directly or uses a key such as `data`/`results`, the executor should adapt `ScanResponse` using a custom converter or `[JsonPropertyName]` on the wrapper, while keeping downstream `ScanItem` names stable.

### 3. The scan engine should own business outcomes, not UI state

The phase boundary says Phase 3 exposes structured success, empty, and error results. It should not add bought/listed checkboxes, running profit tally, completed-stop collapse behavior, or broad session state. Those are Phase 4 and Phase 5.

Recommended result model:

- `ScanStatus`: `Success`, `Empty`, `Error`, `UsingCache`, `UsingStaleCache`.
- `RankedOpportunity`: normalized immutable/domain object copied from valid `ScanItem` rows.
- `ScanEngineResult`: status, friendly message, technical error string, item counts, selected opportunities, generated route once RouteOptimizer exists, cache freshness metadata.

This keeps Phase 4 from inspecting raw exceptions, nulls, or arbitrary API response shapes.

### 4. Capping should happen around useful stops, not only item count

Phase 3 decisions require the selected route to be something the player will run. The safest plan is:

1. Filter invalid rows (`ItemId <= 0`, blank `Name`, blank purchase source unless vendor, non-positive prices/profit, below configured thresholds).
2. Sort by the deterministic ranking order.
3. Group by purchase source.
4. Select stops by total expected value before trimming final item count.
5. Keep profitable one-item stops if they fit within `MaxServersToVisit`.
6. Limit final items to `MaxItemsPerSession`.

This directly supports D-16 through D-20 and avoids a high-profit list that sends the player to too many worlds for low-value stops.

### 5. Route ordering is value-first with friction as a tie-breaker

The route optimizer should not solve a full traveling-salesman problem. FFXIV world travel has coarse friction:

- Same world/home stop: no shopping stop needed.
- Same data center: lower friction.
- Cross-data-center travel: higher friction.
- Vendor source: render as a distinct non-world purchase stop.

Per context decisions D-21 through D-24:

1. Primary route order is stop total expected value descending.
2. If two stop totals are within about 20%, prefer same-data-center/lower-friction stops first.
3. Ignore data center boundaries when value difference is meaningful.
4. Use a hardcoded world-to-data-center map in Phase 3.

### 6. World data should be centralized before adding route logic

`FirstRunWindow.cs` has a private `KnownWorlds` array with 85 worlds. Route optimization needs world-to-data-center data. Duplicating the world list increases drift risk.

Recommended plan:

- Add `Data/WorldData.cs` with a static dictionary from world name to data center, using `StringComparer.OrdinalIgnoreCase`.
- Include all known worlds from `FirstRunWindow.cs`.
- Add helpers:
  - `IsKnownWorld(string world)`
  - `GetDataCenter(string world)`
  - `GetTravelFriction(string homeWorld, string purchaseWorld)`
- Update `FirstRunWindow` to consume `WorldData.KnownWorlds` or leave it unchanged in Phase 3 only if the executor documents the duplicate list as temporary. Centralizing is preferable because D-24 explicitly calls for hardcoded world/DC data in Phase 3.

### 7. Cache belongs in plugin-local storage and should include both raw and derived data

Phase context decisions D-25 through D-30 require file-backed scan cache in this phase. The cache should not live inside `Configuration` and should not write to a repository path.

Recommended files/classes:

- `Data/ScanCacheStore.cs`: loads/saves cache JSON under `IDalamudPluginInterface.ConfigDirectory`.
- `Data/ScanCacheEnvelope.cs`: `SchemaVersion`, `CreatedAtUtc`, `ExpiresAtUtc`, `ConfigFingerprint`, raw `ScanResponse`, derived route/result.
- `Data/ScanCacheSerializerContext.cs` or additional `[JsonSerializable]` registrations in `ApiJsonContext`.

The cache is valid only when:

1. `CreatedAtUtc + CacheDurationHours` is in the future.
2. `ConfigFingerprint` matches scan-affecting config.
3. `SchemaVersion` matches the current cache schema version.

Manual `/nflip scan` must bypass cache and write a new cache on success.

### 8. Login auto-scan needs cancellation and disposal discipline

Dalamud `IClientState` exposes login state and a login event. The phase context requires using character login, not game launch or title screen. The plugin should:

- Subscribe to `IClientState.Login`.
- Check `IClientState.IsLoggedIn` during startup.
- Delay briefly after login/plugin-ready before scanning.
- Skip auto-scan if home world is missing.
- Ignore duplicate scans while one is running.
- Cancel pending scan work in `Dispose`.

Source: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState/

### 9. Command handling should parse `/nflip scan` without breaking `/nflip`

Current `OnCommand` toggles visibility for every invocation. Phase 3 must preserve D-01:

- Bare `/nflip` toggles the UI/first-run popup behavior.
- `/nflip scan` starts a manual refresh and bypasses cache.
- Unknown subcommands log a concise help message.

Because Phase 4 UI does not exist yet, success output should be log-level concise: status, route item count, stop count, cache/fresh marker. Technical details go to logs on failure.

### 10. Validation can be mostly automated with focused domain tests

This repo currently has no test project. Phase 3 adds deterministic business logic, so the plan should add a minimal test project or equivalent automated verification for:

- Ranking order and invalid-row filtering.
- Stop selection respecting `MaxItemsPerSession` and `MaxServersToVisit`.
- Same-DC friction tie-break when stop values are within 20%.
- Cache validity checks: age, config fingerprint, schema version.
- Duplicate scan guard behavior.

If the executor cannot add a test project because of Dalamud SDK/runtime constraints, it must still add domain methods with grep-verifiable code paths and document manual verification steps for `/nflip scan`.

## Recommended Implementation Slices

### Plan 03-01: ScanEngine and ranked opportunities

Build domain models and `ScanEngine` around `SaddlebagClient.ScanAsync`. This plan covers SCAN-01 and establishes success/empty/error result contracts. It should include response-shape verification/adaptation and deterministic ranking.

### Plan 03-02: RouteOptimizer, cache, and runtime orchestration

Build world/DC data, route stops, optimizer, cache store, `/nflip scan`, login auto-scan, latest-route state, and duplicate scan guard. This plan covers SCAN-02, SCAN-03, and SCAN-04.

## Validation Architecture

| Validation Area | Automated Check | Manual Check |
|-----------------|-----------------|--------------|
| Ranking | Unit tests or CLI/build checks prove `ExpectedDailyProfit`, `SalesPerDay`, `CheapestPrice` order | Inspect generated route summary from `/nflip scan` |
| Route grouping | Tests prove grouping by `CheapestServer` and vendor source | Log summary shows stop count and item count |
| Route ordering | Tests prove value-first and 20% same-DC tie-break | Manual route sample uses high-value stop first |
| Cache | Tests prove age, fingerprint, schema version, and manual rescan invalidation | Run `/nflip scan` twice; second auto path uses cache, manual path bypasses |
| Login scan | Build plus code inspection for `IClientState.Login`, startup `IsLoggedIn`, cancellation in `Dispose` | Login with configured home world and verify concise scan logs |

## Risks

1. `ScanResponse.Items` may not match the live API response key. Mitigation: make 03-01 verify and adapt response deserialization before ranking is considered complete.
2. Dalamud chat output may require `IChatGui` rather than only `IPluginLog` for visible user feedback. Mitigation: Phase 3 can use logs for pre-UI testing, while Phase 4 owns visible UI rendering.
3. Hardcoded world/DC data can drift with future FFXIV worlds. Mitigation: centralize in one `WorldData` file with all current worlds and comments.
4. Cache serialization can accidentally pull mutable raw API objects into UI/session state. Mitigation: cache envelope separates raw response from derived route and uses schema versioning.

## RESEARCH COMPLETE

Phase 3 is ready to plan as two executable plans matching the roadmap split.
