---
phase: 03-scan-engine-route-optimizer
plan: 03-02
subsystem: route-optimizer-cache-runtime
tags: [route, cache, command, login]
requires: [SCAN-01]
provides: [SCAN-02, SCAN-03, SCAN-04]
affects:
  - NamazuFlippers/Core/RouteOptimizer.cs
  - NamazuFlippers/Data/ScanCacheStore.cs
  - NamazuFlippers/NamazuFlippers.cs
tech-stack:
  added: []
  patterns: [file-cache, route-grouping, duplicate-scan-guard]
key-files:
  created:
    - NamazuFlippers/Core/RouteStop.cs
    - NamazuFlippers/Core/RouteOptimizer.cs
    - NamazuFlippers/Data/WorldData.cs
    - NamazuFlippers/Data/ScanCacheEnvelope.cs
    - NamazuFlippers/Data/ScanCacheStore.cs
  modified:
    - NamazuFlippers/API/Models/ApiJsonContext.cs
    - NamazuFlippers/Core/ScanEngine.cs
    - NamazuFlippers/Core/ScanEngineResult.cs
    - NamazuFlippers/FirstRunWindow.cs
    - NamazuFlippers/NamazuFlippers.cs
key-decisions:
  - Centralize world and data-center knowledge in WorldData.
  - Keep route value primary and use travel friction only for close-value stops.
  - Cache raw scan response plus derived route under the Dalamud plugin config directory.
  - Keep bare /nflip as the UI toggle and use /nflip scan for forced refresh.
requirements-completed: [SCAN-02, SCAN-03, SCAN-04]
duration: "9 min"
completed: 2026-05-07
---

# Phase 03 Plan 02: Route, Cache, and Runtime Scan Wiring Summary

Phase 3 now has route stop optimization, file-backed scan cache, manual scan refresh, login/startup auto-scan, latest result state, and duplicate scan protection wired into the plugin entry point.

## Execution

Started: 2026-05-07T02:46:10Z
Completed: 2026-05-07T02:55:01Z
Tasks: 5
Files changed: 10

## Commits

| Commit | Task | Description |
|--------|------|-------------|
| 598c556 | Task 1 | Added `WorldData` with known worlds, data-center lookup, and travel friction; first-run window now uses the shared list. |
| 924d4b2 | Task 2 | Added `RouteStop`, `RouteOptimizer`, route totals, and route stop list support. |
| fd48dd2 | Task 3 | Added `ScanCacheEnvelope`, `ScanCacheStore`, config fingerprinting, expiry/schema validity, and source-gen JSON registrations. |
| a6b110f | Task 4 | Added cache-aware `ScanEngine.GetRouteAsync` with route optimization, cache save, valid-cache reuse, stale-cache fallback, and empty-result replacement. |
| 24349ab | Task 5 | Wired `IClientState`, `/nflip scan`, startup/login auto-scan, latest result state, and duplicate scan guard. |
| 0034f0b | Task 2 follow-up | Switched route stop sorting to explicit `OrderBy(..., comparer)` for compatibility. |

## What Changed

- `WorldData` contains every world from the old first-run picker plus data-center mapping and travel friction helpers.
- `RouteOptimizer` groups opportunities by purchase source, orders stops by value, applies the 20 percent friction tie-break, preserves vendor stops, and trims final items to `MaxItemsPerSession`.
- `ScanCacheStore` writes `scan-cache.json` under `pluginInterface.ConfigDirectory`, validates cache by schema, expiry, and scan-affecting config fingerprint, and avoids storing cache data in `Configuration`.
- `ScanEngine.GetRouteAsync` uses valid cache for non-forced scans, creates route stops after fresh success, saves fresh success/empty results, and uses stale cache only when refresh fails.
- `NamazuFlippers` now preserves bare `/nflip` toggle behavior and adds `/nflip scan` forced refresh, `LatestScanResult`, login/startup auto-scan, cancellation cleanup, and an `Interlocked` duplicate scan guard.

## Verification

Grep acceptance checks passed for:

- `WorldData`, `GetDataCenter`, `GetTravelFriction`, and `WorldData.KnownWorlds`
- `RouteStop`, `RouteOptimizer`, `GroupBy`, `MaxServersToVisit`, `MaxItemsPerSession`, 20 percent tie-break, and `RouteStops`
- `ScanCacheEnvelope`, `ScanCacheStore`, `ConfigDirectory`, `scan-cache.json`, `CreateConfigFingerprint`, `CacheDurationHours`, and source-gen registration
- `GetRouteAsync`, `LoadValidAsync`, `LoadAnyAsync`, `Optimize`, `SaveAsync`, and `UsingStaleCache`
- `IClientState`, login subscribe/unsubscribe, `IsLoggedIn`, `LatestScanResult`, duplicate scan log, forced manual refresh, and cache-reusing auto-scan

`dotnet build NamazuFlippers/NamazuFlippers.csproj` was attempted after each task and remains blocked because this local environment cannot resolve Dalamud assemblies. The first failing diagnostic is missing `Dalamud` references from the SDK root at `/`.

## Deviations from Plan

Build verification could not run to completion locally due missing Dalamud dependency assemblies. No implementation-specific compiler diagnostic was reachable in this environment.

**Total deviations:** 1 environment-limited verification gap.
**Impact:** Deterministic source checks passed; final compiler verification needs a configured Dalamud SDK path or CI environment.

## Self-Check: PASSED

All deterministic acceptance checks available in this workspace passed. Phase 3 runtime behavior that depends on Dalamud login and command services still requires in-game/manual verification.

## Next Phase Readiness

Ready for phase-level verification and later Phase 4 UI work to consume `LatestScanResult`, `RouteStops`, and cache-backed route state.
