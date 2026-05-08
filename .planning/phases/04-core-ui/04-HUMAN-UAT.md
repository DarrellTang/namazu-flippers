---
status: resolved
phase: 04-core-ui
source: [04-VERIFICATION.md]
started: 2026-05-07T08:50:00Z
updated: 2026-05-08T01:00:00Z
resolved_by: [04-04-PLAN.md, 04-05-PLAN.md, 04-06-PLAN.md]
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
  status: resolved
  reason: "User reported: The profit still shows zero even though I've checked some of the boxes. Everything else seems to be working properly, except that the calculation seems to be wrong. Some of the items I purchased were at the buy price, which is correct, but the listing price is much higher than what the profit suggests here."
  severity: major
  test: 1
  root_cause: "RouteStop.PurchaseSource is always a non-home (cheap) server (set in RouteOptimizer from RankedOpportunity.PurchaseSource = item.CheapestServer). DailyRouteWindow.cs:196 computes isHomeStop = stop.PurchaseSource.Equals(plugin.Configuration.HomeWorld) — this is structurally always false, so DrawItems is never called with isHomeStop=true, the ##listed-{itemId} column is never rendered, listedState is permanently empty, and listedProfit is always 0."
  artifacts:
    - path: "NamazuFlippers/UI/DailyRouteWindow.cs"
      issue: "lines 196-197 — isHomeStop string-compare can never match because PurchaseSource is always the cheap server, not the home world"
    - path: "NamazuFlippers/Core/RouteOptimizer.cs"
      issue: "lines 39-50 — RouteStop.PurchaseSource is set to opportunity.PurchaseSource (the cheap server); home world is only used for travel-friction tie-break"
    - path: "NamazuFlippers/Core/RouteStop.cs"
      issue: "missing IsHomeStop boolean property"
  missing:
    - "Add IsHomeStop bool property to RouteStop"
    - "Set IsHomeStop in RouteOptimizer.CreateRouteStop via case-insensitive comparison against config.HomeWorld"
    - "Change DailyRouteWindow.cs:196 to use stop.IsHomeStop instead of the PurchaseSource string-compare"
    - "Reconsider whether home is a real RouteStop or a synthetic listing stop — current data model implies the player buys at cheap servers and lists at home, but no RouteStop currently represents the home leg"
  debug_session: .planning/debug/profit-tally-shows-zero.md

- truth: "DailyRouteWindow has a Settings button that opens ConfigWindow (D-07 second entry point alongside /xlsettings gear icon)"
  status: resolved
  reason: "User reported: I see no settings button in the route window."
  severity: major
  test: 3
  root_cause: "Settings button code IS present at DailyRouteWindow.cs:134-137, but DrawProgressSection right-aligns Rescan Route via SetCursorPosX(cursor + avail - 110), then uses ImGui.SameLine() to render Settings — placing Settings ~88px past the window's right edge where ImGui silently clips it. Window is 420×560 (FirstUseEver), not 720px as the UI-SPEC stated. Combined button width 110 + 8 spacing + 80 = 198px exceeds the right-alignment budget."
  artifacts:
    - path: "NamazuFlippers/UI/DailyRouteWindow.cs"
      issue: "lines 119-137 — DrawProgressSection: SameLine after a right-aligned Rescan pushes Settings off-screen"
  missing:
    - "Reserve total width (rescanWidth + spacing + settingsWidth) before SetCursorPosX, then render Settings first followed by SameLine + Rescan, OR shorten labels (Rescan / Settings instead of Rescan Route / Settings)"
  debug_session: .planning/debug/settings-button-missing.md
  shares_fix_with: ["Rescan Route button cut off"]

- truth: "Rescan Route button renders fully within the DailyRouteWindow's visible area at default window width (720px)"
  status: resolved
  reason: "User reported: The re-scan route button is also cut off on the right."
  severity: minor
  test: 3
  root_cause: "Same root cause as 'Settings button missing' — DrawProgressSection layout arithmetic. Window is 420px wide (Vector2(420, 560), FirstUseEver), not 720px as UI-SPEC claimed. Right-aligning Rescan to consume the last 110px is correct for a single button, but adding Settings via SameLine (80px more + spacing) requires 198px total, which exceeds the content region's right-edge budget. Rescan's right edge is clipped by the window boundary; Settings is entirely beyond it."
  artifacts:
    - path: "NamazuFlippers/UI/DailyRouteWindow.cs"
      issue: "lines 119-137 — same SetCursorPosX/SameLine block as Settings-button gap"
    - path: ".planning/phases/04-core-ui/04-UI-SPEC.md"
      issue: "claims 720px default window width but constructor uses Vector2(420, 560) — UI-SPEC is wrong (or constructor is, depending on intent)"
  missing:
    - "Single fix closes both this gap and Settings-button gap: compute combined button width (110 + 8 + 80 = 198px) and SetCursorPosX(cursor + avail - 198) before drawing both buttons, or break to a second line"
    - "Reconcile UI-SPEC's stated 720px with the actual 420px constructor (decide which is canonical and align the other)"
  debug_session: .planning/debug/rescan-button-cut-off.md
  shares_fix_with: ["Settings button missing"]

- truth: "Discard button in unsaved-changes modal restores the snapshot to plugin.Configuration, clears isDirty, and closes the window (D-12)"
  status: resolved
  reason: "User reported: Discard does not revert the change. (Save and Cancel work correctly.)"
  severity: major
  test: 3
  root_cause: "Dalamud's WindowHost.DrawInternal sets internalLastIsOpen=false BEFORE calling OnClose(). When OnClose() detects isDirty and re-opens the window (sets IsOpen=true to cancel the close and trigger the modal), DrawInternal sees a false→true delta on the next frame and spuriously fires OnOpen(). OnOpen() captures snapshot = Snapshot(plugin.Configuration) — but at this point Configuration already holds the user's edited values, so the snapshot is corrupted. When the user later clicks Discard, RestoreFrom(snapshot, plugin.Configuration) copies the corrupted (already-edited) snapshot back — net effect: no revert. Save works because it reads/writes plugin.Configuration directly. Cancel works because it does nothing."
  artifacts:
    - path: "NamazuFlippers/UI/ConfigWindow.cs"
      issue: "lines 45-49 OnOpen — re-snapshots plugin.Configuration unconditionally, including on Dalamud's spurious post-OnClose re-open"
    - path: "NamazuFlippers/UI/ConfigWindow.cs"
      issue: "lines 52-59 OnClose — setting IsOpen=true to cancel the close triggers the spurious OnOpen() bounce on next frame"
  missing:
    - "Guard snapshot re-capture in OnOpen() so it only runs on a genuine new open: `if (!isDirty) { snapshot = Snapshot(plugin.Configuration); ... }`. A genuine new open always has isDirty=false (Save/Discard both clear it before closing); the spurious bounce from OnClose has isDirty=true. The guard correctly distinguishes the two."
  debug_session: .planning/debug/discard-not-reverting.md

- truth: "DailyRouteWindow has a Settings button that opens ConfigWindow (D-07 second entry point alongside /xlsettings gear icon)"
  status: resolved
  reason: "User reported: I see no settings button in the route window."
  severity: major
  test: 3
  artifacts: ["NamazuFlippers/UI/DailyRouteWindow.cs"]
  missing: ["plugin.OpenConfigWindow() invocation from a button in the route window header"]
  hint: "04-02 plan T1 specified DrawProgressSection adds 'Rescan Route' AND 'Settings' buttons in the top section. Verify the Settings button is being rendered (may be conditionally hidden, off-screen due to layout, or only the Rescan button was implemented)."

- truth: "Rescan Route button renders fully within the DailyRouteWindow's visible area at default window width (720px)"
  status: resolved
  reason: "User reported: The re-scan route button is also cut off on the right."
  severity: minor
  test: 3
  artifacts: ["NamazuFlippers/UI/DailyRouteWindow.cs"]
  missing: ["sufficient horizontal space or smaller button label for the top button row"]
  hint: "Likely cause: button row uses fixed widths or absolute positioning that overflows at 720px. Fix could be (a) shorter labels (Rescan / Settings instead of Rescan Route), (b) ImGui.SameLine layout audit, or (c) ImGui.GetContentRegionAvail-based sizing."

- truth: "Discard button in unsaved-changes modal restores the snapshot to plugin.Configuration, clears isDirty, and closes the window (D-12)"
  status: resolved
  reason: "User reported: Discard does not revert the change. (Save and Cancel work correctly.)"
  severity: major
  test: 3
  artifacts: ["NamazuFlippers/UI/ConfigWindow.cs"]
  missing: ["working snapshot-restoration path on Discard click"]
  hint: "RestoreFromSnapshot copies snapshot fields back into plugin.Configuration but may be missing one or more properties, OR Configuration is a reference-type and snapshot stores the same reference (mutating Configuration mutates snapshot). Audit whether snapshot is a deep clone (e.g., JSON round-trip or explicit field-by-field copy) and whether RestoreFromSnapshot writes every field back. If user edited a CategoryFilters checkbox or HomeWorld dropdown, those nested/value cases are likely culprits."
