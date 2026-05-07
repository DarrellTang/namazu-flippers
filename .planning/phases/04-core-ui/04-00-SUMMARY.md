---
phase: 04-core-ui
plan: "00"
subsystem: validation
tags: [nyquist, bash, validation, imgui, dalamud]
dependency_graph:
  requires: [tests/phase03_nyquist.sh]
  provides: [tests/phase04_nyquist.sh]
  affects: [04-01-PLAN, 04-02-PLAN, 04-03-PLAN]
tech_stack:
  added: []
  patterns: [bash-nyquist-validation]
key_files:
  created: [tests/phase04_nyquist.sh]
  modified: []
decisions:
  - "Helper functions copied verbatim from tests/phase03_nyquist.sh lines 1-87 (pass, fail, require_file, require_pattern, require_absent_pattern, require_order, require_all_patterns)"
  - "Color values stored as regex-escaped strings (e.g. 1\\.0f) per grep -E pattern convention; plan acceptance criterion 9 grep expects unescaped literals but functional behavior is correct"
  - "D-07 OpenConfigWindow second entry point asserted via two checks: plugin.OpenConfigWindow method existence and DailyRouteWindow Settings button call"
metrics:
  duration: "3 minutes"
  completed_date: "2026-05-07"
---

# Phase 4 Plan 00: Phase04 Nyquist Validation Script Summary

Source-level Nyquist validation script for UI-01..UI-08 and CONF-01..CONF-09 with regex-escaped grep patterns asserting ImGui calls and color Vector4 tokens in Phase 4 UI source files.

## What Was Built

`tests/phase04_nyquist.sh` — a 202-line executable bash script that validates Phase 4 UI source patterns via grep. It exits non-zero when any required ImGui call, color token, or configuration property widget is missing from the source files.

## Helper Functions

Copied verbatim from `tests/phase03_nyquist.sh` lines 1-87:

| Function | Lines (phase03) | Purpose |
|----------|----------------|---------|
| `pass()` | 9-11 | Print `ok - {label}` |
| `fail()` | 13-16 | Print `not ok - {label}`, increment failures |
| `require_file()` | 18-24 | Assert file exists |
| `require_pattern()` | 26-37 | Assert single grep -E pattern in file |
| `require_absent_pattern()` | 39-49 | Assert pattern NOT present |
| `require_order()` | 51-67 | Assert first pattern appears before second |
| `require_all_patterns()` | 69-87 | Assert all patterns present; report all missing |

## Pattern Assertion Count per Requirement

| Requirement | Assertions | Key Patterns |
|------------|-----------|-------------|
| UI-01 | 5 | WindowSystem, AddWindow, windowSystem.Draw, DailyRouteWindow:Window, CollapsingHeader, PurchaseSource, LatestScanResult |
| UI-02 | 1 (multi) | ##bought-, boughtState, ImGui.Checkbox |
| UI-03 | 1 (multi) | ##listed-, listedState |
| UI-04 | 1 (multi) | GilGold color literal, ExpectedDailyProfit |
| UI-05 | 3 | PlotHistogram+ProgressBar (multi), SuccessGreen 0.2f/0.8f/0.3f, PurchaseCyan 0.2f/0.85f/0.9f |
| UI-06 | 1 (multi) | OosOrange literal, [OOS], OutOfStock |
| UI-07 | 1 (multi) | SetNextItemOpen, autoCollapsedStops, ImGuiCond.Always |
| UI-08 | 8 | ConfigWindow:Window, SavePluginConfig, BeginPopupModal, "Reset to Defaults", "Save Settings", OpenConfigUi, OpenConfigWindow method, DailyRouteWindow Settings button |
| CONF-01..09 | 17 | BeginCombo, HomeWorld, PreferredRoi, MinProfitAmount, MinDesiredAvgPpu, MaxBudgetPerItem, MinSalesPerDay, MinSalesPerWeek, RegionWide, CategoryFilters, IncludeVendors, ShowOutOfStock, MaxItemsPerSession, MaxServersToVisit, CacheDurationHours, EnableShortagePredictor, isDirty |
| Color tokens | 6 | GilGold, OosOrange, ErrorRed, CompletedGray, CacheBlue, StaleAmber (all with exact Vector4 float values) |
| FirstRunWindow | 1 | class FirstRunWindow.*: Window (D-05, D-06) |
| Lambda safety | 1 | OnOpenConfigUi named method (RESEARCH.md Pitfall 1) |

## Initial Run Exit Code

Script exits **1** (49 checks failed) against current source tree — correct behavior for Wave 0.

First failure message: `not ok - NamazuFlippers/UI/DailyRouteWindow.cs exists`

This confirms the gating behavior: the script will flip to exit 0 only when all Phase 4 implementation plans (04-01, 04-02, 04-03) have landed their required source patterns.

## Deviations from Plan

### Minor: Color Literal Grep Escaping

**Found during:** Task 1 verification
**Issue:** Plan acceptance criterion 9 runs `grep -c '1\.0f, 0\.85f, 0\.1f\|...' tests/phase04_nyquist.sh` expecting ≥ 4 matches. The criterion assumes color values appear as literal `1.0f` in the script, but the script correctly stores them as regex-escaped `1\.0f` for use in `grep -E` pattern arguments.
**Fix:** No fix required — the script is correct. The color literals are present and functional as grep -E patterns. Acceptance criterion 9 fails on the exact grep command phrasing but all other acceptance criteria pass, and the functional behavior (script exits non-zero on missing patterns) is correct.
**Files modified:** None

## Self-Check

- `tests/phase04_nyquist.sh` exists: FOUND
- Commit `8c35b34` exists: FOUND
- `bash -n tests/phase04_nyquist.sh` syntax check: PASS
- `test -x tests/phase04_nyquist.sh` executable: PASS
- UI-01..UI-08 echo headers: 8 (PASS)
- CONF-01 echo header: 1 (PASS)
- OpenConfigWindow grep: PASS
- Config properties ≥ 13: 13 (PASS)
- Exit non-zero on missing sources: PASS (exit code 1, 49 failures)
