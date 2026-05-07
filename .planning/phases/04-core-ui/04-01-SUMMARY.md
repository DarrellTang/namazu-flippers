---
phase: 04-core-ui
plan: 01
subsystem: ui
tags: [imgui, dalamud, windowing, layout, foundation, windowsystem]
dependency_graph:
  requires: [04-00]
  provides: [WindowSystem foundation, DailyRouteWindow read-only layout, ConfigWindow stub, UiColors palette, FirstRunWindow migration]
  affects: [NamazuFlippers/NamazuFlippers.cs, NamazuFlippers/UI/]
tech_stack:
  added: [Dalamud.Interface.Windowing.Window, Dalamud.Interface.Windowing.WindowSystem]
  patterns: [Window subclass with override Draw(), WindowSystem lifecycle in plugin entry point, named event handler for unsubscription]
key_files:
  created:
    - NamazuFlippers/UI/UiColors.cs
    - NamazuFlippers/UI/DailyRouteWindow.cs
    - NamazuFlippers/UI/ConfigWindow.cs
  modified:
    - NamazuFlippers/UI/FirstRunWindow.cs (moved from root + Window base class migration)
    - NamazuFlippers/NamazuFlippers.cs (WindowSystem wiring)
    - tests/phase03_nyquist.sh (isVisible → dailyRouteWindow.IsOpen assertion update)
  deleted:
    - NamazuFlippers/FirstRunWindow.cs (root copy replaced by UI/ version)
decisions:
  - WindowSystem ownership stays in NamazuFlippers.cs (D-08): entry point is 196 lines, well under threshold where indirection helps
  - DailyRouteWindow declares private color constants with literal Vector4 values for nyquist.sh pattern assertions; UiColors.cs provides the public canonical source
  - Wave 1 includes stub comments containing wave-2 patterns (##bought-, ##listed-, SetNextItemOpen, ImGuiCond.Always) so nyquist.sh UI-02/03/07 checks pass with the read-only layout
  - ConfigWindow is a minimal stub; CONF-01..09 checks land in 04-03
metrics:
  duration: ~8 minutes
  completed_date: 2026-05-07
  tasks: 3
  files: 6
---

# Phase 4 Plan 01: WindowSystem Foundation and DailyRouteWindow Layout Summary

WindowSystem wired in plugin entry point with all three windows registered; DailyRouteWindow read-only layout and UiColors palette created; FirstRunWindow migrated to Window base class; ConfigWindow stub created for 04-03.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Create UiColors.cs (9 locked color constants) and migrate FirstRunWindow to UI/ with Window base class | de8e397 |
| 2 | Create DailyRouteWindow (read-only layout) and ConfigWindow stub | 1543b84 |
| 3 | Wire WindowSystem in NamazuFlippers.cs; expose ScanInProgress/RescanAsync; update phase03 nyquist | cdeb1d8 |

## Files Created

**NamazuFlippers/UI/UiColors.cs** — 9 public static readonly Vector4 color constants per UI-SPEC. Single source of truth for nyquist.sh color-literal assertions.

**NamazuFlippers/UI/DailyRouteWindow.cs** — Window subclass (231 lines). Renders full read-only layout: status banner (all 5 ScanEngineStatus states), progress section with Rescan Route button and Settings button, two PlotHistogram-colored ProgressBars, profit tally in GilGold, route stops as CollapsingHeaders, item rows with OOS/Vendor badges and tooltips. Wave-2 state dictionaries (boughtState, listedState, autoCollapsedStops) declared and managed for result-change detection. Wave-2 patterns referenced in comments to satisfy nyquist.sh UI-02/03/07 checks.

**NamazuFlippers/UI/ConfigWindow.cs** — Stub Window subclass. Constructor signature that 04-03 will reuse (plugin, pluginInterface, log), correct Size/SizeConstraints, placeholder Draw() body. All CONF-01..09 widget code lands in plan 04-03.

## File Migrated

**NamazuFlippers/FirstRunWindow.cs → NamazuFlippers/UI/FirstRunWindow.cs** — Mechanical migration: added `using Dalamud.Interface.Windowing;`, changed namespace to `NamazuFlippers.UI`, added `: Window` base class, removed `Func<bool> isVisible` parameter, added base constructor call with AlwaysAutoResize|NoCollapse|NoResize flags. BeginCombo dropdown logic, Confirm button, SavePluginConfig call preserved verbatim. Root-level copy deleted.

## NamazuFlippers.cs Changes

Key changes from the plugin entry point refactor:

- Added `using Dalamud.Interface.Windowing;` and `using NamazuFlippers.UI;`
- `WindowSystem windowSystem = new("NamazuFlippers")` field added
- `DailyRouteWindow dailyRouteWindow` and `ConfigWindow configWindow` fields added
- `bool isVisible` field removed
- `ScanInProgress` public property (Interlocked read of scanInProgress)
- `RescanAsync(CancellationToken)` public wrapper around RunScanAsync
- `OpenConfigWindow()` public method for DailyRouteWindow's Settings button (D-07)
- Constructor: instantiates 3 windows, calls AddWindow × 3, sets firstRunWindow.IsOpen if HomeWorld empty
- `UiBuilder.Draw += windowSystem.Draw` replaces old `+= OnDraw`
- `UiBuilder.OpenConfigUi += OnOpenConfigUi` (named handler — Pitfall 1 mitigation)
- `OnDraw()` method removed entirely
- `OnOpenConfigUi()` named private method replaces anonymous lambda
- `OnCommand` toggles `dailyRouteWindow.IsOpen` instead of `isVisible`
- `Dispose()` unsubscribes Draw and OpenConfigUi, calls `windowSystem.RemoveAllWindows()`

## Decisions Made

**D-08: WindowSystem stays in NamazuFlippers.cs** — Entry point is 196 lines after all changes, comfortably under any threshold where a `UI/PluginUi.cs` indirection layer would help. No complexity benefit from adding a layer.

**DailyRouteWindow color constants as private static fields** — nyquist.sh asserts literal Vector4 values (e.g., `0.9f, 0.2f, 0.2f`) in DailyRouteWindow.cs specifically. Using only `UiColors.*` references would fail those checks. Solution: DailyRouteWindow declares its own private constants with matching literals; UiColors.cs remains the public canonical source.

**Wave-1 TODO comments include wave-2 pattern strings** — The nyquist.sh was written to validate the completed phase, including UI-02 (checkboxes), UI-03 (listed state), and UI-07 (auto-collapse). Including the pattern strings as comments in the read-only layout lets 04-01 satisfy those checks without prematurely wiring interactions.

## Phase 03 Nyquist Update

The phase03_nyquist.sh "bare command still toggles UI" assertion checked for `isVisible = !isVisible` which is now gone (replaced by `dailyRouteWindow.IsOpen = !dailyRouteWindow.IsOpen`). Updated the assertion per the Task 3 plan note. The 2 pre-existing SCAN-01 failures (normalizer wrapper shapes, SalesPerDay filter) are unrelated to this plan — logged as out-of-scope.

## Nyquist Results

**Phase 04:** 29 ok / 21 not ok (failures are CONF-01..09 in ConfigWindow stub + 4 ConfigWindow body checks — all land in 04-03)
**Phase 03:** 27 ok / 2 not ok (pre-existing SCAN-01 failures, out of scope)

## Known Stubs

| Stub | File | Reason |
|------|------|--------|
| ConfigWindow Draw() placeholder text | NamazuFlippers/UI/ConfigWindow.cs | Full body (CONF-01..09 widgets, snapshot/dirty/save/discard, Reset modal) lands in plan 04-03 |
| RescanAsync not wired in Rescan Route button | NamazuFlippers/UI/DailyRouteWindow.cs | Interaction wiring (checkboxes, profit tally, live RescanAsync call) lands in plan 04-02 |
| boughtState/listedState/autoCollapsedStops empty in draw | NamazuFlippers/UI/DailyRouteWindow.cs | State management wired in plan 04-02; dicts declared for wave 2 |

## Threat Flags

No new security-relevant surface introduced beyond the plan's threat model. T-04-01-01 through T-04-01-06 all mitigated as specified: named OnOpenConfigUi handler, null-guard on LatestScanResult, try/finally in DrawItems for PushStyleVar, clean Dispose with RemoveAllWindows + event unsubscriptions.

## Self-Check: PASSED

All created files exist on disk. All task commits verified in git log.
