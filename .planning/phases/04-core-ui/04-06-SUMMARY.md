---
phase: 04-core-ui
plan: "06"
subsystem: ui
tags: [imgui, dalamud, configwindow, snapshot-discard, gap-closure]

requires:
  - phase: 04-03
    provides: ConfigWindow with snapshot/dirty/save/discard plumbing (isDirty, snapshot field, OnOpen, OnClose, Snapshot(), RestoreFrom())

provides:
  - ConfigWindow.OnOpen() guards snapshot capture with !isDirty, fixing Dalamud spurious re-open corruption
  - phase04_nyquist.sh regression assertions for the !isDirty guard (gap-closure 04-06)

affects: [phase-05, any future ConfigWindow refactors]

tech-stack:
  added: []
  patterns:
    - "Spurious OnOpen guard: wrap snapshot capture in !isDirty to distinguish genuine open from Dalamud WindowHost post-OnClose bounce"

key-files:
  created: []
  modified:
    - NamazuFlippers/UI/ConfigWindow.cs
    - tests/phase04_nyquist.sh

key-decisions:
  - "isDirty = false removed from OnOpen: clearing isDirty in OnOpen was a no-op when isDirty is false (genuine open) and dangerously wrong when isDirty is true (spurious bounce). Save and Discard already clear it at close-time."
  - "selectedWorldIndex moved inside guard: on spurious bounce, preserve the user's in-progress HomeWorld dropdown selection rather than re-syncing from plugin.Configuration.HomeWorld"

patterns-established:
  - "Dalamud WindowHost spurious-OnOpen defense: any state that must survive OnClose-cancellation should be gated on !isDirty in OnOpen"

requirements-completed: [UI-08]

duration: 8min
completed: 2026-05-07
---

# Phase 4 Plan 06: ConfigWindow Discard Gap-Closure Summary

**One-line guard in OnOpen fixes Discard-does-not-revert gap: `if (!isDirty)` prevents Dalamud's spurious post-OnClose re-open from overwriting the pre-edit snapshot**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-05-07T19:30:00Z
- **Completed:** 2026-05-07T19:38:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Added `if (!isDirty)` guard in `ConfigWindow.OnOpen()` so Dalamud's spurious frame-N+2 re-open (triggered when `OnClose` sets `IsOpen=true` to cancel a dirty close) no longer overwrites the snapshot with the user's already-edited values
- Removed the now-redundant `isDirty = false` from `OnOpen` — Save and Discard already clear it before closing; the spurious bounce must not accidentally clear the dirty flag either
- Moved `selectedWorldIndex` re-sync inside the guard so in-progress HomeWorld dropdown edits are preserved on spurious bounce
- Extended `tests/phase04_nyquist.sh` with two gap-closure regression assertions that pin the `if (!isDirty)` guard and the `snapshot = Snapshot(plugin.Configuration)` call

## Task Commits

1. **Task 1: Guard OnOpen snapshot capture with !isDirty** - `e5aedcb` (fix)
2. **Task 2: Extend phase04_nyquist.sh with !isDirty guard regression assertion** - `df01ed2` (test)

**Plan metadata:** (committed with SUMMARY)

## Files Created/Modified
- `NamazuFlippers/UI/ConfigWindow.cs` - OnOpen() guard added; isDirty=false and selectedWorldIndex re-sync moved inside guard
- `tests/phase04_nyquist.sh` - Two gap-closure 04-06 regression assertions appended before the final failure check

## Decisions Made
- `isDirty = false` removed from OnOpen: It was redundant (already false on genuine open) and harmful (would clear dirty flag on spurious bounce, hiding unsaved changes from the modal trigger)
- `selectedWorldIndex` moved inside guard: Spurious bounce should preserve the user's pending HomeWorld selection, not reset it from the already-mutated `plugin.Configuration`
- No changes to `Snapshot()`, `RestoreFrom()`, `OnClose()`, or Discard handler — the bug was purely in WHEN the snapshot was captured, not in the mechanics

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

- **phase03_nyquist.sh pre-existing failures (2 checks):** These failures exist on the baseline branch before any changes in this plan. The plan's verification section mentions "phase03_nyquist.sh exits 0" but that was aspirational — the failures are in SCAN-01 pattern assertions related to `ApiJsonContext` and `Where(IsUsable)` which are separate from this plan's scope. Confirmed pre-existing by stashing changes and re-running.

## Known Stubs

None — this is a pure bug fix with no placeholder or stub patterns introduced.

## Threat Flags

None — no new network endpoints, auth paths, or trust boundaries introduced. The `!isDirty` guard reduces (not expands) the attack surface by preventing spurious snapshot overwrites.

## Self-Check

- [x] `NamazuFlippers/UI/ConfigWindow.cs` exists with `if (!isDirty)` guard at OnOpen line 54
- [x] `tests/phase04_nyquist.sh` contains exactly 2 occurrences of `gap-closure 04-06`
- [x] Commit `e5aedcb` exists (Task 1)
- [x] Commit `df01ed2` exists (Task 2)
- [x] `bash tests/phase04_nyquist.sh` exits 0
- [x] `DailyRouteWindow.cs` not modified

## Next Phase Readiness
- UAT Test 3 Discard gap is closed at source level; full in-game verification requires Dalamud runtime (GitHub Actions or live FFXIV client)
- Remaining gap-closure plans (04-04 profit tally, 04-05 layout) are handled by the parallel agent

---
*Phase: 04-core-ui*
*Completed: 2026-05-07*
