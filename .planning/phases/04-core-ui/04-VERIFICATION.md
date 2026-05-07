---
phase: 04-core-ui
verified: 2026-05-07T08:45:00Z
status: human_needed
score: 8/8 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Open DailyRouteWindow in-game with an active scan result. Verify: server stops render as CollapsingHeaders in route order; each item row shows name, Buy price (PurchaseCyan), +profit/day (GilGold); bought checkbox dims name to CompletedGray; progress bars animate; profit tally updates as listed checkboxes change."
    expected: "Interactive route window matching the 04-UI-SPEC layout: status banner at top, progress section, route stops, item rows with checkboxes and price columns."
    why_human: "ImGui rendering cannot be verified programmatically without the game runtime; nyquist.sh validates source patterns but not visual output or click behavior."
  - test: "Mark all items at one stop as bought. Verify the stop CollapsingHeader auto-collapses exactly once and shows a checkmark prefix in CompletedGray. Un-check one item and re-check it; confirm collapse fires again."
    expected: "Auto-collapse fires on first all-bought frame via SetNextItemOpen(false, ImGuiCond.Always); per-stop flag prevents re-fire; un-check resets flag so subsequent re-completion triggers again."
    why_human: "SetNextItemOpen semantics require in-game ImGui frame timing to verify; cannot be observed from source patterns alone."
  - test: "Open ConfigWindow via gear icon (/xlsettings), change a slider, close the window, and select 'Save' in the unsaved-changes modal. Reopen ConfigWindow and verify the changed value persisted."
    expected: "D-12 snapshot/dirty/save/discard flow: closing while dirty triggers 'Save changes before closing?' modal; Save persists via SavePluginConfig and clears dirty flag."
    why_human: "Modal open/close flow and persistence require live game session to verify; OnClose re-entrancy (RESEARCH.md Pitfall 3) can only be observed at runtime."
  - test: "Click 'Reset to Defaults' in ConfigWindow; confirm confirmation modal appears with 'Reset all settings to defaults?'; click Reset; verify all tuning values revert to defaults but HomeWorld is unchanged."
    expected: "D-13 modal fires, RestoreDefaults preserves HomeWorld, isDirty flips to true, user must still click Save to persist."
    why_human: "Modal behavior and HomeWorld preservation require runtime interaction to verify end-to-end."
  - test: "Click 'Rescan Route' in DailyRouteWindow while a scan is in progress. Verify the button is greyed out (BeginDisabled). After scan completes, verify the button re-enables and clicking it triggers a rescan."
    expected: "plugin.ScanInProgress = true disables the Rescan button; _ = plugin.RescanAsync fires on click when enabled; boughtState/listedState/autoCollapsedStops wipe on new result."
    why_human: "ScanInProgress toggle and button enable/disable state require live plugin execution to verify."
---

# Phase 4: Core UI Verification Report

**Phase Goal:** Player sees today's route in an ImGui window, clicks through items, and tracks profit
**Verified:** 2026-05-07T08:45:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

The codebase fully implements the phase goal. All source artifacts are substantive and correctly wired. The nyquist validation gate (`tests/phase04_nyquist.sh`) passes 46/46 checks at exit 0. Human verification is required only for runtime rendering and interaction behavior that cannot be observed from source patterns alone.

### Observable Truths (Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DailyRouteWindow shows route with server stops, items, prices, and expected profit | VERIFIED | `DailyRouteWindow.cs:78` iterates `result.RouteStops`; `DrawRouteStop` renders `CollapsingHeader` per stop; `DrawItems` renders `Buy: {PurchasePrice:n0}` and `+{ExpectedDailyProfit:n0}/day` per item |
| 2 | Each item has a clickable checkbox to mark "bought" | VERIFIED | `DailyRouteWindow.cs:210` — `ImGui.Checkbox($"##bought-{item.ItemId}", ref bought)` wired to `boughtState[item.ItemId] = bought` on click |
| 3 | Home stop shows items to list with "listed" checkboxes | VERIFIED | `DailyRouteWindow.cs:249` — `ImGui.Checkbox($"##listed-{item.ItemId}", ref listed)` rendered only when `isHomeStop == true`; `listedState[item.ItemId] = listed` on click |
| 4 | Running profit tally updates in real time | VERIFIED | `DailyRouteWindow.cs:115-117` — LINQ `.Where(o => listedState.GetValueOrDefault(o.ItemId)).Sum(o => o.ExpectedDailyProfit)` computed each frame; rendered via `ImGui.TextColored(GilGold, $"Profit: {listedProfit:n0} / {totalProfit:n0} gil")` |
| 5 | Progress bar shows bought/total and listed/total completion | VERIFIED | `DailyRouteWindow.cs:139-148` — two `ImGui.ProgressBar` calls with `PushStyleColor(PlotHistogram, SuccessGreen)` and `PushStyleColor(PlotHistogram, PurchaseCyan)` using real fractions from boughtState/listedState counts |
| 6 | OOS items are visually distinct (color/icon) | VERIFIED | `DailyRouteWindow.cs:217,229-232` — `item.OutOfStock` renders name in OosOrange; `[OOS]` badge rendered in OosOrange via `ImGui.TextColored(OosOrange, "[OOS]")` |
| 7 | Completed server stops auto-collapse | VERIFIED | `DailyRouteWindow.cs:155-166` — `stop.Items.All(...)` predicate; `ImGui.SetNextItemOpen(false, ImGuiCond.Always)` fires on first all-bought frame; `autoCollapsedStops[stop.PurchaseSource]` flag prevents re-fire; resets on un-check |
| 8 | ConfigWindow exposes all settings from CONF-01 through CONF-09 | VERIFIED | `ConfigWindow.cs` — 7 CollapsingHeader sections cover all 14 Configuration properties; `BeginCombo` (HomeWorld), `SliderInt/Float` (ROI, sales velocity, caps, cache), `InputInt` (profit thresholds), `Checkbox` (RegionWide, categories, IncludeVendors, ShowOutOfStock, EnableShortagePredictor); nyquist confirms all 17 CONF-01..09 pattern assertions pass |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `tests/phase04_nyquist.sh` | Source-level validation gate for Phase 4 | VERIFIED | 202 lines, executable (0755), 46 assertions, exit 0 when all patterns present |
| `NamazuFlippers/UI/DailyRouteWindow.cs` | Window subclass rendering route, checkboxes, profit | VERIFIED | 261 lines, substantive — all 5 status states handled, checkboxes wired, profit tally live, auto-collapse wired |
| `NamazuFlippers/UI/ConfigWindow.cs` | Full settings editor with all CONF-01..09 widgets | VERIFIED | 399 lines, snapshot/dirty/discard plumbing, both modals, 18 isDirty=true assignments across all controls |
| `NamazuFlippers/UI/UiColors.cs` | 9 locked color Vector4 constants | VERIFIED | 20 lines, 9 `public static readonly Vector4` fields matching UI-SPEC exactly |
| `NamazuFlippers/UI/FirstRunWindow.cs` | Migrated to Window base class, no Func<bool> | VERIFIED | 104 lines, `class FirstRunWindow : Window`, constructor calls `base("Welcome to Namazu Flippers", ...)` |
| `NamazuFlippers/NamazuFlippers.cs` | WindowSystem owner with all wiring | VERIFIED | 196 lines, WindowSystem field, 3x AddWindow, named OnOpenConfigUi handler, public ScanInProgress/RescanAsync/OpenConfigWindow, clean Dispose |
| `NamazuFlippers/FirstRunWindow.cs` (root, deleted) | Must not exist | VERIFIED | `test ! -f NamazuFlippers/FirstRunWindow.cs` — confirmed absent |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `NamazuFlippers.cs` | `windowSystem.Draw` | `UiBuilder.Draw += windowSystem.Draw` | WIRED | Line 92 — subscription; Line 106 — Dispose unsubscription |
| `NamazuFlippers.cs` | `configWindow.IsOpen = true` | `private void OnOpenConfigUi()` | WIRED | Line 93 subscribe, 107 unsubscribe, 140 named handler — Pitfall 1 mitigation confirmed |
| `DailyRouteWindow.Draw()` | `plugin.LatestScanResult` | Frame-level read with null guard | WIRED | Line 54 reads result each frame; null guard at line 65 prevents rendering route on null/Empty/Error |
| `DailyRouteWindow.Rescan button` | `plugin.RescanAsync(CancellationToken.None)` | Button click handler | WIRED | Line 130 — `_ = plugin.RescanAsync(...)` fires on click; BeginDisabled/EndDisabled at lines 127-132 gates on `plugin.ScanInProgress` |
| `DailyRouteWindow.Settings button` | `plugin.OpenConfigWindow()` | Button click handler | WIRED | Line 137 — `plugin.OpenConfigWindow()` on click; plugin exposes it at NamazuFlippers.cs line 53 |
| `ConfigWindow.Save button` | `pluginInterface.SavePluginConfig(plugin.Configuration)` | Direct call | WIRED | Line 256 (Save button) and 293 (modal Save path) |
| `ConfigWindow.Reset button` | `BeginPopupModal("ConfirmReset##config")` | `ImGui.OpenPopup` on click | WIRED | Line 264 opens popup; Line 269 BeginPopupModal renders it |
| `ConfigWindow.OnClose` | Unsaved-changes modal | `showUnsavedModal = true` + `IsOpen = true` | WIRED | Lines 55-58 in OnClose; Lines 64-68 in Draw() — trigger moved to top of Draw per D-12/Pitfall 3 |
| `ConfigWindow.HomeWorld dropdown` | `WorldData.KnownWorlds` | `BeginCombo + Selectable` iteration | WIRED | Lines 77-91 — BeginCombo over `WorldData.KnownWorlds` array |
| `DailyRouteWindow` state wipe | `ReferenceEquals(result, lastSeenResult)` | Result-change detection | WIRED | Lines 69-75 — all three dicts cleared when result reference changes (D-09) |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `DailyRouteWindow.Draw()` | `result` (ScanEngineResult?) | `plugin.LatestScanResult` set at `NamazuFlippers.cs:177` inside `RunScanAsync` which calls `scanEngine.GetRouteAsync()` (Phase 3) | Yes — real DB/API-backed scan result | FLOWING |
| `DailyRouteWindow.DrawProgressSection` | `boughtCount`, `listedCount`, `listedProfit` | LINQ over `boughtState`/`listedState` dictionaries populated by Checkbox click handlers | Yes — real in-memory state from user interaction | FLOWING |
| `ConfigWindow.Draw()` | All control values | `plugin.Configuration.*` — live Configuration object loaded from `pluginInterface.GetPluginConfig()` at plugin startup | Yes — persisted config from disk | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| nyquist exit 0 (all 46 patterns) | `bash tests/phase04_nyquist.sh` | `Phase 04 Nyquist validation passed. EXIT:0` | PASS |
| nyquist executable | `test -x tests/phase04_nyquist.sh` | passes | PASS |
| DailyRouteWindow: CollapsingHeader per stop | `grep -c CollapsingHeader NamazuFlippers/UI/DailyRouteWindow.cs` | 1 call site (in DrawRouteStop) | PASS |
| DailyRouteWindow: two ProgressBar calls | `grep -c ImGui.ProgressBar NamazuFlippers/UI/DailyRouteWindow.cs` | 2 | PASS |
| ConfigWindow: 18 isDirty=true assignments | `grep -c "isDirty = true" NamazuFlippers/UI/ConfigWindow.cs` | 18 | PASS |
| UiColors: 9 static readonly Vector4 fields | `grep -c "public static readonly Vector4" NamazuFlippers/UI/UiColors.cs` | 9 | PASS |
| No ref bool CollapsingHeader overload | `grep -c "CollapsingHeader([^)]*,.*ref" NamazuFlippers/UI/DailyRouteWindow.cs` | 0 | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| UI-01 | 04-01-PLAN | DailyRouteWindow displays today's route: server stops in order, items to buy per stop with prices | SATISFIED | `DailyRouteWindow.cs` fully renders stops (CollapsingHeader per RouteStop), items (foreach stop.Items), and prices (PurchaseCyan Buy, GilGold profit). REQUIREMENTS.md checkbox not updated (documentation gap — see Anti-Patterns). |
| UI-02 | 04-02-PLAN | Each item has a checkbox to mark "bought" | SATISFIED | `##bought-{item.ItemId}` Checkbox wired to boughtState |
| UI-03 | 04-02-PLAN | Home stop shows items to list with "listed" checkboxes | SATISFIED | `##listed-{item.ItemId}` rendered only when isHomeStop |
| UI-04 | 04-02-PLAN | Running profit tally updates in real time | SATISFIED | LINQ sum over listedState computed per frame |
| UI-05 | 04-02-PLAN | Progress bars show completion | SATISFIED | Two ProgressBar calls with PlotHistogram color push, real fractions |
| UI-06 | 04-02-PLAN | OOS items visually distinct | SATISFIED | OosOrange item name + [OOS] badge |
| UI-07 | 04-02-PLAN | Completed server stops auto-collapse | SATISFIED | SetNextItemOpen(false, ImGuiCond.Always) + per-stop flag |
| UI-08 | 04-03-PLAN | ConfigWindow exposes all CONF-01..09 settings | SATISFIED | 7 CollapsingHeader sections, 14 Configuration properties wired, Save/Reset/modals implemented |
| CONF-01 | 04-03-PLAN | User can set home world | SATISFIED | BeginCombo over WorldData.KnownWorlds in ConfigWindow |
| CONF-02 | 04-03-PLAN | Profit thresholds UI | SATISFIED | PreferredRoi, MinProfitAmount, MinDesiredAvgPpu, MaxBudgetPerItem widgets |
| CONF-03 | 04-03-PLAN | Velocity floor UI | SATISFIED | MinSalesPerDay SliderFloat, MinSalesPerWeek SliderInt |
| CONF-04 | 04-03-PLAN | Region-wide toggle | SATISFIED | RegionWide Checkbox with tooltip |
| CONF-05 | 04-03-PLAN | Category filters UI | SATISFIED | Furniture/Collectibles/Glamour checkboxes via ApplyCategoryToggle |
| CONF-06 | 04-03-PLAN | Vendor/OOS toggles | SATISFIED | IncludeVendors, ShowOutOfStock checkboxes |
| CONF-07 | 04-03-PLAN | Session caps UI | SATISFIED | MaxItemsPerSession (1-20), MaxServersToVisit (1-15) sliders |
| CONF-08 | 04-03-PLAN | Cache duration UI | SATISFIED | CacheDurationHours slider (1-24) |
| CONF-09 | Phase 1 + 04-03 | Settings persist across sessions | SATISFIED | ConfigWindow calls `pluginInterface.SavePluginConfig(plugin.Configuration)` via Save button and unsaved-changes modal Save path |

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|---------|--------|
| `.planning/REQUIREMENTS.md` | UI-01 checkbox remains `[ ]` (Pending) even though implementation is complete and all other Phase 4 UI requirements were marked `[x]` | Warning | Documentation inconsistency only; does not affect code behavior. The REQUIREMENTS.md traceability table still shows `UI-01 | Phase 4 | Pending`. The code fully satisfies UI-01 — DailyRouteWindow renders route stops, items, and prices as required. This was a tracking omission in the docs commit (`5df943e` updated UI-02..07 but missed UI-01). |

No code anti-patterns found. No TODO/FIXME/PLACEHOLDER comments in any Phase 4 source files. No stub implementations. No orphaned artifacts. No empty return/null handlers in rendering paths.

### Human Verification Required

#### 1. DailyRouteWindow Route Rendering

**Test:** Open DailyRouteWindow in-game with an active scan result. Navigate each server stop CollapsingHeader. Click bought checkboxes; verify item name dims to gray. Check listed checkboxes on home stop; verify profit tally and progress bars animate.

**Expected:** Status banner reflects ScanEngineStatus; route stops appear as collapsible headers in route order; item rows show name, Buy: price in cyan, +profit/day in gold; bought dims name; progress bars fill as checkboxes are checked; profit tally sums listed items.

**Why human:** ImGui rendering, interactive checkbox behavior, and per-frame tally computation require the live game runtime (Dalamud + XIV). Nyquist validates source patterns only.

#### 2. Auto-Collapse Trigger and Reset

**Test:** Mark all items at one server stop as bought. Verify the CollapsingHeader force-closes showing `✓ [StopName]` in gray. Un-check one item; the stop re-opens. Re-check all items; confirm collapse fires again.

**Expected:** SetNextItemOpen fires exactly once on first all-bought frame; autoCollapsedStops flag prevents repeated firing; un-check resets flag so next completion re-triggers.

**Why human:** SetNextItemOpen semantics depend on ImGui's per-frame render order. The one-shot behavior and flag reset cannot be validated from source patterns.

#### 3. ConfigWindow Snapshot/Dirty/Discard Flow

**Test:** Open ConfigWindow (gear icon or in-window Settings button), change a slider value, then close the window without saving. Verify the "Save changes before closing?" modal appears with Save/Discard/Cancel. Choose Discard; reopen ConfigWindow and confirm the slider reverted to its original value.

**Expected:** OnClose() cancels the close when isDirty; modal renders with three buttons; Discard calls RestoreFrom(snapshot) reverting all changes; isDirty cleared; window closes.

**Why human:** The OnClose re-entrancy pattern (setting IsOpen = true in OnClose) and modal render order require runtime observation to confirm no one-frame flicker or double-close.

#### 4. Reset to Defaults Modal and HomeWorld Preservation

**Test:** In ConfigWindow, change HomeWorld via dropdown, then click "Reset to Defaults" (red button). Confirm the modal asks "Reset all settings to defaults?"; click Reset. Verify ROI and other tuning values revert to defaults (25, 10000, etc.) but HomeWorld retains the player's world, not an empty string.

**Expected:** RestoreDefaults() preserves HomeWorld; isDirty flips true after reset; user must click Save to persist; D-13 rationale holds.

**Why human:** HomeWorld preservation and the exact reset values require in-game inspection of the Configuration object post-reset.

#### 5. Rescan Button Disabled State and State Wipe

**Test:** Trigger a scan (slow enough to observe). While scanning, observe Rescan Route button is greyed out. After scan completes and new result loads, confirm boughtState is wiped (previously checked items unchecked) and the button re-enables.

**Expected:** plugin.ScanInProgress = true disables the button via BeginDisabled; new result reference triggers dict.Clear() on all three state dictionaries; bought checkboxes reset.

**Why human:** Scan timing, Interlocked state, and the mid-scan UI state require live plugin execution.

### Gaps Summary

No blocking gaps. All 8 roadmap success criteria are verified in code. The 5 human verification items are standard runtime behavior checks for an ImGui Dalamud plugin — they cannot be validated from source alone and require in-game UAT.

The only documentation artifact gap is the unchecked UI-01 checkbox in REQUIREMENTS.md (a tracking omission, not a code deficiency). This can be addressed in a follow-up commit updating the requirements tracker.

---

_Verified: 2026-05-07T08:45:00Z_
_Verifier: Claude (gsd-verifier)_
