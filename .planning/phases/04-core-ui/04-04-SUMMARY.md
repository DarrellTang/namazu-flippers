---
phase: 04-core-ui
plan: "04"
subsystem: ui
tags: [imgui, dalamud, gap-closure, profit-tally, listed-checkbox]

requires:
  - phase: 04-01
    provides: DailyRouteWindow scaffolding with DrawRouteStop/DrawItems structure
  - phase: 04-02
    provides: listedState dictionary, profit tally LINQ, bought/listed checkbox wiring

provides:
  - Listed checkbox rendered on every item row in every RouteStop (isHomeStop gate removed)
  - Profit tally (listedProfit) now accumulates correctly as user marks items listed
  - Regression assertions in phase04_nyquist.sh guarding against isHomeStop reintroduction

affects: [04-05, 04-06, phase-05]

tech-stack:
  added: []
  patterns:
    - "Gap-closure pattern: remove structurally-impossible gate rather than synthesize workaround data"
    - "Regression guard pattern: require_absent_pattern in nyquist.sh to prevent re-introduction of deleted code paths"

key-files:
  created: []
  modified:
    - NamazuFlippers/UI/DailyRouteWindow.cs
    - tests/phase04_nyquist.sh

key-decisions:
  - "D-14: Listed checkbox rendered inline on every item row (option b) rather than synthesizing a home stop in RouteOptimizer (option a) — keeps fix UI-only, no Phase 3 source churn"

patterns-established:
  - "require_absent_pattern: used in nyquist.sh to assert deleted code patterns stay deleted"

requirements-completed: [UI-03, UI-04]

duration: 2min
completed: 2026-05-08
---

# Phase 04 Plan 04: Gap Closure — Profit Tally Zero Bug Summary

**Removed structurally-impossible isHomeStop gate from DrawItems so the listed checkbox renders on every item row and the profit tally updates correctly**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-05-08T01:34:29Z
- **Completed:** 2026-05-08T01:36:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Deleted `isHomeStop` string-compare in `DrawRouteStop` — `PurchaseSource` is always the cheap server, never `HomeWorld`, so the gate was permanently false
- Listed checkbox (`##listed-{itemId}`) now renders on every item row in every `RouteStop`, populating `listedState` so the LINQ profit tally at lines 115-117 produces non-zero output
- Added 4 regression assertions to `phase04_nyquist.sh` guarding the gap-closure invariants

## Task Commits

1. **Task 1: Remove isHomeStop gate, render listed checkbox on every item row** - `0cc1c03` (fix)
2. **Task 2: Add gap-closure regression assertions to phase04_nyquist.sh** - `a90534c` (test)

## Files Created/Modified

- `NamazuFlippers/UI/DailyRouteWindow.cs` - Removed `isHomeStop` boolean, dropped `bool isHomeStop` parameter from `DrawItems`, ungated listed-checkbox + List-price column
- `tests/phase04_nyquist.sh` - Added 4 `require_absent_pattern` / `require_pattern` assertions for gap-closure 04-04

## Decisions Made

- **D-14: Option (b) — UI-only fix.** Rendered listed checkbox inline on every item row rather than synthesizing a home/listing stop in `RouteOptimizer`. Keeps Phase 3 source files untouched; semantics are correct (listing happens after buying, independent of which server the item was purchased from).

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

Phase 3 nyquist (`tests/phase03_nyquist.sh`) has 2 pre-existing failures unrelated to this plan (normalizer wrapper shapes and `Where(IsUsable)` filter). These failures existed before this plan ran and are unchanged — verified by running the same script against the main repo. No regression introduced.

## Known Stubs

None — this plan removes a gate; no placeholder data or stub behavior introduced.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes introduced. The listed-checkbox and listedState dictionary were already present; only the rendering gate was removed.

## Next Phase Readiness

- UAT Test 1 gap (profit shows zero) is closed at the source level
- Wave 2 plans (04-05, 04-06) can proceed
- Phase 3 source files (RouteStop.cs, RouteOptimizer.cs) are byte-for-byte unchanged

---
*Phase: 04-core-ui*
*Completed: 2026-05-08*
