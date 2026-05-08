---
status: complete
phase: 04-core-ui
source: [04-04-SUMMARY.md, 04-05-SUMMARY.md, 04-06-SUMMARY.md]
scope: gap-closure-behavioral-verification
started: 2026-05-08T03:12:12Z
updated: 2026-05-08T03:35:00Z
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
  status: failed
  reason: "User reported on build 1.0.26.0: 'there's 2 checkboxes now but the 2nd one is at the end of the line which is very ugly to look at. because the lines are varying lengths, the checkboxes aren't lined up.' Note: behavioral gap (GAP-A1, profit tally update) is CLOSED — user confirmed checkbox toggle increases status bar and updates profit number."
  severity: cosmetic
  test: 1
  context: "Introduced by 04-04 listed-checkbox column. The fix renders ##listed-{itemId} unconditionally in DrawItems but does not column-align it; with item names of varying width and ImGui's flow layout, the Listed checkbox lands at the end of each row at varying X coordinates."
  artifacts: []
  missing:
    - "Use ImGui table columns or fixed-width text padding (Selectable with FixedHeight, or BeginTable/EndTable, or pre-padded item-name field) so the Listed checkbox occupies a consistent X column across all rows"

- truth: "Rescan Route button renders fully within DailyRouteWindow at default 420px window width"
  status: failed
  reason: "User reported on build 1.0.26.0: 'Rescan route is still cut off. Settings is there though.' This is the same gap originally reported as GAP-B2; 04-05's combined-width arithmetic (110 + 8 + 80 = 198px right-alignment) closed the Settings visibility (GAP-B1) but did NOT close the Rescan clipping (GAP-B2)."
  severity: major
  test: 2
  context: "04-05 computed combinedWidth assuming the Rescan button occupies 110px and the Settings button occupies 80px. Likely failure mode: ImGui FramePadding (default ~4px each side) makes the actual rendered width of `Button(\"Rescan Route\", new Vector2(110, 0))` exceed 110px when text doesn't fit, OR the 420px window's content region (after WindowPadding ~16px each side + ScrollbarSize) is < 198px so Rescan still overflows. Settings now renders within bounds because it's drawn FIRST (leftmost in the right-aligned group) — Rescan, drawn after SameLine, still falls off the right edge."
  artifacts:
    - path: "NamazuFlippers/UI/DailyRouteWindow.cs"
      issue: "DrawProgressSection right-alignment math: combinedWidth = rescanWidth + spacing + settingsWidth assumes button widths are exactly Vector2(110,0) and Vector2(80,0), but ImGui adds FramePadding to the actual draw width. Also: at 420px window width, content region after WindowPadding may be < 198px."
  missing:
    - "Either (a) shorten the Rescan label to just `Rescan` so it fits in 110px including padding, or (b) compute button widths from CalcTextSize + FramePadding rather than hardcoding 110/80, or (c) drop the right-alignment and use ImGui.GetContentRegionAvail() to size both buttons evenly (avail/2 each minus spacing)."
    - "Verify against a real ImGui style snapshot: log GetWindowContentRegionWidth() vs combinedWidth at runtime to confirm whether the math overflows the available region."
