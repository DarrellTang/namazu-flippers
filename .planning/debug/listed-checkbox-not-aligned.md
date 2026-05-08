---
status: diagnosed
trigger: "Listed checkbox ##listed-{itemId} not column-aligned across item rows in DailyRouteWindow"
created: 2026-05-07T00:00:00Z
updated: 2026-05-07T00:00:00Z
---

## Current Focus

hypothesis: The Listed checkbox is positioned via unconstrained SameLine() after variable-width price/profit text fields, so its X coordinate floats with the accumulated line width instead of being pinned to a fixed column offset.
test: Trace the render sequence in DrawItems to determine what controls the X position of the Listed checkbox before it is drawn.
expecting: No fixed X-coordinate anchor is used before ##listed-{itemId}; all prior SameLine() calls are relative, not absolute.
next_action: DIAGNOSED — return root cause.

## Symptoms

expected: The Listed checkbox column occupies a consistent X coordinate across all item rows, regardless of item name length, price string width, or OOS/Vendor badge presence.
actual: "there's 2 checkboxes now but the 2nd one is at the end of the line which is very ugly to look at. because the lines are varying lengths, the checkboxes aren't lined up. but it is there. checking the listed checkbox does increase the accompanying status bar and updates the profit number."
errors: None (visual/layout only).
reproduction: Open DailyRouteWindow with a scan result loaded; observe item rows with varying name lengths.
started: Introduced in plan 04-04 which removed the isHomeStop gate and renders ##listed-{itemId} unconditionally.

## Eliminated

(none — source-level inspection reached confirmed root cause directly)

## Evidence

- timestamp: 2026-05-07T00:00:00Z
  checked: DrawItems render sequence in DailyRouteWindow.cs lines 205-259
  found: |
    Per-item render order:
      1. Checkbox  ##bought-{itemId}           — first element, left edge
      2. SameLine()                             — relative, no offset arg
      3. item.Name (Text / TextColored)         — variable width
      4. [OOS] badge — SameLine(0,4) if present — variable presence
      5. [Vendor] badge — SameLine(0,4) if present — variable presence
      6. SameLine()                             — relative, no offset arg
      7. "Buy: {price}" (TextColored)           — variable width (price digits)
      8. SameLine()                             — relative, no offset arg
      9. "+{profit}/day" (TextColored)          — variable width (profit digits)
     10. SameLine()                             — relative, no offset arg
     11. Checkbox  ##listed-{itemId}            — X = accumulated width of steps 1–9
     12. SameLine(); "List: {price}" text
  implication: |
    Steps 3–9 accumulate a variable amount of horizontal space. Step 10's SameLine() has no
    explicit offset argument, so the Listed checkbox is placed immediately after the profit
    text at whatever X position the cursor happens to be. Items with longer names or larger
    price/profit numbers push the checkbox further right. Items with OOS/Vendor badges add
    additional variable width. There is no call to SetCursorPosX() or SameLine(fixedOffset)
    before step 11, so no fixed column is established.

## Resolution

root_cause: |
  `##listed-{itemId}` is placed with an unconstrained `ImGui.SameLine()` (no offset argument)
  after a series of variable-width elements: the item name, optional [OOS] and [Vendor] badges,
  a "Buy: {price}" field, and a "+{profit}/day" field. Because these elements vary in width
  per row, each row leaves the ImGui cursor at a different X position before the Listed
  checkbox is drawn, producing the ragged right-side alignment seen in UAT.

fix: ""
verification: ""
files_changed: []
