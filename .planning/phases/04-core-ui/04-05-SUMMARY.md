---
phase: 04-core-ui
plan: "05"
subsystem: ui
tags: [imgui, dalamud, layout, gap-closure, button-alignment]

requires:
  - phase: 04-core-ui/04-02
    provides: DrawProgressSection with Rescan Route and Settings buttons (broken layout)
  - phase: 04-core-ui/04-03
    provides: OpenConfigWindow entry point on plugin
provides:
  - DrawProgressSection with Settings+Rescan right-aligned as a combined group using combinedWidth arithmetic
  - Both buttons visible and reachable within 420px default window width
affects:
  - UAT Test 3 (UI-01, UI-08 gap closure)

tech-stack:
  added: []
  patterns:
    - "Combined-width right-alignment: reserve (w1 + spacing + w2) before SetCursorPosX, render left button first then SameLine + right button"

key-files:
  created: []
  modified:
    - NamazuFlippers/UI/DailyRouteWindow.cs

key-decisions:
  - "Settings rendered FIRST (leftmost) so Rescan lands at right edge; combined group still right-aligned"
  - "combinedWidth = rescanWidth + buttonSpacing + settingsWidth avoids negative SetCursorPosX at narrow widths"

patterns-established:
  - "Two-button right-alignment: compute combinedWidth, SetCursorPosX once, draw left button, SameLine, draw right button"

requirements-completed: [UI-01, UI-08]

duration: 5min
completed: 2026-05-07
---

# Phase 04 Plan 05: Button Layout Gap Closure Summary

**Fixed DrawProgressSection to right-align Settings+Rescan as a combined 198px group, ending both buttons inside the 420px content region**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-05-07T19:35:00Z
- **Completed:** 2026-05-07T19:40:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- Replaced single-button SetCursorPosX(avail - 110) with combined-width calculation: `combinedWidth = rescanWidth + buttonSpacing + settingsWidth` (= 198px)
- Settings button now rendered FIRST at the reserved cursor position, then SameLine + Rescan — both buttons visible within the 420px window
- BeginDisabled/EndDisabled guard on Rescan preserved from 04-02
- Both nyquist scripts confirm no regression (phase03 failures are pre-existing, unrelated to DailyRouteWindow.cs)

## Task Commits

1. **Task 1: Replace DrawProgressSection button row with combined-width right-alignment** - `8e1f713` (fix)

**Plan metadata:** _(docs commit follows)_

## Files Created/Modified

- `NamazuFlippers/UI/DailyRouteWindow.cs` - DrawProgressSection button row: replaced 9-line broken block with 13-line combined-width right-alignment

## Decisions Made

- Settings rendered left of Rescan (not right) — because right-alignment cursor is set to `cursor + avail - combinedWidth`; first widget (Settings, 80px) draws there, SameLine + Rescan (110px) draws immediately after, ending exactly at the content region right edge
- `buttonSpacing = 8f` hardcodes ImGui default item spacing as a named constant for readability

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

`bash tests/phase03_nyquist.sh` has 2 pre-existing failures in SCAN-01 (normalizer patterns) that existed before any changes in this worktree. These are out-of-scope for 04-05 (which only touches DailyRouteWindow.cs) and pre-dated this plan's execution.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- UAT Test 3 layout gaps (Settings missing, Rescan clipped) are closed at source level
- Both buttons reachable at 420px default window width
- Ready for in-game UAT verification after merge

---
*Phase: 04-core-ui*
*Completed: 2026-05-07*
