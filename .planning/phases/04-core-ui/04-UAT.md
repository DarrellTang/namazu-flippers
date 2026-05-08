---
status: diagnosed
phase: 04-core-ui
source: [04-04-SUMMARY.md, 04-05-SUMMARY.md, 04-06-SUMMARY.md]
scope: gap-closure-behavioral-verification
started: 2026-05-08T03:12:12Z
updated: 2026-05-08T03:45:00Z
build_tested: 1.0.26.0
---

## Current Test

[testing complete]

## Tests

### 1. Profit tally + listed checkbox (re-verify GAP-A1)
expected: Open DailyRouteWindow with today's scan loaded. The progress section shows a profit number in GilGold (yellow). Below it, item rows each have two checkboxes per row — Bought and Listed (`##listed-{itemId}`). The Listed column renders on every item in every RouteStop. Click Listed on a few rows. The GilGold profit tally recomputes each frame, summing ExpectedDailyProfit for checked items. Unchecking subtracts.
fixed_by: 04-04
result: issue
behavioral_gap_closed: true
reported: "there's 2 checkboxes now but the 2nd one is at the end of the line which is very ugly to look at. because the lines are varying lengths, the checkboxes aren't lined up. but it is there. checking the listed checkbox does increase the accompanying status bar and updates the profit number."
severity: cosmetic
build_tested: 1.0.26.0

### 2. Settings + Rescan visibility at 420px (re-verify GAP-B1/B2)
expected: Open DailyRouteWindow at default size (do not resize). In the progress section header row, BOTH a Settings button and a Rescan Route button are visible, right-aligned, neither clipped or pushed off the right edge. Click Settings → ConfigWindow opens. Click Rescan Route → scan starts and the button greys out (disabled) until the scan completes.
fixed_by: 04-05
result: issue
gap_b1_closed: true
gap_b2_closed: false
reported: "Rescan route is still cut off. Settings is there though"
severity: major
build_tested: 1.0.26.0

### 3. ConfigWindow Discard reverts edits (re-verify GAP-C1)
expected: Open ConfigWindow (via gear icon OR the new Settings button). Note the current value of one setting (e.g., a slider or HomeWorld dropdown). Edit it to something different. Close the window via the X / Esc / window close. The "Save changes before closing?" modal appears. Click Discard. ConfigWindow closes. Re-open ConfigWindow and confirm the field is back to its original (pre-edit) value, not the edited one. (Cancel and Save flows are not under test here — only Discard.)
fixed_by: 04-06
result: pass
build_tested: 1.0.26.0

## Summary

total: 3
passed: 1
issues: 2
pending: 0
skipped: 0
blocked: 0

## Closures Confirmed (this UAT round)

- GAP-A1 behavioral fix (profit tally + listed checkbox toggle) → CLOSED in 1.0.26.0
- GAP-B1 (Settings button visible) → CLOSED in 1.0.26.0
- GAP-C1 (ConfigWindow Discard reverts) → CLOSED in 1.0.26.0

## Closures NOT Confirmed (this UAT round)

- GAP-B2 (Rescan button not clipped at 420px) → STILL FAILS in 1.0.26.0 — see new gap below

## New Findings (this UAT round)

- Cosmetic alignment of Listed checkboxes — see new gap below

## Gaps

- truth: "Listed checkboxes are visually aligned in a consistent column across item rows in DailyRouteWindow"
  status: diagnosed
  reason: "User reported on build 1.0.26.0: 'there's 2 checkboxes now but the 2nd one is at the end of the line which is very ugly to look at. because the lines are varying lengths, the checkboxes aren't lined up.' Note: behavioral gap (GAP-A1, profit tally update) is CLOSED — user confirmed checkbox toggle increases status bar and updates profit number."
  severity: cosmetic
  test: 1
  root_cause: "DrawItems (DailyRouteWindow.cs:205-259) draws the Listed checkbox after a bare ImGui.SameLine() with no offset, following 5 variable-width elements: item.Name (item-dependent), [OOS] badge (conditional), [Vendor] badge (conditional), 'Buy: {price}' (digit count varies), '+{profit}/day' (digit count varies). With nothing anchoring the checkbox to a fixed X column, each row's accumulated width differs, so the checkbox lands at a different X on every row. The behavioral wiring from 04-04 (checkbox + listedState dict update + profit tally LINQ) is correct and must not be touched — only the X positioning is wrong."
  artifacts:
    - path: "NamazuFlippers/UI/DailyRouteWindow.cs"
      issue: "Lines 243-251: bare `ImGui.SameLine()` before the Listed checkbox lacks an absolute column anchor. Variable-width preceding elements cause the checkbox X to drift per row."
  missing:
    - "Replace bare `ImGui.SameLine()` before the Listed checkbox with an absolute-position call: either `ImGui.SameLine(fixedOffset)` or `ImGui.SetCursorPosX(contentMax.X - listedColumnWidth)` so the checkbox lands in a consistent column across rows."
    - "Either hardcode an offset that fits the 420px content region (~330-360px from window left), OR compute it via `ImGui.GetWindowContentRegionMax().X - checkboxWidth - 'List:'-text-width` each frame so it stays right-aligned if the window is later resizable."
    - "Add a nyquist regression assertion: a `ImGui.SameLine\(\d` or `SetCursorPosX` pattern must precede the `##listed-` Checkbox call, so a future refactor can't reintroduce drift."
  debug_session: .planning/debug/listed-checkbox-not-aligned.md

- truth: "Rescan Route button renders fully within DailyRouteWindow at default 420px window width"
  status: diagnosed
  reason: "User reported on build 1.0.26.0: 'Rescan route is still cut off. Settings is there though.' This is the same gap originally reported as GAP-B2; 04-05's combined-width arithmetic (110 + 8 + 80 = 198px right-alignment) closed the Settings visibility (GAP-B1) but did NOT close the Rescan clipping (GAP-B2)."
  severity: major
  test: 2
  root_cause: "DrawProgressSection hardcodes `const float buttonSpacing = 8f` in the combinedWidth formula, but ImGui.SameLine() between Settings and Rescan uses runtime ImGui.GetStyle().ItemSpacing.X — which Dalamud SCALES BY THE FFXIV GLOBAL UI SCALE FACTOR. At any UI scale > 1.0, the actual gap is 8 * scale > 8, so SetCursorPosX reserves too little space and Rescan ends at content_right + (actual_ItemSpacing - 8) — past the clipping boundary by that delta. The arithmetic was correct at scale 1.0 (which is why the Settings-only fix appeared to close GAP-B1 in source-pattern testing) but fails at the user's actual UI scale. Code review WR-02 identified this exact mechanism in 04-REVIEW.md before user UAT; 04-VERIFICATION.md incorrectly dismissed it as cosmetic-only. The user's UAT result confirms WR-02."
  artifacts:
    - path: "NamazuFlippers/UI/DailyRouteWindow.cs"
      issue: "Lines 124-128: `const float buttonSpacing = 8f` is a compile-time constant that diverges from the runtime `ImGui.GetStyle().ItemSpacing.X` used by `SameLine()` between Settings and Rescan. Mismatch grows with FFXIV UI scale."
    - path: ".planning/phases/04-core-ui/04-REVIEW.md"
      issue: "WR-02 named this exact bug pre-UAT; the verifier dismissed it incorrectly. Code-review-to-verifier handoff has a gap when the verifier downgrades a code-review WR finding without explicit user-facing testing at non-1.0 scales."
  missing:
    - "Replace `const float buttonSpacing = 8f` with `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X;` so the reserved gap tracks the actual gap that SameLine() will insert at the user's runtime UI scale. One-line fix per 04-REVIEW.md WR-02."
    - "Add a nyquist regression assertion: phase04_nyquist.sh must require `ImGui\\.GetStyle\\(\\)\\.ItemSpacing\\.X` near the combinedWidth/SetCursorPosX block so a future refactor cannot silently re-hardcode the value."
    - "Promote 04-REVIEW.md WR-02 from 'advisory' to a verifier blocker pattern: when a code review finding identifies a runtime-style mismatch, do not dismiss without explicit non-1.0-UI-scale verification."
  debug_session: .planning/debug/rescan-button-still-cut-off.md
