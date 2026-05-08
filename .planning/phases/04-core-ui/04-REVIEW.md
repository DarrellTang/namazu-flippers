---
phase: 04-core-ui
reviewed: 2026-05-07T00:00:00Z
depth: standard
files_reviewed: 3
files_reviewed_list:
  - NamazuFlippers/UI/ConfigWindow.cs
  - NamazuFlippers/UI/DailyRouteWindow.cs
  - tests/phase04_nyquist.sh
findings:
  critical: 0
  warning: 3
  info: 4
  total: 7
status: issues_found
---

# Phase 04 Gap-Closure: Code Review Report

**Reviewed:** 2026-05-07
**Depth:** standard
**Diff Base:** c3d0471
**Files Reviewed:** 3
**Status:** issues_found

## Summary

Reviewed the three files affected by gap-closure plans 04-04, 04-05, and 04-06. The
substantive changes are correct as written:

- 04-04 (DailyRouteWindow): The `isHomeStop` gate was removed; the listed checkbox now
  renders on every item row, the profit tally sums `ExpectedDailyProfit` over items
  with `listedState[itemId] == true`, and `Configuration.HomeWorld` is no longer
  referenced for stop classification. Trace through `Draw` -> `DrawRouteStop` -> `DrawItems`
  confirms the listed checkbox is rendered unconditionally inside the per-item loop.
- 04-05 (DrawProgressSection): `combinedWidth = 110 + 8 + 80 = 198px` arithmetic is
  correct for the locked 420px window. Settings is rendered first, `SameLine()` is
  inserted, Rescan is rendered second. Both fit inside the content region.
- 04-06 (ConfigWindow.OnOpen): The `if (!isDirty)` guard and the comment explaining
  the Dalamud spurious-OnOpen scenario are present. `selectedWorldIndex` re-sync sits
  inside the guard alongside the snapshot capture.

No blocker bugs, no security issues. Three warnings flag genuine fragility (one of
which is a real correctness gap on paths that bypass the unsaved-changes modal).
Four info items cover code quality and regression-test rigor.

## Warnings

### WR-01: ConfigWindow.OnOpen no longer resets `isDirty = false`, leaving a path where stale dirty state survives a fresh open

**File:** `NamazuFlippers/UI/ConfigWindow.cs:45-59`
**Issue:** The original `OnOpen` unconditionally set `isDirty = false`. The new
implementation only re-snapshots when `!isDirty` and never explicitly clears the flag.
The plan justifies removing the reset by claiming "Save and Discard both clear it before
closing, and Cancel keeps the window open without dispatching OnClose." That claim
holds for the explicit modal buttons, but it does **not** cover every dismissal path.
Specifically, the unsaved-changes popup is opened with `BeginPopupModal(... ref unsavedOpen ...)`
on line 296. ImGui populates the popup with a built-in close button (the X glyph in the
title bar) wired to the `unsavedOpen` ref. If the user dismisses the modal via that X
(or via ESC, which also flips `unsavedOpen` false), none of the Save / Discard / Cancel
branches run. `IsOpen` is still `true` from line 65, so the window stays open with
`isDirty == true` and `snapshot` pristine — that part is fine. But because the spurious
OnOpen scenario is no longer the only way `OnOpen` can fire while `isDirty == true`, the
window will continue to honor the stale snapshot for the rest of its lifetime. A user who
then triggers an external `OpenConfigWindow()` (e.g. via the gear icon while the window is
already open — a no-op) is fine, but a user whose flow ends with a Save click after such a
modal-X dismissal is also fine, because Save clears `isDirty` itself. The actual user-visible
defect is narrower than it sounds, but the invariant the plan relies on ("`isDirty == true`
on OnOpen entry implies a Dalamud spurious re-fire") is not actually enforced anywhere in
the code, so a future change that introduces a new path to set `IsOpen = true` while dirty
will silently regress the snapshot/discard contract.
**Fix:** Either tighten `OnClose` so the only way to leave the modal alive is through one
of the three explicit branches (e.g. set `unsavedOpen` to a sentinel and re-trigger the popup
on every Draw frame until a branch fires), or document this invariant with a runtime assertion:
```csharp
public override void OnOpen()
{
    if (!isDirty)
    {
        snapshot = Snapshot(plugin.Configuration);
        selectedWorldIndex = Array.IndexOf(WorldData.KnownWorlds, plugin.Configuration.HomeWorld);
    }
    else
    {
        // Spurious Dalamud re-fire after OnClose Cancel — verify snapshot is non-null.
        // If snapshot is null while isDirty is true, we have a logic bug elsewhere.
        log.Warning("ConfigWindow.OnOpen fired with isDirty=true; preserving snapshot.");
    }
}
```

### WR-02: `buttonSpacing` constant 8f drifts from runtime ImGui style under Dalamud UI scaling

**File:** `NamazuFlippers/UI/DailyRouteWindow.cs:124-128`
**Issue:** `combinedWidth = rescanWidth + buttonSpacing + settingsWidth` hardcodes
`buttonSpacing = 8f` with the comment "ImGui default item spacing." Dalamud applies
a global UI scale to ImGui style, and players can configure that scale (typically
0.85x to 1.5x). When scale is non-1.0, the actual `ItemSpacing.x` between Settings
and Rescan is ~`8 * scale`, so the right-alignment math is off by `8 * (scale - 1)`
pixels. At scale=1.5 that's a 4px discrepancy — Rescan extends past the right edge or
sits with a visible gap. Cosmetic, but it is the exact failure mode the plan was
trying to avoid.
**Fix:** Query the live style so the math tracks the actual spacing the next `SameLine()`
will insert:
```csharp
var buttonSpacing = ImGui.GetStyle().ItemSpacing.X;
var avail = ImGui.GetContentRegionAvail().X;
var combinedWidth = rescanWidth + buttonSpacing + settingsWidth;
```

### WR-03: Listed checkbox semantically permits invalid states (listed without bought, listed on OOS items) and the profit tally trusts them

**File:** `NamazuFlippers/UI/DailyRouteWindow.cs:115-117, 248-251`
**Issue:** With the `isHomeStop` gate gone (correct fix per the plan), every item now
renders both `##bought-{itemId}` and `##listed-{itemId}`. The listed checkbox is
independent of the bought state — a user can tick "listed" without ever ticking
"bought", or tick "listed" on an item flagged `OutOfStock` (which by definition the
user cannot buy). The profit tally on line 115-117 sums `ExpectedDailyProfit` over
every item where `listedState[itemId] == true`, regardless of bought state. A
mistaken click on the listed box for an OOS item inflates the displayed profit and
the listed-progress bar fraction. The OOS visual badge is still present, but nothing
disables the checkbox.
**Fix:** Either gate the listed checkbox on `boughtState[itemId] == true` (semantic
prerequisite), or at minimum exclude OOS items from the profit tally and use
`BeginDisabled()`/`EndDisabled()` around the listed checkbox when not bought:
```csharp
var bought = boughtState.GetValueOrDefault(item.ItemId);
ImGui.SameLine();
if (!bought) ImGui.BeginDisabled();
var listed = listedState.GetValueOrDefault(item.ItemId);
if (ImGui.Checkbox($"##listed-{item.ItemId}", ref listed))
    listedState[item.ItemId] = listed;
if (!bought) ImGui.EndDisabled();
```
And in `DrawProgressSection`:
```csharp
var listedProfit = result?.Opportunities
    .Where(o => listedState.GetValueOrDefault(o.ItemId)
             && boughtState.GetValueOrDefault(o.ItemId))
    .Sum(o => o.ExpectedDailyProfit) ?? 0;
```
If the plan deliberately allows listing without buying (e.g., the user lists from
a stockpile they already had before this scan), this is acceptable as designed —
in that case, leave the code alone but add a comment documenting the intent.

## Info

### IN-01: `phase04_nyquist.sh` regression checks are pattern-existence only, not structural

**File:** `tests/phase04_nyquist.sh:213-219`
**Issue:** The two new checks for the `OnOpen` snapshot guard are independent
`require_pattern` calls — one matches `if (!isDirty)` anywhere in the file, the other
matches `snapshot = Snapshot(plugin.Configuration)` anywhere in the file. The latter
pattern matches three sites in `ConfigWindow.cs` (the `OnOpen` guard at line 56, the
Save handler at line 266, and the modal Save handler at line 303), so deleting the
guarded `OnOpen` snapshot would not fail this test as long as the Save handler
remains. The regression assertion does not enforce the structural relationship
(snapshot call inside the if-block).
**Fix:** Add a multi-line/PCRE check that ties the guard and the call together, e.g.
```bash
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" \
  "if \(!isDirty\)\s*\{\s*snapshot = Snapshot" \
  "OnOpen guard wraps snapshot capture in same block (gap-closure 04-06)"
```
or use `pcregrep -M` for a robust multi-line match.

### IN-02: `require_absent_pattern` for `Configuration\.HomeWorld` is an over-broad ban

**File:** `tests/phase04_nyquist.sh:202-204`
**Issue:** The check fails the test if `Configuration.HomeWorld` appears anywhere in
`DailyRouteWindow.cs`. A future legitimate use (e.g. displaying the user's home world
in the status banner: "Routing to Adamantoise") would fail this regression test even
though it has nothing to do with the `isHomeStop` gate. The intent is "no
home-world-based stop classification," but the test enforces "no `Configuration.HomeWorld`
reference at all."
**Fix:** Narrow the ban to the actual anti-pattern:
```bash
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "PurchaseSource\.Equals\(.*Configuration\.HomeWorld" \
  "isHomeStop string-compare against Configuration.HomeWorld is gone"
```

### IN-03: `require_absent_pattern` regex `isHomeStop\s*=` matches equality operators

**File:** `tests/phase04_nyquist.sh:199-201`
**Issue:** The regex `isHomeStop\s*=` matches `isHomeStop =`, `isHomeStop=`, AND
`isHomeStop ==` (the trailing `=` of `==`). Today the test passes because
`isHomeStop` does not appear at all, but if a future contributor wrote
`if (isHomeStop == true)` for some new purpose, this assertion would fail with a
misleading message about "string-compare assignment."
**Fix:** Anchor the assignment intent:
```bash
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "isHomeStop\s*=[^=]" \
  "isHomeStop assignment is gone (gap-closure 04-04)"
```

### IN-04: Trailing separator after early-return when result is null/Empty/Error

**File:** `NamazuFlippers/UI/DailyRouteWindow.cs:58-66`
**Issue:** The Draw flow renders Status banner, separator, ProgressSection,
separator, then early-returns for null/Empty/Error. The second separator dangles
below an empty progress section with no content following it. Cosmetic only —
the separator just sits at the bottom of the window content, not floating in
space. Pre-existing pattern, not introduced by gap closure, but worth noting
during a layout review.
**Fix:** Move the early-return ahead of the second separator, or guard the
separator on having route stops to render:
```csharp
DrawStatusBanner(result);
ImGui.Separator();
DrawProgressSection(result);
if (result == null || result.Status == ScanEngineStatus.Empty || result.Status == ScanEngineStatus.Error)
    return;
ImGui.Separator();
// ... rest unchanged
```

---

_Reviewed: 2026-05-07_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
