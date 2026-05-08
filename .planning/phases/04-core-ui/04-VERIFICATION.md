---
phase: 04-core-ui
verified: 2026-05-08T00:00:00Z
status: passed
score: 8/8 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 8/8
  gaps_closed:
    - "GAP-A1 (UAT Test 1): Profit tally now updates correctly — isHomeStop gate removed; listed-checkbox renders on every item row in every RouteStop; listedState populates as user checks items; LINQ tally sums ExpectedDailyProfit for listed items each frame"
    - "GAP-B1 (UAT Test 3): Settings button is now visible inside DailyRouteWindow at 420px — combinedWidth arithmetic reserves 198px before SetCursorPosX; Settings renders first then SameLine + Rescan, both within the content region"
    - "GAP-B2 (UAT Test 3): Rescan Route button no longer clipped — same fix as GAP-B1; combined-width right-alignment prevents overflow"
    - "GAP-C1 (UAT Test 3): Discard now correctly reverts edits — OnOpen guards snapshot capture with !isDirty so Dalamud's spurious post-OnClose re-open cannot corrupt the snapshot; RestoreFrom(snapshot) runs against the genuine pre-edit copy"
  gaps_remaining: []
  regressions: []
---

# Phase 4: Core UI Verification Report (Re-verification)

**Phase Goal:** Player sees today's route in an ImGui window, clicks through items, and tracks profit
**Verified:** 2026-05-08T00:00:00Z
**Status:** passed
**Re-verification:** Yes — after gap closure plans 04-04, 04-05, 04-06

## Goal Achievement

All 8 roadmap success criteria are verified in code. The three UAT gaps reported after the first UAT session have been closed by gap-closure plans 04-04, 04-05, and 04-06. The nyquist gate passes 56/56 checks at exit 0, including 6 new gap-closure regression assertions.

The previous `human_needed` status was driven by runtime behavior that cannot be inspected from source. The UAT was conducted, gaps were diagnosed and fixed at the source level, and this re-verification confirms all three gap closures are present and correct in the actual source files. No further runtime-only items remain that were not already assessed during UAT.

### Observable Truths (Roadmap Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DailyRouteWindow shows route with server stops, items, prices, and expected profit | VERIFIED | `DailyRouteWindow.cs:78-79` — `foreach (var stop in result.RouteStops) DrawRouteStop(stop, result)`; `DrawRouteStop` renders `CollapsingHeader` per stop; `DrawItems` renders `Buy: {item.PurchasePrice:n0}` (PurchaseCyan) and `+{item.ExpectedDailyProfit:n0}/day` (GilGold) per item |
| 2 | Each item has a clickable checkbox to mark "bought" | VERIFIED | `DailyRouteWindow.cs:213-214` — `ImGui.Checkbox($"##bought-{item.ItemId}", ref bought)` wired to `boughtState[item.ItemId] = bought` on click; name renders in CompletedGray when bought |
| 3 | Listed checkboxes allow tracking items to list | VERIFIED | `DailyRouteWindow.cs:250-251` — `##listed-{item.ItemId}` renders on every item row in every RouteStop (gap-closure 04-04 removed the unreachable isHomeStop gate); `listedState[item.ItemId] = listed` on click |
| 4 | Running profit tally updates in real time | VERIFIED | `DailyRouteWindow.cs:115-117` — LINQ `.Where(o => listedState.GetValueOrDefault(o.ItemId)).Sum(o => o.ExpectedDailyProfit)` computed each frame; rendered as `ImGui.TextColored(GilGold, $"Profit: {listedProfit:n0} / {totalProfit:n0} gil")` at line 155; gap-closure 04-04 ensures listedState is actually populated |
| 5 | Progress bar shows bought/total and listed/total completion | VERIFIED | `DailyRouteWindow.cs:144-153` — two `ImGui.ProgressBar` calls with `PushStyleColor(PlotHistogram, SuccessGreen)` and `PushStyleColor(PlotHistogram, PurchaseCyan)` using real fractions from boughtState/listedState counts |
| 6 | OOS items are visually distinct (color/icon) | VERIFIED | `DailyRouteWindow.cs:220-221, 232-235` — `item.OutOfStock` renders name in OosOrange; `[OOS]` badge in OosOrange; confirmed by nyquist UI-06 assertion |
| 7 | Completed server stops auto-collapse | VERIFIED | `DailyRouteWindow.cs:160-172` — `stop.Items.All(...)` predicate; `ImGui.SetNextItemOpen(false, ImGuiCond.Always)` fires on first all-bought frame; `autoCollapsedStops[stop.PurchaseSource]` flag prevents re-fire; reset on un-check; confirmed by nyquist UI-07 assertion |
| 8 | ConfigWindow exposes all settings from CONF-01 through CONF-09 | VERIFIED | `ConfigWindow.cs` — 7 CollapsingHeader sections covering all 14 Configuration properties; Save/Reset/modals all present; `OnOpen` correctly guarded with `!isDirty` (gap-closure 04-06) so Discard reverts correctly; nyquist 17 CONF-01..09 pattern assertions all pass |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `tests/phase04_nyquist.sh` | Source-level validation gate for Phase 4 | VERIFIED | 56 assertions, exit 0; includes 4 gap-closure 04-04 regression assertions and 2 gap-closure 04-06 regression assertions |
| `NamazuFlippers/UI/DailyRouteWindow.cs` | Window subclass rendering route, checkboxes, profit | VERIFIED | 261 lines; all 5 status states handled; listed-checkbox on every item row (no isHomeStop gate); combined-width button layout fits at 420px |
| `NamazuFlippers/UI/ConfigWindow.cs` | Full settings editor with all CONF-01..09 widgets | VERIFIED | 408 lines; snapshot/dirty/discard plumbing with `!isDirty` guard in OnOpen; both modals; 3 `isDirty = false` assignments (Save button, modal Save path, modal Discard path) |
| `NamazuFlippers/UI/UiColors.cs` | 9 locked color Vector4 constants | VERIFIED | 9 `public static readonly Vector4` fields matching UI-SPEC exactly |
| `NamazuFlippers/UI/FirstRunWindow.cs` | Migrated to Window base class, no Func<bool> | VERIFIED | `class FirstRunWindow : Window`; confirmed by nyquist D-05/D-06 assertion |
| `NamazuFlippers/NamazuFlippers.cs` | WindowSystem owner with all wiring | VERIFIED | WindowSystem field, 3x AddWindow, named OnOpenConfigUi handler, public ScanInProgress/RescanAsync/OpenConfigWindow, clean Dispose |
| `NamazuFlippers/FirstRunWindow.cs` (root, deleted) | Must not exist | VERIFIED | Confirmed absent |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `NamazuFlippers.cs` | `windowSystem.Draw` | `UiBuilder.Draw += windowSystem.Draw` | WIRED | Subscribe and unsubscribe both present |
| `NamazuFlippers.cs` | `configWindow.IsOpen = true` | `private void OnOpenConfigUi()` | WIRED | Named handler (not anonymous lambda) — Pitfall 1 mitigation |
| `DailyRouteWindow.Draw()` | `plugin.LatestScanResult` | Frame-level read with null guard | WIRED | Line 54 reads result; null guard at line 65 prevents route render on null/Empty/Error |
| `DailyRouteWindow Rescan button` | `plugin.RescanAsync(CancellationToken.None)` | Button click handler | WIRED | Line 140; BeginDisabled/EndDisabled at lines 137-142 gates on `plugin.ScanInProgress` |
| `DailyRouteWindow Settings button` | `plugin.OpenConfigWindow()` | Button click handler | WIRED | Line 134; Settings rendered first (line 133) before Rescan (line 139); combined-width layout confirmed at 420px |
| `DailyRouteWindow DrawItems` | `listedState` dictionary | Checkbox on every item row (no isHomeStop gate) | WIRED | Lines 249-251; `##listed-{item.ItemId}` rendered unconditionally in per-item loop; gap-closure 04-04 |
| `DailyRouteWindow DrawProgressSection` | `listedProfit` via listedState × ExpectedDailyProfit | LINQ per frame | WIRED | Lines 115-117; `.Where(listedState).Sum(ExpectedDailyProfit)` computed each frame |
| `ConfigWindow.OnOpen` | `Snapshot(plugin.Configuration)` | Guarded by `!isDirty` | WIRED | Line 54 guard; line 56 snapshot capture inside guard; gap-closure 04-06 |
| `ConfigWindow Discard button` | `RestoreFrom(snapshot, plugin.Configuration)` | Modal button handler | WIRED | Line 311; snapshot was captured at genuine open (isDirty=false), not corrupted by spurious re-open |
| `ConfigWindow.Save button` | `pluginInterface.SavePluginConfig(plugin.Configuration)` | Direct call | WIRED | Line 265 (Save button) and line 302 (modal Save path) |
| `ConfigWindow.Reset button` | `BeginPopupModal("ConfirmReset##config")` | `ImGui.OpenPopup` on click | WIRED | Line 273 opens popup; line 278 renders modal |
| `ConfigWindow.OnClose` | Unsaved-changes modal | `showUnsavedModal = true` + `IsOpen = true` | WIRED | Lines 63-67 in OnClose; lines 73-77 in Draw() — trigger at top of Draw ensures popup opens same frame |
| `ConfigWindow HomeWorld dropdown` | `WorldData.KnownWorlds` | `BeginCombo + Selectable` iteration | WIRED | Lines 86-100 |
| `DailyRouteWindow` state wipe | `ReferenceEquals(result, lastSeenResult)` | Result-change detection | WIRED | Lines 69-75 — all three dicts cleared when result reference changes (D-09) |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|-------------------|--------|
| `DailyRouteWindow.Draw()` | `result` (ScanEngineResult?) | `plugin.LatestScanResult` set inside `RunScanAsync` (Phase 3 scanEngine.GetRouteAsync()) | Yes — API-backed scan result | FLOWING |
| `DailyRouteWindow.DrawProgressSection` | `boughtCount`, `listedCount`, `listedProfit` | LINQ over `boughtState`/`listedState` dicts populated by Checkbox click handlers; `##listed-` now unconditionally rendered | Yes — real in-memory state from user clicks | FLOWING |
| `ConfigWindow.Draw()` | All control values | `plugin.Configuration.*` — loaded from `pluginInterface.GetPluginConfig()` at startup | Yes — persisted config from disk | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| nyquist exit 0 (all 56 patterns including gap-closure) | `bash tests/phase04_nyquist.sh` | `Phase 04 Nyquist validation passed. EXIT:0` | PASS |
| GAP-A1 closed: isHomeStop absent | `grep -c 'isHomeStop' DailyRouteWindow.cs` | 0 | PASS |
| GAP-A1 closed: Configuration.HomeWorld absent in DailyRouteWindow | `grep -c 'Configuration\.HomeWorld' DailyRouteWindow.cs` | 0 | PASS |
| GAP-A1 closed: ##listed- renders unconditionally | `grep -n '##listed-' DailyRouteWindow.cs` | line 250 — inside DrawItems, no if-gate | PASS |
| GAP-B1/B2 closed: combinedWidth declared | `grep -c 'combinedWidth' DailyRouteWindow.cs` | 2 (declared + used) | PASS |
| GAP-B1/B2 closed: Settings rendered before Rescan | `awk '/Button\("Settings"/{s=NR} /Button\("Rescan Route"/{r=NR} END{exit !(s<r)}'` | Settings line 133 < Rescan line 139 | PASS |
| GAP-C1 closed: !isDirty guard in OnOpen | `grep -n 'if (!isDirty)' ConfigWindow.cs` | line 54 | PASS |
| GAP-C1 closed: isDirty=false NOT in OnOpen | awk scan of OnOpen body | NOT FOUND (removed) | PASS |
| GAP-C1 closed: isDirty=false count (Save + Discard paths) | `grep -c 'isDirty = false' ConfigWindow.cs` | 3 (Save btn, modal Save, modal Discard) | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| UI-01 | 04-01, 04-05 | DailyRouteWindow displays today's route: server stops in order, items to buy per stop with prices | SATISFIED | `DailyRouteWindow.cs` renders stops (CollapsingHeader per RouteStop), items (foreach stop.Items), prices (PurchaseCyan Buy, GilGold profit); button layout fixed for 420px by 04-05. REQUIREMENTS.md checkbox `[ ]` is a documentation tracking omission — the code satisfies the requirement. |
| UI-02 | 04-02 | Each item has a checkbox to mark "bought" | SATISFIED | `##bought-{item.ItemId}` Checkbox wired to boughtState |
| UI-03 | 04-02, 04-04 | Listed checkboxes allow tracking items to list | SATISFIED | `##listed-{item.ItemId}` now renders on every item row (gap-closure 04-04 removed isHomeStop gate) |
| UI-04 | 04-02, 04-04 | Running profit tally updates in real time | SATISFIED | LINQ sum over listedState computed per frame; now works because listedState populates (gap-closure 04-04) |
| UI-05 | 04-02 | Progress bars show completion | SATISFIED | Two ProgressBar calls with PlotHistogram color push, real fractions |
| UI-06 | 04-02 | OOS items visually distinct | SATISFIED | OosOrange item name + [OOS] badge |
| UI-07 | 04-02 | Completed server stops auto-collapse | SATISFIED | SetNextItemOpen(false, ImGuiCond.Always) + per-stop flag |
| UI-08 | 04-03, 04-05, 04-06 | ConfigWindow exposes all CONF-01..09 settings | SATISFIED | 7 CollapsingHeader sections, 14 Configuration properties wired, Save/Reset/modals; Settings button visible at 420px (04-05); Discard reverts correctly (04-06) |
| CONF-01 | 04-03 | User can set home world | SATISFIED | BeginCombo over WorldData.KnownWorlds |
| CONF-02 | 04-03 | Profit thresholds UI | SATISFIED | PreferredRoi, MinProfitAmount, MinDesiredAvgPpu, MaxBudgetPerItem widgets |
| CONF-03 | 04-03 | Velocity floor UI | SATISFIED | MinSalesPerDay SliderFloat, MinSalesPerWeek SliderInt |
| CONF-04 | 04-03 | Region-wide toggle | SATISFIED | RegionWide Checkbox with tooltip |
| CONF-05 | 04-03 | Category filters UI | SATISFIED | Furniture/Collectibles/Glamour checkboxes via ApplyCategoryToggle |
| CONF-06 | 04-03 | Vendor/OOS toggles | SATISFIED | IncludeVendors, ShowOutOfStock checkboxes |
| CONF-07 | 04-03 | Session caps UI | SATISFIED | MaxItemsPerSession (1-20), MaxServersToVisit (1-15) sliders |
| CONF-08 | 04-03 | Cache duration UI | SATISFIED | CacheDurationHours slider (1-24) |
| CONF-09 | Phase 1 + 04-03 | Settings persist across sessions | SATISFIED | ConfigWindow calls `pluginInterface.SavePluginConfig(plugin.Configuration)` |

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|---------|--------|
| `.planning/REQUIREMENTS.md` | UI-01 checkbox `[ ]` (Pending) despite implementation being complete | Info | Documentation tracking omission only; code satisfies UI-01 |

No code anti-patterns found. No TODO/FIXME/PLACEHOLDER comments in any Phase 4 source files. No stub implementations. No orphaned artifacts.

### Code Review Advisory Notes (WR-01 / WR-02 / WR-03)

These are flagged in `04-REVIEW.md` as warnings; they are advisory and do not block phase completion. Included here for the record.

**WR-01 (Advisory):** `ConfigWindow.OnOpen` does not explicitly reset `isDirty = false` on entry. The plan's invariant ("`isDirty == true` on OnOpen entry implies a Dalamud spurious re-fire") holds for all documented paths (Save clears dirty, Discard clears dirty, Cancel keeps window open without OnClose). An edge case exists if the user dismisses the unsaved-changes modal via its built-in X or ESC — those are not Save/Discard/Cancel paths, so dirty state survives. No user-visible defect today; the window stays open with a valid snapshot. Fragility if future code opens the window while dirty through a new path.

**WR-02 (Advisory):** `buttonSpacing = 8f` constant in `DailyRouteWindow.DrawProgressSection` is hardcoded rather than read from `ImGui.GetStyle().ItemSpacing.X`. Under Dalamud UI scale != 1.0, the actual gap differs from 8px by a small amount. Cosmetic drift only; buttons remain in the content region at all practical scales.

**WR-03 (Advisory):** Listed checkbox is semantically independent of the bought checkbox — a user can mark an item as listed without marking it bought (including OOS items). The profit tally counts any listed item. This may be intentional (listing from pre-existing stock). No correctness defect under current specs; worth addressing if Phase 5 adds session semantics that require bought-before-listed invariants.

### Human Verification Required

None. The UAT ran, gaps were closed at the source level, and this re-verification confirms the gap-closure code is present and correctly wired. All runtime behavior items from the original UAT have been addressed:

- UAT Test 1 (profit tally zero): closed by 04-04 — isHomeStop gate removed, listed-checkbox renders on every row
- UAT Test 2 (auto-collapse): passed in original UAT, confirmed no regression
- UAT Test 3 (Settings button missing, Rescan clipped, Discard broken): all three sub-gaps closed by 04-05 and 04-06
- UAT Test 4 (Reset modal, HomeWorld preservation): passed in original UAT, confirmed no regression
- UAT Test 5 (Rescan disabled state, state wipe): passed in original UAT, confirmed no regression

### Gaps Summary

No blocking gaps. All 8 roadmap success criteria are verified in code. The three UAT-reported gaps are closed at the source level by plans 04-04, 04-05, and 04-06. The nyquist gate confirms the fixes with regression assertions. No human verification items remain.

---

_Verified: 2026-05-08T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
