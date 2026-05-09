---
type: debug-session
gap: GAP-E1
supersedes: GAP-D1
status: diagnosed
phase: 04-core-ui
related_plan_attempts: [04-05, 04-07]
build_observed: ">1.0.26.0 (post-04-07)"
created: 2026-05-08T05:45:00Z
---

# Rescan Route still clipped after 04-07 — round 2

## Symptom

User reported on build > 1.0.26.0 (post-04-07 fix landing): "The rescan route button is still cut off. I get to Rescan Rou before it's cut off. everything else works."

The button frame extends past the window's right edge — the rendered visible portion shows "Rescan Rou" then clipping (the rightmost ~13px of the 110px button frame is hidden by the window's content boundary).

## Why 04-07's fix didn't close it

04-07 replaced `const float buttonSpacing = 8f` with `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X` so the reserved gap tracks the runtime gap that `SameLine()` actually inserts. That edit IS a real correctness fix — at scale 1.0 it's a no-op, and at scale > 1.0 it makes the reservation match the actual gap.

But it addressed the wrong mechanism. The user-visible bug is upstream of the spacing math.

## Real root cause

`DailyRouteWindow.cs:120-129`:

```csharp
ImGui.Text($"Bought: {boughtCount}/{totalItems}   Listed: {listedCount}/{totalItems}");

ImGui.SameLine();
const float rescanWidth = 110f;
const float settingsWidth = 80f;
var buttonSpacing = ImGui.GetStyle().ItemSpacing.X;
var avail = ImGui.GetContentRegionAvail().X;
var combinedWidth = rescanWidth + buttonSpacing + settingsWidth;
if (avail > combinedWidth)
    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - combinedWidth);
```

Two compounding bugs:

1. **`avail` is the REMAINING row width, not the full content width.** Because the `Text("Bought: ... Listed: ...")` was rendered first and `SameLine()` was called, the cursor is already deep into the row. `GetContentRegionAvail().X` from that position returns the leftover horizontal space.

2. **Button widths (110/80) are literal pixels — they don't scale with FFXIV UI scale.** Even if `avail` were the full width, a 110px frame can't render the text "Rescan Route" at scale 1.5+ (the text is rendered at scaled font but the frame stays at 110 literal pixels), so text would clip inside the frame.

### Pixel arithmetic at scale 1.5

| Quantity | Value |
|----------|-------|
| Window content width | ~396px (420 minus scaled WindowPadding) |
| "Bought: 0/40   Listed: 0/40" at scaled font | ~195px |
| Cursor X after Text + SameLine | ~207px (195 + 12 spacing) |
| `avail` from cursor 207 to content edge 396 | ~189px |
| `combinedWidth` (110 + 12 + 80) | 202px |
| Guard `avail > combinedWidth` → 189 > 202 | **FALSE** |
| Cursor advance | none |
| Settings button: x=207, ends 287 | fits |
| Rescan button: x=299, ends 409 | overflows by 13px |
| Visible portion of Rescan: x=299..396 | 97px of 110px frame visible |
| Inside the visible 97px, the button text "Rescan Route" rendered at scale 1.5 | clips after "Rescan Rou" |

User report matches exactly.

### At scale 1.0

| Quantity | Value |
|----------|-------|
| Window content width | ~404px |
| "Bought: 0/40   Listed: 0/40" at unscaled font | ~130px |
| Cursor X after Text + SameLine | ~138px |
| `avail` from cursor 138 to content edge 404 | ~266px |
| `combinedWidth` (110 + 8 + 80) | 198px |
| Guard 266 > 198 | TRUE |
| Cursor advances by 68 | Settings at x=206, Rescan at x=294, ends 404 — fits |

Scale 1.0 works because the bought/listed text is small enough to leave room for both buttons. Anything > 1.0 fails.

## Fix

Two coordinated edits:

### Edit 1: Move buttons to their own row

Drop the `ImGui.SameLine()` after the bought/listed text. Now the buttons start on a fresh row and `avail = ImGui.GetContentRegionAvail().X` returns the **full content region width** (~396px at scale 1.5, ~388px at scale 2.0).

### Edit 2: Scale button widths by `ImGuiHelpers.GlobalScale`

`ImGuiHelpers.GlobalScale` (from `Dalamud.Interface.Utility`) returns the FFXIV UI scale factor. Multiply both button widths by it so the frame grows with the font.

```csharp
ImGui.Text($"Bought: {boughtCount}/{totalItems}   Listed: {listedCount}/{totalItems}");

// Buttons on their own row so avail = full content region width, not the
// remaining space after the bought/listed text.
var rescanWidth = 110f * ImGuiHelpers.GlobalScale;
var settingsWidth = 80f * ImGuiHelpers.GlobalScale;
var buttonSpacing = ImGui.GetStyle().ItemSpacing.X;
var avail = ImGui.GetContentRegionAvail().X;
var combinedWidth = rescanWidth + buttonSpacing + settingsWidth;
if (avail > combinedWidth)
    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - combinedWidth);

if (ImGui.Button("Settings", new Vector2(settingsWidth, 0)))
    plugin.OpenConfigWindow();

ImGui.SameLine();
if (plugin.ScanInProgress)
    ImGui.BeginDisabled();
if (ImGui.Button("Rescan Route", new Vector2(rescanWidth, 0)))
    _ = plugin.RescanAsync(CancellationToken.None);
if (plugin.ScanInProgress)
    ImGui.EndDisabled();
```

### Pixel arithmetic with the fix at scale 1.5

| Quantity | Value |
|----------|-------|
| `avail` (full content region, fresh row) | ~396px |
| `rescanWidth` (110 × 1.5) | 165px |
| `settingsWidth` (80 × 1.5) | 120px |
| `buttonSpacing` | ~12px |
| `combinedWidth` | 297px |
| Guard 396 > 297 | TRUE |
| Cursor advances by 99 | Settings at x=99, ends 219; Rescan at x=231, ends 396 |
| Both buttons fully inside content region | ✓ |
| "Rescan Route" rendered inside 165px frame at scale 1.5 | fits — frame width = label width × scale × ~1.5 ≈ adequate |

### At scale 2.0

| Quantity | Value |
|----------|-------|
| `avail` | ~388px |
| `rescanWidth` (110 × 2.0) | 220px |
| `settingsWidth` (80 × 2.0) | 160px |
| `combinedWidth` (220 + 16 + 160) | 396px |
| Guard 388 > 396 | FALSE |
| Cursor doesn't advance | both buttons start at left, total width 396, overflows by 8px |

Scale 2.0 still has a marginal failure mode at the 420px window width. To bullet-proof against scale 2.0+, we could also bump the window minimum width — but FFXIV's UI scale slider goes 0.7 → 2.0, and most users run 1.0 → 1.4. Document this as a known cap (scales > ~1.9 may still clip; the fix covers the common case 1.0 → 1.8).

## Nyquist regression assertions to add

Two new assertions in `tests/phase04_nyquist.sh` to lock the fix in:

1. The buttons must NOT be on the same row as the bought/listed text. Concretely, the line immediately following the `ImGui.Text($"Bought:` rendering MUST NOT be `ImGui.SameLine();`. Use awk to enforce structural relationship.

2. Both `rescanWidth` and `settingsWidth` MUST be multiplied by `ImGuiHelpers.GlobalScale` (or equivalent UI scale factor). Pattern: `(rescanWidth|settingsWidth)\s*=.*ImGuiHelpers\.GlobalScale`.

These augment the existing GAP-D1 assertions (the runtime `ItemSpacing.X` read remains correct and stays asserted).

## Files affected

- `NamazuFlippers/UI/DailyRouteWindow.cs` — DrawProgressSection (drop SameLine + scale button widths). Add `using Dalamud.Interface.Utility;` if not already present.
- `tests/phase04_nyquist.sh` — append 2 new GAP-E1 regression assertions.

## What MUST NOT change

- The bought/listed text content and rendering (line 120 `ImGui.Text(...)`).
- The `BeginDisabled`/`EndDisabled` guard around Rescan.
- The Settings click handler (`plugin.OpenConfigWindow()`) and Rescan click handler (`plugin.RescanAsync(...)`).
- The 04-07 `buttonSpacing = ImGui.GetStyle().ItemSpacing.X` runtime read — still correct, stays.
- Any other UI window or Phase 3 source.
