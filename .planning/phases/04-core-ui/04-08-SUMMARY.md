---
phase: 04-core-ui
plan: 08
subsystem: ui
tags: [imgui, dalamud, layout, ui-scale, gap-closure, nyquist]

requires:
  - phase: 04-core-ui
    provides: "DrawProgressSection layout from 04-05 + 04-07 (Settings/Rescan right-aligned pair, scale-aware buttonSpacing). 04-08 corrects the upstream-of-spacing bug surfaced by UAT round 2 on build >1.0.26.0."
provides:
  - "DrawProgressSection: Settings + Rescan render on their OWN row (no SameLine after the bought/listed Text), so avail = ImGui.GetContentRegionAvail().X measures the full content region width — closes GAP-E1."
  - "DrawProgressSection: rescanWidth = 110f * ImGuiHelpers.GlobalScale and settingsWidth = 80f * ImGuiHelpers.GlobalScale — button frames grow with FFXIV UI scale so 'Rescan Route' fits inside the frame at scale > 1.0."
  - "phase04_nyquist.sh: 3 new GAP-E1 regression assertions (1 structural awk helper + 2 require_pattern) so any future refactor that re-chains the buttons or strips the GlobalScale multiplier fails source validation before UAT."
affects: [05-session-store, future ui scale changes, phase04 UAT round 3 re-verification]

tech-stack:
  added:
    - "Dalamud.Interface.Utility (using directive added) — exposes ImGuiHelpers.GlobalScale, the FFXIV UI scale factor that DrawProgressSection now multiplies into button-frame widths"
  patterns:
    - "Own-row buttons for full-content-region measurement: when reserving width for a button group on its own visual line, do NOT chain it after a preceding ImGui.Text() via SameLine — render on a fresh row so ImGui.GetContentRegionAvail().X returns the full content-region width, not the partially-consumed remainder."
    - "GlobalScale-multiplied literal-pixel widths: any literal-pixel ImGui Vector2 size that ships text the user must read (button labels, fixed-width frames containing scaled fonts) MUST be multiplied by ImGuiHelpers.GlobalScale, otherwise the frame doesn't grow with FFXIV's UI scale and scaled-font labels clip inside."
    - "Misdiagnosis recovery: when a UAT-round-N fix doesn't close the user-visible report on round N+1, the next debug step is to re-derive the pixel arithmetic at the user's actual UI scale (not at 1.0) BEFORE attempting a second math-only fix. Round-N math correctness does not imply round-N causal sufficiency."

key-files:
  created: []
  modified:
    - "NamazuFlippers/UI/DailyRouteWindow.cs — drop SameLine() between bought/listed Text and the buttons, scale rescanWidth/settingsWidth by ImGuiHelpers.GlobalScale, add `using Dalamud.Interface.Utility;`"
    - "tests/phase04_nyquist.sh — append GAP-E1 04-08 block (progress_buttons_own_row_check awk helper + 2 require_pattern calls) after the 04-07 GAP-D2 listed_anchor_check invocation, before the failure-summary footer"

key-decisions:
  - "Drop SameLine after the bought/listed Text — buttons on own row. Alternative: keep buttons on the same row and conditionally widen the window. Rejected: forces a layout regression at narrow window widths (320px MinimumSize) and complicates the cursor-advance guard. Own-row is simpler, correct at every scale ≤ ~1.8, and visually matches the typical 'header text + actions' Dalamud window pattern."
  - "Multiply both button widths by ImGuiHelpers.GlobalScale rather than caching the multiplier. Reason: GlobalScale is a struct field read; cost is negligible vs. the wins of (a) tracking live UI-scale changes mid-session if the user opens System Configuration and (b) keeping the math co-located with the literal it scales."
  - "Preserve 04-07's `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X` verbatim. It is correct math addressing a real bug class (compile-time constant diverging from runtime style). The GAP-D1 nyquist block (3 assertions) still passes against the post-04-08 source."
  - "Accept the scale ≥ ~1.9 marginal-clip cap rather than auto-widening MinimumSize or stacking buttons vertically. FFXIV's slider goes 0.7 → 2.0; the user's reported scale (~1.5) and the common usage band (1.0 → 1.4) are fully covered. Auto-widening or vertical stacking are deferred (out of scope for a gap-closure plan whose purpose is to close the user's UAT issue at round 3)."

patterns-established:
  - "Own-row buttons pattern: render fixed-width button groups on their own visual row whenever they need the full ImGui.GetContentRegionAvail().X — never chain them after a preceding SameLine() unless the reservation math is known to fit the leftover."
  - "GlobalScale-multiplied frame widths pattern: any Vector2 size literal that frames scaled-font text must be multiplied by ImGuiHelpers.GlobalScale; scaled fonts inside literal-pixel frames clip when the user's UI scale > 1.0."
  - "Polarity self-check pattern for gap-closure nyquist assertions: verify the new assertions FAIL on the pre-fix source (via `git checkout HEAD~1 -- <file>`) and PASS on the post-fix source. Polarity confirmation belongs in every gap-closure plan whose new assertions could otherwise be vacuous."

requirements-completed: [UI-01, UI-08]

duration: 4 min
completed: 2026-05-08
---

# Phase 04 Plan 08: GAP-E1 closure — Rescan/Settings own-row + GlobalScale-scaled widths Summary

**Closes GAP-E1 (Rescan Route still clipped after 04-07) by rendering Settings + Rescan on their OWN row so `avail` measures the full content region width, AND by multiplying both button widths by `ImGuiHelpers.GlobalScale` so the 110/80 base frames grow with FFXIV UI scale; guards the fix with 3 new nyquist regression assertions, polarity-self-checked.**

## Performance

- **Duration:** 4 min
- **Started:** 2026-05-08T05:30:00Z (approx — plan-execution start)
- **Completed:** 2026-05-08T05:34:00Z (approx — final docs commit time)
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- **GAP-E1 closed (the user-visible bug 04-07 didn't close).** Two surgical edits to `NamazuFlippers/UI/DailyRouteWindow.cs` `DrawProgressSection`:
  1. Dropped the `ImGui.SameLine();` between the bought/listed `ImGui.Text(...)` and the button-reservation block. The buttons now render on a fresh row, so `avail = ImGui.GetContentRegionAvail().X` returns the FULL content region width (~396px at scale 1.5) instead of the leftover after the Text consumed ~207px.
  2. Replaced `const float rescanWidth = 110f` with `var rescanWidth = 110f * ImGuiHelpers.GlobalScale` and `const float settingsWidth = 80f` with `var settingsWidth = 80f * ImGuiHelpers.GlobalScale`. Frames grow with FFXIV UI scale so "Rescan Route" rendered at scaled font has room inside the scaled frame.
  Plus one new using directive: `using Dalamud.Interface.Utility;` (alphabetically first in the file's using block) so `ImGuiHelpers.GlobalScale` resolves on Windows/CI builds.
- **3 new GAP-E1 regression assertions** appended to `tests/phase04_nyquist.sh` (after the existing 04-07 GAP-D2 `listed_anchor_check` invocation, before the failure-summary footer):
  1. `Settings/Rescan buttons render on their own row — no SameLine() immediately after bought/listed Text (GAP-E1, 04-08)` — `progress_buttons_own_row_check` awk helper that locates the `ImGui.Text($"Bought:` line, walks forward to the first non-blank source line, and asserts it is NOT `ImGui.SameLine();`.
  2. `rescanWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)` — `require_pattern` for the post-fix assignment literal.
  3. `settingsWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)` — `require_pattern` for the post-fix assignment literal.
- **Polarity self-check passes** — each new assertion FAILS on the pre-fix source (verified via `git checkout HEAD~1 -- NamazuFlippers/UI/DailyRouteWindow.cs`) and PASSES on the post-fix source. The assertions actually catch the regression they claim to catch.
- **04-07's runtime ItemSpacing read preserved verbatim.** `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X` is unchanged and the GAP-D1 04-07 nyquist block (3 assertions) still passes. 04-07's math was correct — it just wasn't the user-visible cause.
- **Behavioral wiring from 04-02 / 04-04 / 04-05 / 04-06 / 04-07 preserved**, all 8 greps from 04-07-SUMMARY's preservation table still match (table reproduced below).

## GAP-E1 Closure Mechanism

### The two-edit fix

From `.planning/debug/rescan-button-still-cut-off-2.md`. Two compounding bugs, two coordinated edits.

**Edit 1 — drop SameLine after the Text.** Pre-fix, the flow was:

```csharp
ImGui.Text($"Bought: ... Listed: ...");
ImGui.SameLine();                                        // ← cursor mid-row
const float rescanWidth = 110f;                           // ← scale 1.0 only
const float settingsWidth = 80f;
var buttonSpacing = ImGui.GetStyle().ItemSpacing.X;       // 04-07 fix, correct math
var avail = ImGui.GetContentRegionAvail().X;              // ← MEASURED MID-ROW
```

Post-fix, the buttons render on their own row so `avail` measures the full content region:

```csharp
ImGui.Text($"Bought: ... Listed: ...");
// own-row comment block citing the debug doc
var rescanWidth = 110f * ImGuiHelpers.GlobalScale;
var settingsWidth = 80f * ImGuiHelpers.GlobalScale;
var buttonSpacing = ImGui.GetStyle().ItemSpacing.X;       // unchanged
var avail = ImGui.GetContentRegionAvail().X;              // FULL content width
```

**Edit 2 — scale button widths by `ImGuiHelpers.GlobalScale`.** The literal 110/80 don't grow with the font, so even on the full-width row, "Rescan Route" rendered at scale 1.5 doesn't fit a 110px frame. `ImGuiHelpers.GlobalScale` is the FFXIV UI scale factor exposed by `Dalamud.Interface.Utility` — multiply both literal widths by it so the frame grows in step.

### Pixel arithmetic

Lifted from `.planning/debug/rescan-button-still-cut-off-2.md`. Window width assumed at the default 420px.

**Pre-fix at scale 1.5 (the user's reported scale):** `combinedWidth = 110 + 12 + 80 = 202`, `avail` (mid-row, after the bought/listed Text consumed ~207px) ≈ `189`. Guard `avail > combinedWidth` → 189 > 202 → FALSE → no cursor advance. Settings at x=207..287 fits, Rescan at x=299..409 overflows the content edge at x=396 by 13px → label clips after "Rescan Rou". User report matches exactly.

**Post-fix at scale 1.5:** buttons on own row so `avail` ≈ `396` (full content region). `rescanWidth = 110 * 1.5 = 165`, `settingsWidth = 80 * 1.5 = 120`, `buttonSpacing ≈ 12`, `combinedWidth = 297`. Guard `396 > 297` → TRUE → cursor advances by 99 → Settings at x=99..219, Rescan at x=231..396 — exactly at the content edge. ✓

**Post-fix at scale 1.0:** `rescanWidth = 110`, `settingsWidth = 80`, `buttonSpacing ≈ 8`, `combinedWidth = 198`, `avail ≈ 404`. Guard 404 > 198 → TRUE → cursor advances → both buttons fit comfortably. Behavior matches the prior working state at scale 1.0.

**Post-fix at scale 2.0 (known cap):** `rescanWidth = 220`, `settingsWidth = 160`, `buttonSpacing ≈ 16`, `combinedWidth = 396`, `avail ≈ 388`. Guard 388 > 396 → FALSE → no cursor advance → Rescan ends at x=396 against content edge ~388, marginal clip ~8px. Documented as a known cap (T-04-08-01); FFXIV's UI scale slider goes 0.7 → 2.0, the common usage band is 1.0 → ~1.8. See "Known Limitation" below.

## The Misdiagnosis Lesson

This plan exists because 04-07 attempted to close the user-visible Rescan-clip bug and didn't. The diagnosis was wrong, but in a subtle way that's worth naming for future debug rounds.

- **04-07 fixed real, correct math.** Replacing `const float buttonSpacing = 8f` with `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X` IS a correctness fix. At scale 1.0 it's a no-op (8 == 8). At scale > 1.0 it makes the reservation-side gap match the actual gap that `ImGui.SameLine()` between Settings and Rescan inserts. Without that fix, the reservation math diverges from the layout math, which is a real bug class. **04-08 preserves 04-07's edit verbatim** — `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X` is still there, the GAP-D1 nyquist block (3 assertions) still passes.
- **But 04-07 addressed the wrong mechanism for the user-visible bug.** The user's UAT report was "Rescan Route is cut off — I see 'Rescan Rou' before clip." That symptom traces to `avail` being measured on a partially-consumed row (after the bought/listed Text + SameLine consumed ~207px of horizontal space) AND to literal-pixel button widths (110/80) that don't scale with the font. Neither of those is "the spacing-between-Settings-and-Rescan is wrong by `8 * (scale - 1)` pixels." 04-07's fix is upstream-correct but downstream-insufficient.
- **Lesson for future debugging.** When a UAT-round-N fix doesn't close the user-visible report on round N+1, the next debug step is to **re-derive the pixel arithmetic at the user's actual UI scale**, not at 1.0, BEFORE attempting a second math-only fix. The pixel arithmetic in `.planning/debug/rescan-button-still-cut-off-2.md` (which named GAP-E1 in 30 minutes of work) would have caught this on round 1 if the round-1 author had derived `avail` at scale 1.5 from the cursor's actual position rather than reasoning about the spacing constant in isolation. Future code-review and verification steps should treat any layout-math edit on a window whose target users are not all on scale-1.0 monitors as **scale-parametric by default** — assert across {1.0, 1.5, 2.0} or write the parametric test, don't reason about scale 1.0 alone.

## New Nyquist Assertions Added

Three new `ok -` lines appear under the `Gap closure regression (04-08): Rescan/Settings own-row + GlobalScale-scaled widths (GAP-E1)` heading in the post-fix `bash tests/phase04_nyquist.sh` output. The 3 labels are:

1. `Settings/Rescan buttons render on their own row — no SameLine() immediately after bought/listed Text (GAP-E1, 04-08)` — emitted by the `progress_buttons_own_row_check` awk helper. Algorithm: locate the first `ImGui.Text($"Bought:` line in `DailyRouteWindow.cs`, walk forward to the first non-blank source line, fail if that line (with leading whitespace stripped) starts with `ImGui.SameLine();`. Catches re-introduction of the chained-row layout.
2. `rescanWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)` — `require_pattern` for `rescanWidth\s*=\s*110f\s*\*\s*ImGuiHelpers\.GlobalScale`. Catches removal of the GlobalScale multiplier on rescanWidth.
3. `settingsWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)` — `require_pattern` for `settingsWidth\s*=\s*80f\s*\*\s*ImGuiHelpers\.GlobalScale`. Catches removal of the GlobalScale multiplier on settingsWidth.

The structural assertion (`progress_buttons_own_row_check`) is one helper but emits one `ok/not ok` line; the two `require_pattern` calls each emit one — total 3 new labels.

## Polarity Self-Check Log

Polarity proves the new assertions actually exercise the regression they claim to catch. Run after Task 2 commit, before final docs commit:

```
$ git checkout HEAD~1 -- NamazuFlippers/UI/DailyRouteWindow.cs
$ grep -nE 'const float rescanWidth' NamazuFlippers/UI/DailyRouteWindow.cs
122:        const float rescanWidth = 110f;       # pre-fix source restored
$ grep -cE 'GlobalScale' NamazuFlippers/UI/DailyRouteWindow.cs
0                                                  # GlobalScale absent in pre-fix
$ bash tests/phase04_nyquist.sh
... not ok - Settings/Rescan buttons render on their own row ... (GAP-E1, 04-08)
... not ok - rescanWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)
... not ok - settingsWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)
Phase 04 Nyquist validation failed: 3 check(s) failed.
exit: 1                                            # ← assertions DO fail on pre-fix
$ git checkout HEAD -- NamazuFlippers/UI/DailyRouteWindow.cs
$ grep -cE 'ImGuiHelpers\.GlobalScale' NamazuFlippers/UI/DailyRouteWindow.cs
3                                                  # post-fix source restored
$ bash tests/phase04_nyquist.sh
... ok - Settings/Rescan buttons render on their own row ... (GAP-E1, 04-08)
... ok - rescanWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)
... ok - settingsWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)
Phase 04 Nyquist validation passed.
exit: 0                                            # ← assertions PASS on post-fix
```

3 not-ok on pre-fix, 3 ok on post-fix, count exact, polarity confirmed.

## Task Commits

Each task was committed atomically (single-line messages per global git conventions, no body, no Claude attribution):

1. **Task 1: Fix DrawProgressSection — own-row buttons + GlobalScale-scaled widths** — `a5b7745` (fix)
   `fix(04-08): rescan/settings on own row + GlobalScale-scaled widths (GAP-E1)`
2. **Task 2: Append GAP-E1 regression assertions to phase04_nyquist.sh** — `cd7ad9a` (test)
   `test(04-08): nyquist regression for GAP-E1 (own-row + GlobalScale)`

**Plan metadata:** committed below as a `docs(04-08): ...` commit including this SUMMARY.md, STATE.md, ROADMAP.md, and REQUIREMENTS.md (UI-01 footnote).

## Files Created/Modified

- `NamazuFlippers/UI/DailyRouteWindow.cs` — Two surgical edits: (1) using-directive block grew by one line: `using Dalamud.Interface.Utility;` placed first, alphabetically. (2) DrawProgressSection: dropped `ImGui.SameLine();` after the bought/listed Text, replaced `const float rescanWidth = 110f;` and `const float settingsWidth = 80f;` with `var rescanWidth = 110f * ImGuiHelpers.GlobalScale;` and `var settingsWidth = 80f * ImGuiHelpers.GlobalScale;`, added a 6-line comment block citing `.planning/debug/rescan-button-still-cut-off-2.md`. Net: +9 / -3 lines, single file.
- `tests/phase04_nyquist.sh` — Appended one new section block immediately after the existing 04-07 GAP-D2 `listed_anchor_check` invocation and immediately before the failure-summary footer (`if [[ "$failures" -ne 0 ]]; then ...`). New content: 1 echo header, 1 echo label, 14-line comment, `progress_buttons_own_row_check` awk helper (~26 lines including local vars + close-brace), one invocation, 2 `require_pattern` calls. Net: +54 / -0 lines, no existing assertion modified.

## Byte-for-byte Unchanged (constraint compliance)

Confirmed via `git diff --name-only HEAD~2 HEAD -- ...` returning empty for every constrained path:

- `NamazuFlippers/Core/RouteStop.cs` — unchanged (Phase 3 source)
- `NamazuFlippers/Core/RouteOptimizer.cs` — unchanged (Phase 3 source)
- `NamazuFlippers/Core/ScanEngine.cs` — unchanged (Phase 3 source)
- `NamazuFlippers/NamazuFlippers.cs` — unchanged (out-of-scope window)
- `NamazuFlippers/UI/ConfigWindow.cs` — unchanged (out-of-scope window)
- `NamazuFlippers/UI/FirstRunWindow.cs` — unchanged (out-of-scope window)
- `tests/phase03_nyquist.sh` — unchanged (Phase 3 nyquist gate)

`bash tests/phase03_nyquist.sh` exit code is **1**, the documented baseline (2 pre-existing Phase 3 failures, count unchanged from 04-07). This plan introduced no new Phase 3 regressions and the two pre-existing Phase 3 failures are not in scope of a Phase 4 UI gap-closure plan.

## Behavioral Preservation (cite the verification greps)

All behavioral preservation greps from 04-07-SUMMARY's preservation table match against the post-04-08 source — confirming the fixes from 04-02 / 04-04 / 04-05 / 04-06 / 04-07 are preserved verbatim:

| Grep | Expected | Actual | Provenance |
|------|----------|--------|------------|
| `plugin\.OpenConfigWindow` | 1 | 1 | 04-05 (Settings entry point) |
| `plugin\.RescanAsync` | 1 | 1 | 04-05 (Rescan click handler) |
| `BeginDisabled` | 1 | 1 | 04-02 (ScanInProgress disabled-state guard) |
| `EndDisabled` | 1 | 1 | 04-02 (ScanInProgress disabled-state guard) |
| `listedState\[item\.ItemId\]\s*=\s*listed` | 1 | 1 | 04-04 (Listed checkbox dictionary write) |
| `##bought-` | 1 | 1 | 04-02 (Bought checkbox imgui ID) |
| `##listed-` | 1 | 1 | 04-04 (Listed checkbox imgui ID) |
| `if \(!isDirty\)` in ConfigWindow.cs | present | present | 04-06 (Discard guard) |
| `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X` | 1 | 1 | 04-07 (GAP-D1 runtime style read) |
| `SameLine\(listedAnchorX\)` | 1 | 1 | 04-07 (GAP-D2 listed-checkbox column anchor) |

All pre-existing nyquist assertion blocks (UI-01..UI-08, CONF-01..CONF-09, color tokens, FirstRunWindow migration, lambda safety, gap-closure 04-04, gap-closure 04-06, GAP-D1 04-07, GAP-D2 04-07) print `ok` on the post-04-08 source. Counts: `gap-closure 04-04` matches 4 lines, `gap-closure 04-06` matches 2 lines, `GAP-D1, 04-07` matches 3 lines, `GAP-D2, 04-07` matches 1 line, `GAP-E1, 04-08` matches 3 lines.

## Decisions Made

- **Drop SameLine, don't widen the window.** The 320px MinimumSize is set in the `DailyRouteWindow` constructor and any auto-widen would either violate the user's manual resize or require a second-pass layout. Own-row buttons are simpler and visually match the typical Dalamud "header text on row 1, action buttons on row 2" pattern.
- **`ImGuiHelpers.GlobalScale` not a self-cached scale factor.** Reading `GlobalScale` on every frame is a single struct field access; the cost is negligible and it tracks live UI-scale changes if the user opens System Configuration mid-session. No need for a static cache; correctness over micro-optimization (mirrors the 04-07 decision on `ImGui.GetStyle().ItemSpacing.X`).
- **Preserve 04-07's runtime ItemSpacing read verbatim.** It is correct math and the GAP-D1 nyquist block enforces it. The fact that 04-07 misdiagnosed the user-visible bug doesn't make 04-07's edit wrong — it just makes it insufficient.
- **Accept the scale ≥ ~1.9 marginal-clip cap.** At scale 2.0 with 420px window, `combinedWidth ≈ 396px` fits exactly in `avail ≈ 388px` only with the cursor-advance guard FALSE, so both buttons start at left and Rescan clips by ~8px. Bullet-proofing this would require widening MinimumSize (regression at user-resize) or stacking the buttons vertically (regression at common-case visual layout). Both deferred — out of scope for a gap-closure plan whose purpose is to close UAT round 2 + close round 3 at the user's reported ~1.5 scale.

## Deviations from Plan

None — plan executed exactly as written. No Rule 1/2/3 auto-fixes were needed (the plan was extremely tight: 2 atomic auto tasks, full code snippets pre-written, every assertion specified verbatim, polarity self-check pre-specified). No Rule 4 architectural decisions arose.

**Total deviations:** 0
**Impact on plan:** No scope creep. Both task commits match the plan's `<files_modified>` list exactly. `git diff --name-only HEAD~2 HEAD` returns exactly the two expected files. The W-1 (4-space indent strip) and W-2 (`! grep -qE` runtime preference) plan-checker notes were absorbed during execution (the inserted bash block was left-aligned to column 0 like the surrounding helpers; W-2 was a runtime preference and is not user-visible). W-3 (re-open UI-01 footnote) is handled in the REQUIREMENTS.md edit included in the final docs commit.

## Issues Encountered

None.

## Authentication Gates

None — no external services touched in this plan.

## Build Verification Model

**Local validation is source-level only.** Per `.planning/PROJECT.md` and `.planning/STATE.md`:

- macOS `dotnet build NamazuFlippers/NamazuFlippers.csproj` is **expected to fail** without Dalamud SDK assemblies installed locally. Did NOT attempt it.
- `bash tests/phase04_nyquist.sh` is the local source-validation gate — exits 0 after both tasks (all pre-existing assertions plus all 3 new GAP-E1 assertions pass).
- `bash tests/phase03_nyquist.sh` baseline preserved (exit 1, 2 pre-existing failures unchanged).
- **GitHub Actions is the authoritative compile/package gate.** The post-merge CI run will compile the plugin against the Dalamud SDK (resolving `using Dalamud.Interface.Utility;` and `ImGuiHelpers.GlobalScale`) and produce the next packaged build (>1.0.26.0); only that build is the ship-level verification surface for in-game UAT round 3.
- **Ship-level gate:** in-game UAT at the user's actual FFXIV UI scale (~1.5) on the post-merge build closes GAP-E1. UAT Test 1 (Rescan visible at the user's UI scale) is the closure criterion.

## Known Limitation (T-04-08-01)

At FFXIV UI scale ≥ ~1.9 with the default 420px window, both scaled button widths plus spacing approach the content edge: `combinedWidth = 110*scale + GAP*scale + 80*scale ≈ 190 * scale + small`, `avail ≈ 396 - 8 * (scale - 1)` (window padding scales). At scale 2.0 specifically, `combinedWidth ≈ 396px` and `avail ≈ 388px`, so the cursor-advance guard is FALSE and Rescan marginally clips by ~8px (about half a character). The fix covers the common scale range 1.0 → ~1.8 (the user's reported ~1.5 included), which is the band most users run; the FFXIV slider goes 0.7 → 2.0, but the right tail above 1.8 is rare on common monitor setups.

Re-widening MinimumSize or vertical button stack are deferred (out of scope for this gap-closure plan). If a UAT user later reports clipping at scale 2.0 specifically, the fix would be a follow-up plan that either bumps `Size = new Vector2(420, 560)` to ~480 or stacks Settings + Rescan vertically inside a `BeginGroup`/`EndGroup` block.

## Next Phase Readiness

- Phase 04 core-ui is now ready for UAT round 3 re-verification on the post-merge CI build (>1.0.26.0). Round 3 closure criterion: user reports Rescan Route fully visible at their reported FFXIV UI scale. UI-01 stays footnoted as "GAP-E1 closure pending UAT round 3" in REQUIREMENTS.md until the user confirms — see "Threat Flags / Status" below for the explicit footnote.
- No blockers for Phase 05 (session-store). The behavioral wiring this phase preserves (boughtState / listedState dictionaries, profit tally LINQ, OnOpen `!isDirty` snapshot guard, SameLine(listedAnchorX) anchor) is the surface Phase 05 will persist to JSON; nothing in 04-08 changes that contract. The own-row layout doesn't introduce any new persistence-relevant state.
- Threat model items T-04-08-01 (cosmetic DoS at scale ≥ ~1.9), T-04-08-02 (`GlobalScale` returning 0 — Dalamud API contract precludes it), and T-04-08-03 (button-width math at MinimumSize) are accepted with documented caps. T-04-08-04 (regression-tampering) and T-04-08-05 (behavioral-wiring tampering) are mitigated by the new GAP-E1 assertions and the existing UI-NN behavioral assertions respectively.

## Threat Flags

None — this plan introduced no new network endpoints, no new auth paths, no new file/IO surface, no schema changes. Layout-math edits and source-validation assertions only. The plan's `<threat_model>` covers the full surface; no new threat-flag entries raised by Task 1 or Task 2.

## Self-Check: PASSED

Verification re-run after writing this SUMMARY:

- File existence:
  - `[ -f .planning/phases/04-core-ui/04-08-SUMMARY.md ]` — FOUND (this file)
  - `[ -f NamazuFlippers/UI/DailyRouteWindow.cs ]` — FOUND
  - `[ -f tests/phase04_nyquist.sh ]` — FOUND
- Commit existence:
  - `git log --oneline | grep a5b7745` — FOUND (`fix(04-08): rescan/settings on own row + GlobalScale-scaled widths (GAP-E1)`)
  - `git log --oneline | grep cd7ad9a` — FOUND (`test(04-08): nyquist regression for GAP-E1 (own-row + GlobalScale)`)
- Acceptance criteria re-run:
  - Task 1: all 9 acceptance criteria PASS (using directive added; rescanWidth/settingsWidth scaled by GlobalScale; const-float gone; runtime ItemSpacing.X read preserved; structural awk confirms next non-blank line after Text is the new comment, not SameLine; behavioral wiring greps all == 1; Phase 3 source unchanged in this task; `git diff --name-only HEAD~1 HEAD` (Task 1's commit) = exactly DailyRouteWindow.cs).
  - Task 2: all 12 acceptance criteria PASS (`bash -n` exits 0; `bash phase04_nyquist.sh` exits 0; 3 new GAP-E1 ok lines printed exactly once each; GAP-E1 04-08 label count = 3; progress_buttons_own_row_check count = 2; GAP-D1 04-07 = 3 unchanged; GAP-D2 04-07 = 1 unchanged; gap-closure 04-04 = 4 unchanged; gap-closure 04-06 = 2 unchanged; polarity self-check stash → fail → unstash → pass; `bash phase03_nyquist.sh` exits 1 baseline; `git diff --name-only HEAD~1 HEAD` for Task 2's commit = exactly tests/phase04_nyquist.sh).
- Plan-level verification re-run:
  - `bash tests/phase04_nyquist.sh` → exit 0 ✓
  - `bash tests/phase03_nyquist.sh` → exit 1 (baseline preserved, 2 pre-existing failures unchanged) ✓
  - `git diff --name-only HEAD~2 HEAD` → exactly 2 files: `NamazuFlippers/UI/DailyRouteWindow.cs` and `tests/phase04_nyquist.sh` ✓
  - Phase 3 source + cross-window source + `phase03_nyquist.sh` byte-unchanged across `HEAD~2..HEAD` ✓
  - All 8 behavioral-preservation greps from 04-07-SUMMARY's table match expected counts ✓
  - GAP-D1 04-07 (3 assertions) and GAP-D2 04-07 (1 assertion) still pass — 04-07's fixes preserved ✓
  - Polarity self-check (revert via `git checkout HEAD~1`, run, restore via `git checkout HEAD`, re-run) — 3 not-ok on pre-fix, 3 ok on post-fix ✓

---
*Phase: 04-core-ui*
*Completed: 2026-05-08*
