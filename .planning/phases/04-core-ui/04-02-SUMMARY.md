---
phase: 04-core-ui
plan: 02
subsystem: UI
tags: [imgui, dalamud, interactions, profit-tally, auto-collapse, checkboxes]
dependency_graph:
  requires: [04-01]
  provides: [interactive-route-window]
  affects: [DailyRouteWindow.cs]
tech_stack:
  added: [System.Linq, System.Threading]
  patterns: [bought-listed-checkbox-state, profit-tally-linq, progress-bar-plothistogram, auto-collapse-setnextitemopen]
key_files:
  modified:
    - NamazuFlippers/UI/DailyRouteWindow.cs
decisions:
  - "Settings button placed after Rescan Route on same row using SameLine(); avail-based cursor positioning pushes Rescan to right edge"
  - "DrawItems tooltip hovers on item name (after bought checkbox), not on [OOS] badge, matching natural read order"
  - "pushColor flag unifies vendor-stop VendorCyan and allBought CompletedGray into one PushStyleColor/PopStyleColor pair with no early-return risk"
metrics:
  duration: ~12 min
  completed_date: "2026-05-07"
requirements: [UI-02, UI-03, UI-04, UI-05, UI-06, UI-07]
---

# Phase 4 Plan 02: Interactive Route Window Summary

Wire interactions onto the DailyRouteWindow scaffold from 04-01: bought/listed checkboxes with live state, profit tally via LINQ over listedState, progress bar real fractions with PlotHistogram color push, auto-collapse via SetNextItemOpen on first completion frame, and Rescan/Settings button hookups.

## Method-Level Diff Summary

### DrawProgressSection
- **Before:** hardcoded `boughtCount = 0`, `listedCount = 0`, `listedProfit = 0`; Rescan button body empty; Settings button used `GetWindowWidth()` cursor math
- **After:** LINQ Count/Sum from boughtState/listedState over result.Opportunities; `_ = plugin.RescanAsync(CancellationToken.None)` on click; Settings calls `plugin.OpenConfigWindow()`; both buttons on same text row using `avail - buttonWidth` cursor positioning

### DrawItems
- **Before:** read-only name rendering (OosOrange or plain Text); no checkboxes
- **After:** `ImGui.Checkbox($"##bought-{item.ItemId}", ref bought)` left of name; CompletedGray on bought name (takes priority over OosOrange); tooltip moved to item name hover; `##listed-{item.ItemId}` + `List: {HomePrice:n0}` rendered only when `isHomeStop == true`

### DrawRouteStop
- **Before:** auto-collapse flag tracked but `SetNextItemOpen` commented out; vendor/non-vendor split into two if-branches with different color handling
- **After:** `ImGui.SetNextItemOpen(false, ImGuiCond.Always)` fires on first frame where `allBought` is true; flag resets when `allBought` is false; unified `pushColor` bool collapses vendor and allBought cases into one PushStyleColor/PopStyleColor pair; checkmark prefix `✓` in label when allBought

### Draw (unchanged by this plan)
- State-wipe block (`ReferenceEquals(result, lastSeenResult)` → clear three dicts) was already present from 04-01 and is in the correct position: before the `foreach` over RouteStops, after the null/Empty/Error guard.

## Auto-Collapse Mechanics

`SetNextItemOpen(false, ImGuiCond.Always)` is called exactly when:
1. `stop.Items.Count > 0` (no-item stops never trigger)
2. All items satisfy `boughtState.GetValueOrDefault(item.ItemId) == true`
3. `autoCollapsedStops.GetValueOrDefault(stop.PurchaseSource) == false` (first completion frame only)

After firing, `autoCollapsedStops[stop.PurchaseSource] = true` is set immediately. On any subsequent frame where `allBought` is false (user un-checks an item), the flag resets to false so the next re-completion re-triggers collapse. This satisfies Pitfall 2 from RESEARCH.md.

## State-Wipe Location

In `Draw()`, the block:
```csharp
if (!ReferenceEquals(result, lastSeenResult))
{
    boughtState.Clear();
    listedState.Clear();
    autoCollapsedStops.Clear();
    lastSeenResult = result;
}
```
executes after the null/Empty/Error early return but before `foreach (var stop in result.RouteStops) DrawRouteStop(...)`. This ensures the wipe takes effect on the same frame as a new result, so no stale checkbox state is rendered.

## Profit Tally Formula

```csharp
var listedProfit = result?.Opportunities
    .Where(o => listedState.GetValueOrDefault(o.ItemId))
    .Sum(o => o.ExpectedDailyProfit) ?? 0;
```

Rendered as: `ImGui.TextColored(GilGold, $"Profit: {listedProfit:n0} / {totalProfit:n0} gil")`

## phase04_nyquist.sh Results After This Plan

| Section | ok | not ok | Notes |
|---------|-----|--------|-------|
| File existence | 4 | 0 | |
| UI-01 | 5 | 0 | |
| UI-02 | 1 | 0 | |
| UI-03 | 1 | 0 | |
| UI-04 | 1 | 0 | |
| UI-05 | 3 | 0 | |
| UI-06 | 1 | 0 | |
| UI-07 | 1 | 0 | |
| UI-08 | 3 | 4 | ConfigWindow — 04-03 not yet run |
| CONF-01..09 | 0 | 17 | ConfigWindow — 04-03 not yet run |
| Color tokens | 6 | 0 | |
| FirstRunWindow migration | 1 | 0 | |
| Lambda safety | 1 | 0 | |
| **Total** | **28** | **21** | All 21 failures are ConfigWindow (04-03) |

## Pattern Divergences from 04-PATTERNS.md

None. The implementation follows all documented patterns exactly:
- `##bought-{item.ItemId}` / `##listed-{item.ItemId}` key format matches
- `try/finally` around `PushStyleVar(ItemSpacing)` in DrawItems
- Single-string `CollapsingHeader` overload (no `ref bool`)
- `SetNextItemOpen(false, ImGuiCond.Always)` before CollapsingHeader
- GilGold TextColored for profit tally

## Deviations from Plan

None. Plan executed exactly as written.

## Threat Surface Scan

No new network endpoints, auth paths, or file access patterns introduced. The Rescan button calling `plugin.RescanAsync` routes through the existing `Interlocked.Exchange`-guarded `RunScanAsync` (T-04-02-01 already mitigated by Phase 3 SCAN-04 invariant). The `BeginDisabled` wrapper around the Rescan button satisfies T-04-02-01 at the UI layer. `SetNextItemOpen` one-shot pattern satisfies T-04-02-02.

## Self-Check: PASSED

- NamazuFlippers/UI/DailyRouteWindow.cs: FOUND
- Commit eb463bc (Task 1): FOUND
- Commit 8138365 (Task 2): FOUND
- UI-01 through UI-07 nyquist sections: all pass
- No ref bool CollapsingHeader overload: confirmed absent
- State-wipe block clears all 3 dicts: confirmed (3 Clear() calls)
