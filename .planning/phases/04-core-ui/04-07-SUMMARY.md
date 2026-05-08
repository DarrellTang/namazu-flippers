---
phase: 04-core-ui
plan: 07
subsystem: ui
tags: [imgui, dalamud, layout, alignment, ui-scale, gap-closure, nyquist]

requires:
  - phase: 04-core-ui
    provides: "DailyRouteWindow Settings+Rescan layout (04-05) and Listed checkbox + listedState wiring (04-04) — this plan corrects the regressions in those layouts that surfaced on build 1.0.26.0 UAT"
provides:
  - "DrawProgressSection: scale-aware buttonSpacing sourced from ImGui.GetStyle().ItemSpacing.X (closes GAP-D1, fixes Rescan clip at FFXIV UI scale > 1.0)"
  - "DrawItems: absolute-X anchor (ImGui.SameLine(listedAnchorX)) anchors the ##listed- checkbox to a fixed column right-aligned inside the row (closes GAP-D2)"
  - "phase04_nyquist.sh: 4 new regression assertions (3 for GAP-D1, 1 for GAP-D2) so a future refactor cannot reintroduce either bug"
affects: [05-session-store, future ui scale changes, phase04 UAT re-verification]

tech-stack:
  added: []
  patterns:
    - "Source spacing from ImGui.GetStyle().ItemSpacing.X each frame instead of hardcoding the 8px default — keeps reservation arithmetic in sync with Dalamud's UI-scale-driven actual gap"
    - "Anchor right-aligned columns with ImGui.SameLine(absoluteX) computed from GetWindowContentRegionMax().X minus a column-width budget; fall back to bare SameLine() when row width is too narrow"
    - "Nyquist regression assertions co-located with their source feature — every behavioral fix grows a grep-able guard in tests/phase04_nyquist.sh before the plan closes"

key-files:
  created: []
  modified:
    - "NamazuFlippers/UI/DailyRouteWindow.cs — DrawProgressSection runtime ItemSpacing read; DrawItems listed-checkbox absolute-X anchor"
    - "tests/phase04_nyquist.sh — 4 new GAP-D1/GAP-D2 regression assertions appended after the 04-06 OnOpen-guard block"

key-decisions:
  - "Use ImGui.GetStyle().ItemSpacing.X each frame instead of caching it once: the value is cheap to read, ImGui already reads it for the matching SameLine(), and binding the reservation to the same source as the actual gap eliminates the entire class of scale-driven layout drift bugs."
  - "Anchor the listed-checkbox column with ImGui.SameLine(absoluteX) not SetCursorPosX: SameLine(arg) is the documented ImGui idiom for 'place the next widget at this absolute X on the same logical line', it preserves the SameLine semantics already used between elements, and the bare-SameLine fallback handles the too-narrow-row degenerate case gracefully."
  - "Pin listedColumnWidth = 150f: the trailing column needs (~22 px checkbox + ~8 px scaled spacing + ~120 px worst-case 'List: 9,999,999' label) ≈ 150 px; smaller would clip the price label, larger would shrink the headroom available to long item names."

patterns-established:
  - "Scale-aware spacing pattern: when reserving width that includes an ImGui.SameLine() gap, source the gap from ImGui.GetStyle().ItemSpacing.X — never a literal."
  - "Right-aligned column pattern: compute anchorX = GetWindowContentRegionMax().X - columnWidth each frame, then ImGui.SameLine(anchorX) with a bare-SameLine fallback when anchorX <= cursorX."
  - "Gap-closure feedback bundling: when a single UAT round flags multiple gaps in the same file with the same retest, bundle the fixes into one plan with two atomic commits, not per-gap atomic plans (per the user's vault note feedback_bundling.md)."

requirements-completed: [UI-01, UI-03, UI-04, UI-08]

duration: 2 min
completed: 2026-05-08
---

# Phase 04 Plan 07: Rescan-clip + Listed-alignment gap closure Summary

**Closes GAP-D1 (Rescan button clipped at FFXIV UI scale > 1.0) by sourcing buttonSpacing from runtime ImGui.GetStyle().ItemSpacing.X, and GAP-D2 (Listed checkbox column drift) by anchoring the ##listed- Checkbox to ImGui.SameLine(contentMaxX - 150f); guards both with 4 new nyquist regression assertions.**

## Performance

- **Duration:** 2 min
- **Started:** 2026-05-08T05:22:00Z
- **Completed:** 2026-05-08T05:24:01Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- **GAP-D1 closed (major):** `DrawProgressSection` now sources `buttonSpacing` from `ImGui.GetStyle().ItemSpacing.X` at runtime instead of `const float buttonSpacing = 8f`. At FFXIV UI scale 1.0 behavior is unchanged (8 == 8). At scale 1.5 both the reservation and the actual gap inserted by `ImGui.SameLine()` between Settings and Rescan become 12, so Rescan's right edge lands exactly at content_right instead of overflowing by `8 * (scale - 1)`. Closes the truth in `must_haves.truths` about Rescan being fully visible at all UI scales (not just 1.0) and the truth about `buttonSpacing` being sourced from runtime style.
- **GAP-D2 closed (cosmetic):** `DrawItems` now anchors the `##listed-{itemId}` Checkbox to a fixed X column via `ImGui.SameLine(listedAnchorX)` where `listedAnchorX = GetWindowContentRegionMax().X - 150f`. Every row places the checkbox at the same X regardless of preceding widget widths (item name, [OOS]/[Vendor] badges, prices). Bare-SameLine fallback on too-narrow rows prevents the checkbox from jumping LEFT into prior text. Closes the truth about absolute-X anchoring and the truth about a consistent X column.
- **4 new regression assertions** added to `tests/phase04_nyquist.sh` so any future refactor that reintroduces either bug fails source validation before it can reach UAT or CI:
  1. `DrawProgressSection reads buttonSpacing from runtime ImGui.GetStyle().ItemSpacing.X (GAP-D1, 04-07)` — `require_pattern` for the `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X` literal.
  2. `Hardcoded const float buttonSpacing = 8f is gone (GAP-D1, 04-07)` — `require_absent_pattern` for the old constant.
  3. `combinedWidth still composed from rescanWidth + buttonSpacing + settingsWidth (GAP-D1, 04-07)` — defense-in-depth `require_pattern` confirming the arithmetic still references the runtime value.
  4. `absolute-X anchor (SameLine(arg) or SetCursorPosX) precedes ##listed- Checkbox in DrawItems (GAP-D2, 04-07)` — custom `listed_anchor_check` awk helper that scans the 30 source lines preceding the first `##listed-` match and asserts at least one anchor pattern (`SameLine(<arg>)` or `SetCursorPosX`) appears.
- **No behavioral fixes from earlier plans regressed** — see "Behavioral Preservation" below.

## Task Commits

Each task was committed atomically (single-line messages per the user's global git conventions):

1. **Task 1: Apply both layout fixes to DailyRouteWindow.cs** — `6607f56` (fix)
   `fix(04-07): scale-aware buttonSpacing + listed checkbox column anchor`
2. **Task 2: Extend phase04_nyquist.sh with regression assertions** — `2424bff` (test)
   `test(04-07): nyquist regressions for GAP-D1 and GAP-D2`

**Plan metadata:** committed below as a docs commit including this SUMMARY.md.

## Files Created/Modified

- `NamazuFlippers/UI/DailyRouteWindow.cs` — Two surgical edits: (1) `DrawProgressSection` `const float buttonSpacing = 8f` → `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X` plus explanatory comment citing 04-REVIEW.md WR-02 and the debug doc; (2) `DrawItems` bare `ImGui.SameLine()` before the listed-checkbox replaced with `ImGui.SameLine(listedAnchorX)` (with a bare-SameLine `else` fallback) computed from `GetWindowContentRegionMax().X - 150f`. Net diff: `+22 / -2` lines, single file.
- `tests/phase04_nyquist.sh` — Appended one new section block (3 `require_pattern`/`require_absent_pattern` calls + 1 awk helper function `listed_anchor_check`) immediately after the existing 04-06 OnOpen-guard block and before the `if [[ "$failures" -ne 0 ]]; then ... fi` failure summary footer. Net diff: `+59 / -0` lines, no existing assertion modified.

## Byte-for-byte Unchanged (constraint compliance)

Confirmed via `git diff HEAD~2 HEAD -- ...` returning empty for every constrained path:

- `NamazuFlippers/Core/RouteStop.cs` — unchanged
- `NamazuFlippers/Core/RouteOptimizer.cs` — unchanged
- `NamazuFlippers/Core/ScanEngine.cs` — unchanged
- `NamazuFlippers/NamazuFlippers.cs` — unchanged
- `NamazuFlippers/UI/ConfigWindow.cs` — unchanged
- `NamazuFlippers/UI/FirstRunWindow.cs` — unchanged
- `tests/phase03_nyquist.sh` — unchanged

`bash tests/phase03_nyquist.sh` exit code is **1**, the documented baseline (2 pre-existing Phase 3 failures noted in 04-04-SUMMARY and 04-05-SUMMARY). Failure count unchanged: this plan introduced no new Phase 3 regressions.

## Behavioral Preservation (cite the verification greps)

All behavioral preservation greps from `<verification>` match against the post-Task-1 source — confirming the fixes from 04-02 / 04-04 / 04-05 / 04-06 are preserved verbatim:

| Grep | Expected | Actual | Provenance |
|------|----------|--------|------------|
| `plugin\.OpenConfigWindow` | 1 | 1 | 04-05 (Settings entry point) |
| `plugin\.RescanAsync` | 1 | 1 | 04-05 (Rescan click handler) |
| `BeginDisabled` | ≥1 | 1 | 04-02 (ScanInProgress disabled-state guard) |
| `EndDisabled` | ≥1 | 1 | 04-02 (ScanInProgress disabled-state guard) |
| `listedState\[item\.ItemId\]\s*=\s*listed` | 1 | 1 | 04-04 (Listed checkbox dictionary write) |
| `##bought-` | 1 | 1 | 04-02 (Bought checkbox imgui ID) |
| `##listed-` | 1 | 1 | 04-04 (Listed checkbox imgui ID) |
| `if \(!isDirty\)` in ConfigWindow.cs | present | present | 04-06 (Discard guard) |

All pre-existing nyquist assertion blocks (UI-01..UI-08, CONF-01..CONF-09, color tokens, FirstRunWindow migration, lambda safety, gap-closure 04-04 / 04-06) print `ok` after Task 2 — verified by reading the full output of `bash tests/phase04_nyquist.sh`. Counts: `gap-closure 04-04` matches 4 lines (≥4 baseline), `gap-closure 04-06` matches 2 lines (≥2 baseline), `GAP-D1, 04-07` matches 3 lines, `GAP-D2, 04-07` matches 1 line.

## Decisions Made

- **Runtime style read, not cached:** `ImGui.GetStyle().ItemSpacing.X` is read fresh each frame in `DrawProgressSection`. Caching it once at construction would defeat the purpose because Dalamud's UI scale can change live (e.g., user opens System Configuration mid-session). The cost is one struct field read per frame; correctness over micro-optimization.
- **`ImGui.SameLine(absoluteX)` for column anchor, not `ImGui.SetCursorPosX`:** Both work, but `SameLine(arg)` keeps the layout idiom consistent with the surrounding `ImGui.SameLine()` calls between widgets in `DrawItems`. The nyquist assertion accepts either pattern (`SameLine\([^)[:space:]]` OR `SetCursorPosX`) so a future refactor can switch without breaking the gate.
- **Bare-SameLine fallback for too-narrow rows:** `if (listedAnchorX > rowCursorPosX) ImGui.SameLine(listedAnchorX); else ImGui.SameLine();` — guarantees the cursor never jumps backward into prior text on a row whose accumulated width already exceeds the anchor. Defends the T-04-07-01 DoS-class threat from the threat model.
- **Original second-helper assertion dropped after checker review:** The plan originally proposed a `sameline_before_listed_bare_check` helper as a defense-in-depth assertion, but its `line[NR-1]` lookback would have evaluated `var listed = listedState.GetValueOrDefault(...)` in BOTH the buggy and fixed source — meaning it would always pass and contribute no regression-detection value. `listed_anchor_check` (which scans 30 lines before `##listed-` for an explicit anchor) is the sole and sufficient GAP-D2 regression assertion: reverting Task 1's anchor change removes the anchor pattern from the 30-line window and `listed_anchor_check` fails. One assertion, but it correctly catches the regression.

## Deviations from Plan

None — plan executed exactly as written. The plan was extremely tight (2 atomic auto tasks, full code snippets pre-written, every assertion specified verbatim) so no Rule 1/2/3 auto-fixes were needed and no Rule 4 architectural decisions arose.

**Total deviations:** 0
**Impact on plan:** No scope creep. Both commits match the plan's `<files_modified>` list exactly. Exactly two files in `git diff --name-only HEAD~2 HEAD`: `NamazuFlippers/UI/DailyRouteWindow.cs` and `tests/phase04_nyquist.sh`.

## Issues Encountered

None.

## Authentication Gates

None — no external services touched in this plan.

## Build Verification Model

**Local validation is source-level only.** Per `.planning/PROJECT.md` and `.planning/STATE.md`:
- macOS `dotnet build NamazuFlippers/NamazuFlippers.csproj` is **expected to fail** without Dalamud SDK assemblies installed locally. Did NOT attempt it.
- `bash tests/phase04_nyquist.sh` is the local source-validation gate — exits 0 after both tasks (all pre-existing assertions plus all 4 new GAP-D1/GAP-D2 assertions pass).
- `bash tests/phase03_nyquist.sh` baseline preserved (exit 1, 2 pre-existing failures unchanged).
- **GitHub Actions is the authoritative compile/package gate.** The post-merge CI run will compile the plugin against the Dalamud SDK and produce the next packaged build (>1.0.26.0); only that build is the ship-level verification surface for in-game UAT Test 1 (Listed checkbox column alignment) and Test 2 (Rescan visible at the user's actual FFXIV UI scale).

## WR-02 Lesson (referenced in `<output>`)

Code-review WR-02 in `.planning/phases/04-core-ui/04-REVIEW.md` named the GAP-D1 mechanism **before UAT**: it pointed out that hardcoding `buttonSpacing = 8f` while the matching `ImGui.SameLine()` reads the runtime style would cause Rescan to overflow at non-1.0 UI scales. The verifier dismissed it as cosmetic on the assumption that "8 px is close enough to most UI-scale-adjusted defaults to be invisible." The 1.0.26.0 UAT result invalidated that assumption: the user runs FFXIV at a UI scale > 1.0 (likely 1.5 — common for high-DPI monitors), at which scale the gap arithmetic is off by `8 * (scale - 1)` ≥ 4 px and Rescan visibly clips.

**Lesson:** Do not dismiss code-review WR (warning/recommendation) findings as cosmetic without explicit non-1.0-UI-scale verification. Layout math that mixes hardcoded constants with runtime style reads is a structural correctness issue, not a polish issue, and it manifests cleanly only when the runtime style read is exercised — i.e., when the user's UI scale differs from the default. Future reviews should treat any "default value of an ImGui style" hardcoded against a matching runtime-style call as WR-level severity by default and require a non-1.0-scale UAT or a parametric test to dismiss.

## Next Phase Readiness

- Phase 04 core-ui is now ready for UAT re-verification on the post-merge CI build (>1.0.26.0). UAT Test 1 (Listed checkbox column alignment) and Test 2 (Rescan visible) should both close at the in-game gate.
- No blockers for Phase 05 (session-store). The behavioral wiring this phase preserves (boughtState / listedState dictionaries, profit tally LINQ, OnOpen `!isDirty` snapshot guard) is the surface Phase 05 will persist to JSON; nothing in 04-07 changes that contract.
- Threat model items T-04-07-01 (cursor at narrow widths) and T-04-07-02 (extreme UI scales) are mitigated by the bare-SameLine fallback and the existing `if (avail > combinedWidth)` guard respectively. T-04-07-03/04/05 are accepted (single-user, in-process, advisory CI gate) consistent with prior phase posture.

## Threat Flags

None — this plan introduced no new network endpoints, no new auth paths, no new file/IO surface, no schema changes. Layout-math edits and source-validation assertions only. The threat model in the plan covers the full surface; no flags raised.

## Self-Check: PASSED

Verification re-run after writing this SUMMARY:

- File existence:
  - `[ -f .planning/phases/04-core-ui/04-07-SUMMARY.md ]` — FOUND (this file)
  - `[ -f NamazuFlippers/UI/DailyRouteWindow.cs ]` — FOUND
  - `[ -f tests/phase04_nyquist.sh ]` — FOUND
- Commit existence:
  - `git log --oneline | grep 6607f56` — FOUND (`fix(04-07): scale-aware buttonSpacing + listed checkbox column anchor`)
  - `git log --oneline | grep 2424bff` — FOUND (`test(04-07): nyquist regressions for GAP-D1 and GAP-D2`)
- Acceptance criteria re-run:
  - Task 1: all 9 acceptance criteria PASS (see "Behavioral Preservation" table above + grep checks logged in execution).
  - Task 2: all 8 acceptance criteria PASS (grep counts: GAP-D1=3, GAP-D2=1, gap-closure 04-04=4, gap-closure 04-06=2, helpers each defined once, listed_anchor_check defined and invoked once, bash -n exits 0).
- Plan-level verification re-run:
  - `bash tests/phase04_nyquist.sh` → exit 0 ✓
  - `bash tests/phase03_nyquist.sh` → exit 1 (baseline preserved, 2 pre-existing failures unchanged) ✓
  - `git diff --name-only HEAD~2 HEAD` → exactly 2 files: `NamazuFlippers/UI/DailyRouteWindow.cs` and `tests/phase04_nyquist.sh` ✓
  - Phase 3 source + `phase03_nyquist.sh` byte-unchanged ✓
  - All 7 behavioral-preservation greps match expected counts ✓

---
*Phase: 04-core-ui*
*Completed: 2026-05-08*
