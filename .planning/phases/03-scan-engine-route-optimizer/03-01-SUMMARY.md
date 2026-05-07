---
phase: 03-scan-engine-route-optimizer
plan: 03-01
subsystem: scan-engine
tags: [scan, ranking, api, errors]
requires: [API-01, API-03]
provides: [SCAN-01]
affects:
  - NamazuFlippers/API/SaddlebagClient.cs
  - NamazuFlippers/Core/ScanEngine.cs
tech-stack:
  added: []
  patterns: [structured-results, deterministic-ranking]
key-files:
  created:
    - NamazuFlippers/Core/RankedOpportunity.cs
    - NamazuFlippers/Core/ScanEngineResult.cs
    - NamazuFlippers/Core/ScanEngine.cs
  modified:
    - NamazuFlippers/API/Models/ScanResponse.cs
    - NamazuFlippers/API/Models/ApiJsonContext.cs
    - NamazuFlippers/API/SaddlebagClient.cs
key-decisions:
  - Normalize scan response shapes at the API boundary while preserving ScanResponse.Items.
  - Rank locally by ExpectedDailyProfit, SalesPerDay, then CheapestPrice.
requirements-completed: [SCAN-01]
duration: "18 min"
completed: 2026-05-07
---

# Phase 03 Plan 01: Scan Engine Business Layer Summary

Fresh scan ranking now flows through a dedicated `ScanEngine` that calls `SaddlebagClient.ScanAsync`, filters invalid rows, ranks usable opportunities deterministically, caps the result to the configured session item count, and returns structured success, empty, and error outcomes.

## Execution

Started: 2026-05-07T02:28:00Z
Completed: 2026-05-07T02:46:07Z
Tasks: 3
Files changed: 6

## Commits

| Commit | Task | Description |
|--------|------|-------------|
| 9daeb4d | Task 1 | Hardened scan response deserialization with a normalizer for root arrays and common object wrappers. |
| d444653 | Task 2 | Added UI-neutral ranked opportunity and structured scan result models. |
| 914749e | Task 3 | Implemented fresh scan filtering, deterministic ranking, capping, vendor metadata, and structured error handling. |

## What Changed

- `SaddlebagClient` now normalizes uncertain `/api/scan` response shapes into `ScanResponse.Items` instead of depending on a single guessed wrapper key.
- `RankedOpportunity` preserves purchase source, vendor source, out-of-stock metadata, price, velocity, and expected daily profit.
- `ScanEngineResult` exposes `Success`, `Empty`, `Error`, `UsingCache`, and `UsingStaleCache` states for later route/cache/UI work.
- `ScanEngine.ScanFreshAsync` filters invalid rows, sorts by profit/velocity/price, caps to `MaxItemsPerSession`, and converts API exceptions into friendly user messages with technical details preserved.

## Verification

Grep acceptance checks passed for:

- `List<ScanItem> Items`
- scan response normalization
- `RankedOpportunity`
- `ScanEngineStatus`
- `ScanFreshAsync`
- `ScanAsync`
- `ExpectedDailyProfit`, `SalesPerDay`, `CheapestPrice`
- `MaxItemsPerSession`
- `ApiException`

`dotnet build NamazuFlippers/NamazuFlippers.csproj` was attempted but could not complete in this local environment because the Dalamud assemblies are not available under `DALAMUD_HOME`; the failure begins with missing `Dalamud` references.

## Deviations from Plan

The live API response shape could not be verified from this local environment. Instead, the boundary was hardened with a normalizer that accepts root arrays plus `items`, `results`, or `data` object wrappers while keeping downstream `ScanResponse.Items` stable.

**Total deviations:** 1 environment-limited adaptation.
**Impact:** Lower runtime risk than the original single-key assumption; final live validation still belongs in Dalamud/API testing.

## Self-Check: PASSED

All non-build acceptance gates available in this workspace passed. Build remains blocked by missing local Dalamud dependencies, not by a code-specific compiler diagnostic.

## Next Phase Readiness

Ready for Plan 03-02 to add route optimization, cache storage, manual scan command handling, and login auto-scan on top of the ranked opportunities.
