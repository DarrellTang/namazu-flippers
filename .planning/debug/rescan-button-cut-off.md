---
status: diagnosed
trigger: "Rescan Route button is cut off on the right side of DailyRouteWindow at default size"
created: 2026-05-07T00:00:00Z
updated: 2026-05-07T00:00:00Z
---

## Current Focus

hypothesis: The SameLine button row (Rescan Route 110px + Settings 80px) overflows the available content region because window initial size is 420px (not 720px per UI-SPEC), and the SetCursorPosX right-alignment calculation pushes Rescan past the right edge leaving Settings entirely clipped off-screen.
test: Static analysis of button row layout math vs actual initial window width
expecting: Confirmed — root cause is cumulative button row width vs actual content region
next_action: DIAGNOSED — return ROOT CAUSE FOUND

## Symptoms

expected: Rescan Route button renders fully within the DailyRouteWindow visible area at default window width
actual: The re-scan route button is cut off on the right; Settings button is missing entirely
errors: None
reproduction: Open DailyRouteWindow at default size, observe progress section header row
started: Discovered during Phase 4 UAT

## Eliminated

- hypothesis: Window size constraint is wrong (below minimum)
  evidence: SizeConstraints.MinimumSize = (320, 300), actual Size = (420, 560) — within constraints, so no clamping occurs
  timestamp: 2026-05-07T00:00:00Z

## Evidence

- timestamp: 2026-05-07T00:00:00Z
  checked: DailyRouteWindow constructor (DailyRouteWindow.cs L38-50)
  found: Size = new Vector2(420, 560); SizeCondition = ImGuiCond.FirstUseEver
  implication: Initial window width is 420px, NOT 720px as stated in diagnostic hint. UI-SPEC table says "420 × 560 (ImVec2)" — the 720px figure was incorrect. Content region at 420px is approximately 420px minus ~16px window padding on each side = ~388px usable.

- timestamp: 2026-05-07T00:00:00Z
  checked: DrawProgressSection button row (DailyRouteWindow.cs L119-137)
  found: |
    ImGui.Text($"Bought: {boughtCount}/{totalItems}   Listed: {listedCount}/{totalItems}");
    ImGui.SameLine();
    const float buttonWidth = 110f;
    var avail = ImGui.GetContentRegionAvail().X;
    if (avail > buttonWidth)
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - buttonWidth);
    if (ImGui.Button("Rescan Route", new Vector2(buttonWidth, 0)))
        ...
    ImGui.SameLine();
    if (ImGui.Button("Settings", new Vector2(80, 0)))
        plugin.OpenConfigWindow();
  implication: The right-alignment calculation positions Rescan's LEFT edge at (cursorX + avail - 110). Rescan then occupies pixels [rightEdge - 110 .. rightEdge]. Settings is then placed via SameLine AFTER Rescan, adding 80px + item spacing (~8px) beyond the right edge of the content region — it is entirely off-screen. Furthermore, if the text "Bought: 0/0   Listed: 0/0" is wide enough that avail < 110 after SameLine, the cursor is NOT moved at all, and Rescan starts wherever the text ended, potentially running past the edge.

- timestamp: 2026-05-07T00:00:00Z
  checked: UI-SPEC Interaction Contracts — Rescan Route Button placement spec (04-UI-SPEC.md L260-261)
  found: "Placement: right-aligned in the summary section (use ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - buttonWidth) before drawing)."
  implication: The spec describes right-aligning ONLY the Rescan button. It says nothing about placing Settings on the same line after it. The implementation adds Settings via SameLine after the already-right-aligned Rescan, pushing Settings past the window boundary.

- timestamp: 2026-05-07T00:00:00Z
  checked: 04-HUMAN-UAT.md Test 3 gap entry
  found: User reported both "no settings button" and "rescan route button cut off on the right" as a single observation
  implication: These are the same root cause: Settings is being rendered via SameLine after the right-aligned Rescan, causing Settings to land fully off-screen (invisible to user) and Rescan to clip at the very right edge because ImGui's clipping rect cuts it off when it approaches the window boundary.

## Resolution

root_cause: |
  In DrawProgressSection, Rescan Route (110px) is right-aligned to the window edge using
  SetCursorPosX(cursorX + avail - buttonWidth). Settings (80px) is then appended via
  ImGui.SameLine(), placing it ~88px BEYOND the right edge of the content region.
  This causes Settings to be entirely invisible and Rescan to appear clipped at its right
  edge (ImGui clips draws that extend past the window boundary).
  The window's actual initial width is 420px (not 720px), making the content region
  approximately 388px — leaving zero room for a two-button right-aligned row after
  the progress text.
fix: empty — diagnosis only
verification: empty — diagnosis only
files_changed: []
