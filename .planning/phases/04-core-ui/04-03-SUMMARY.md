---
phase: 04-core-ui
plan: 03
subsystem: ui
tags: [imgui, dalamud, config, settings, modal, snapshot-discard, dirty-flag]
dependency_graph:
  requires: [04-00, 04-01]
  provides: [ConfigWindow-full, UI-08, CONF-01..09]
  affects: [NamazuFlippers/UI/ConfigWindow.cs]
tech_stack:
  added: []
  patterns: [snapshot-clone, dirty-flag, BeginPopupModal, BeginCombo-over-fixed-list, Math.Max-clamp]
key_files:
  created: []
  modified:
    - NamazuFlippers/UI/ConfigWindow.cs
decisions:
  - "Both plan tasks written atomically in one file write per plan's explicit permission ('Both approaches satisfy acceptance')"
  - "showUnsavedModal trigger moved to top of Draw() so OpenPopup fires in the same frame as the flag set — avoids one-frame delay described in RESEARCH.md Pattern 6"
  - "HomeWorld preserved on Reset per D-13 rationale: player identity, not a tunable preference"
metrics:
  duration: ~4 min
  completed: 2026-05-07
  tasks_completed: 2
  files_modified: 1
---

# Phase 4 Plan 03: ConfigWindow Full Implementation Summary

Full settings editor replacing the 04-01 stub. Implements snapshot/dirty/save/discard pattern (D-12), Reset-to-Defaults modal (D-13), and renders one ImGui widget per Configuration property covering CONF-01 through CONF-09. All 46 phase04_nyquist.sh checks pass.

## Section Breakdown

| CollapsingHeader | Controls | CONF # |
|---|---|---|
| Home World | BeginCombo over WorldData.KnownWorlds | CONF-01 |
| Profit Thresholds | SliderInt (ROI 0-100), 3x InputInt (MinProfit, MinPpu, Budget) | CONF-02 |
| Velocity | SliderFloat (MinSalesPerDay 0-5 "%.2f"), SliderInt (MinSalesPerWeek 0-20) | CONF-03 |
| Filters | Checkbox (RegionWide), 3x Checkbox (Furniture/Collectibles/Glamour), Checkbox (IncludeVendors, ShowOutOfStock) | CONF-04/05/06 |
| Route Caps | SliderInt (MaxItemsPerSession 1-20), SliderInt (MaxServersToVisit 1-15) | CONF-07 |
| Cache | SliderInt (CacheDurationHours 1-24) | CONF-08 |
| Shortage Predictor (Phase 6 preview) | Checkbox (EnableShortagePredictor, visible/inert) | CONF-09 |

## Snapshot/Dirty/Discard Plumbing

- **OnOpen()**: calls `Snapshot(plugin.Configuration)` — deep clone via property copy + array Clone(). Resets `isDirty = false`. Recomputes `selectedWorldIndex` from current HomeWorld.
- **Any control change**: sets `isDirty = true` on the widget's return-bool.
- **OnClose()**: if `isDirty`, sets `IsOpen = true` (cancels close) and `showUnsavedModal = true`. Complies with RESEARCH.md Pitfall 3 (re-entrancy: the flag flip + modal open happen atomically in one frame cycle).
- **Save Settings button**: calls `pluginInterface.SavePluginConfig(plugin.Configuration)`, refreshes `snapshot = Snapshot(...)`, clears `isDirty`.
- **Discard (in unsaved modal)**: calls `RestoreFrom(snapshot, plugin.Configuration)`, clears `isDirty`, sets `IsOpen = false`.

## Reset Semantics

`RestoreDefaults()` resets all 14 tuning properties to their Configuration.cs initializer values, but **preserves HomeWorld**. Rationale: HomeWorld is player identity (which server you play on), not a search preference. A user who accidentally hits Reset should not lose their world selection. After Reset, `isDirty` flips to `true` — the user must click Save to persist the reset (D-13: no auto-save).

## Modal Open/Close Flow

### ConfirmReset##config
1. User clicks "Reset to Defaults" (red text button) → `ImGui.OpenPopup("ConfirmReset##config")` called immediately in same Draw frame
2. `BeginPopupModal` renders: "Reset all settings to defaults?" + [Reset] [Cancel]
3. [Reset] → `RestoreDefaults()`, `isDirty = true`, `CloseCurrentPopup()`
4. [Cancel] → `CloseCurrentPopup()`

### UnsavedChanges##config
1. `OnClose()` fires → sets `showUnsavedModal = true`, `IsOpen = true` (window stays open)
2. Next `Draw()` frame: `if (showUnsavedModal) { ImGui.OpenPopup("UnsavedChanges##config"); showUnsavedModal = false; }` — trigger at top of Draw ensures OpenPopup fires before EndPopup check
3. `BeginPopupModal` renders: "Save changes before closing?" + [Save] [Discard] [Cancel]
4. [Save] → SavePluginConfig, refresh snapshot, `isDirty = false`, `IsOpen = false`, CloseCurrentPopup
5. [Discard] → RestoreFrom(snapshot), `isDirty = false`, `IsOpen = false`, CloseCurrentPopup
6. [Cancel] → `IsOpen = true`, CloseCurrentPopup (window stays open)

## phase04_nyquist.sh Results After This Plan

```
Phase 04 Nyquist validation passed.
46/46 checks ok — exit 0
```

Wave 2 is now fully complete (04-02 + 04-03 both merged on main). Full nyquist exits 0.

## Deviations from Plan

**1. [Rule 3 - Implementation] Atomic write covering both tasks**

The plan split implementation into Task 1 (CONF-01..06) and Task 2 (Route Caps, Cache, modals) but explicitly stated: "The executor may also choose to write the entire file in one go and split mentally; the pattern split is for plan readability. Both approaches satisfy acceptance." Both tasks were satisfied by a single file write committed as Task 1. Task 2 verification was run against the same commit. No behavioral deviation.

**2. [Rule 2 - Missing critical] showUnsavedModal trigger placement**

The plan's modal pattern showed the `showUnsavedModal` trigger block at the bottom of Draw(). Moved it to the top of Draw() so `OpenPopup` fires in the same frame the flag is set (before any other content renders). RESEARCH.md Pattern 6 notes that `OpenPopup` must be called in the same frame as `BeginPopupModal` — placing the trigger at the bottom could miss the popup open by one frame if early returns existed.

## Known Stubs

None. All 14 Configuration properties have wired widgets. EnableShortagePredictor is intentionally rendered but inert in Phase 4 — documented in the CollapsingHeader label ("Phase 6 preview") and `// inert` inline comment. The bool is persisted correctly; Phase 6 wires the API call.

## Threat Flags

None. No new network endpoints, auth paths, file access patterns, or schema changes introduced. All inputs validated per STRIDE register (T-04-03-01 through T-04-03-08): InputInt results clamped via Math.Max(0), SliderInt/Float clamped via Math.Clamp, HomeWorld constrained to WorldData.KnownWorlds list.

## Self-Check: PASSED
