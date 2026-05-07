---
status: partial
phase: 04-core-ui
source: [04-VERIFICATION.md]
started: 2026-05-07T08:50:00Z
updated: 2026-05-07T08:50:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. DailyRouteWindow route rendering
expected: Interactive route window matching the 04-UI-SPEC layout — status banner at top, progress section, route stops, item rows with checkboxes and price columns. Server stops render as CollapsingHeaders in route order. Each item row shows name, Buy price in PurchaseCyan, +profit/day in GilGold. Bought checkbox dims name to CompletedGray. Progress bars animate. Profit tally updates as listed checkboxes change.
result: [pending]

### 2. Auto-collapse trigger and reset
expected: Auto-collapse fires on first all-bought frame via `SetNextItemOpen(false, ImGuiCond.Always)`. Per-stop flag prevents re-fire. Un-check resets flag so subsequent re-completion triggers again. Header shows `✓ {StopName}` in CompletedGray after collapse.
result: [pending]

### 3. ConfigWindow snapshot / dirty / save flow
expected: D-12 flow — opening ConfigWindow snapshots Configuration; any change flips isDirty; closing while dirty cancels close and shows "Save changes before closing?" modal with Save / Discard / Cancel buttons; Save persists via `SavePluginConfig` and clears dirty.
result: [pending]

### 4. Reset to Defaults with confirmation modal
expected: D-13 — Reset to Defaults button (ErrorRed) opens "Reset all settings to defaults?" modal. Reset reverts every tuning value to defaults but preserves HomeWorld. isDirty flips true; user must still click Save to persist.
result: [pending]

### 5. Rescan button disabled state
expected: While `plugin.ScanInProgress` is true, the Rescan button is greyed via `BeginDisabled`. After scan completes, button re-enables. Clicking it fires `plugin.RescanAsync` and the bought/listed/autoCollapsed dictionaries wipe on new scan result.
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
