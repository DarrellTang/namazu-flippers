---
status: complete
phase: 04-core-ui
source: [04-VERIFICATION.md]
started: 2026-05-07T08:50:00Z
updated: 2026-05-07T09:30:00Z
---

## Current Test

[testing complete]

## Tests

### 1. DailyRouteWindow route rendering
expected: Interactive route window matching the 04-UI-SPEC layout — status banner at top, progress section, route stops, item rows with checkboxes and price columns. Server stops render as CollapsingHeaders in route order. Each item row shows name, Buy price in PurchaseCyan, +profit/day in GilGold. Bought checkbox dims name to CompletedGray. Progress bars animate. Profit tally updates as listed checkboxes change.
result: issue
reported: "The profit still shows zero even though I've checked some of the boxes. Everything else seems to be working properly, except that the calculation seems to be wrong. Some of the items I purchased were at the buy price, which is correct, but the listing price is much higher than what the profit suggests here."
severity: major

### 2. Auto-collapse trigger and reset
expected: Auto-collapse fires on first all-bought frame via `SetNextItemOpen(false, ImGuiCond.Always)`. Per-stop flag prevents re-fire. Un-check resets flag so subsequent re-completion triggers again. Header shows `✓ {StopName}` in CompletedGray after collapse.
result: pass

### 3. ConfigWindow snapshot / dirty / save flow
expected: D-12 flow — opening ConfigWindow snapshots Configuration; any change flips isDirty; closing while dirty cancels close and shows "Save changes before closing?" modal with Save / Discard / Cancel buttons; Save persists via `SavePluginConfig` and clears dirty.
result: issue
reported: "I see no settings button in the route window. The re-scan route button is also cut off on the right. I can get to the settings by clicking in the plugins window and clicking on the settings button with the gear icon. Discard does not revert the change. Save does save the change correctly. Cancel closes the modal but keeps the ConfigWindow open."
severity: major

### 4. Reset to Defaults with confirmation modal
expected: D-13 — Reset to Defaults button (ErrorRed) opens "Reset all settings to defaults?" modal. Reset reverts every tuning value to defaults but preserves HomeWorld. isDirty flips true; user must still click Save to persist.
result: pass

### 5. Rescan button disabled state
expected: While `plugin.ScanInProgress` is true, the Rescan button is greyed via `BeginDisabled`. After scan completes, button re-enables. Clicking it fires `plugin.RescanAsync` and the bought/listed/autoCollapsed dictionaries wipe on new scan result.
result: pass

## Summary

total: 5
passed: 3
issues: 2
pending: 0
skipped: 0
blocked: 0

## Gaps

- truth: "The profit tally displays the sum of ExpectedDailyProfit for listed items in GilGold and updates each frame as listed checkboxes are toggled"
  status: failed
  reason: "User reported: The profit still shows zero even though I've checked some of the boxes. Everything else seems to be working properly, except that the calculation seems to be wrong. Some of the items I purchased were at the buy price, which is correct, but the listing price is much higher than what the profit suggests here."
  severity: major
  test: 1
  artifacts: []
  missing: []
  hint: "User checked 'some of the boxes' — likely bought-checkboxes (which intentionally don't count toward profit), and possibly listed-checkboxes too. Two-part diagnosis: (a) verify listedState wiring actually flips when ##listed-{itemId} is clicked and that LINQ Where(o => listedState.GetValueOrDefault(o.ItemId)).Sum(o => o.ExpectedDailyProfit) executes correctly; (b) audit ExpectedDailyProfit semantics — user expects (HomePrice - PurchasePrice) per item but tally sums ExpectedDailyProfit (margin × sales/day, can be much smaller than per-flip margin). UI label may need to clarify 'expected daily profit' vs 'realized resale margin'."

- truth: "DailyRouteWindow has a Settings button that opens ConfigWindow (D-07 second entry point alongside /xlsettings gear icon)"
  status: failed
  reason: "User reported: I see no settings button in the route window."
  severity: major
  test: 3
  artifacts: ["NamazuFlippers/UI/DailyRouteWindow.cs"]
  missing: ["plugin.OpenConfigWindow() invocation from a button in the route window header"]
  hint: "04-02 plan T1 specified DrawProgressSection adds 'Rescan Route' AND 'Settings' buttons in the top section. Verify the Settings button is being rendered (may be conditionally hidden, off-screen due to layout, or only the Rescan button was implemented)."

- truth: "Rescan Route button renders fully within the DailyRouteWindow's visible area at default window width (720px)"
  status: failed
  reason: "User reported: The re-scan route button is also cut off on the right."
  severity: minor
  test: 3
  artifacts: ["NamazuFlippers/UI/DailyRouteWindow.cs"]
  missing: ["sufficient horizontal space or smaller button label for the top button row"]
  hint: "Likely cause: button row uses fixed widths or absolute positioning that overflows at 720px. Fix could be (a) shorter labels (Rescan / Settings instead of Rescan Route), (b) ImGui.SameLine layout audit, or (c) ImGui.GetContentRegionAvail-based sizing."

- truth: "Discard button in unsaved-changes modal restores the snapshot to plugin.Configuration, clears isDirty, and closes the window (D-12)"
  status: failed
  reason: "User reported: Discard does not revert the change. (Save and Cancel work correctly.)"
  severity: major
  test: 3
  artifacts: ["NamazuFlippers/UI/ConfigWindow.cs"]
  missing: ["working snapshot-restoration path on Discard click"]
  hint: "RestoreFromSnapshot copies snapshot fields back into plugin.Configuration but may be missing one or more properties, OR Configuration is a reference-type and snapshot stores the same reference (mutating Configuration mutates snapshot). Audit whether snapshot is a deep clone (e.g., JSON round-trip or explicit field-by-field copy) and whether RestoreFromSnapshot writes every field back. If user edited a CategoryFilters checkbox or HomeWorld dropdown, those nested/value cases are likely culprits."
