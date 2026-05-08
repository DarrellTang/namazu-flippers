---
status: diagnosed
trigger: "Settings button missing from DailyRouteWindow — UAT Test 3 reports no Settings button in the route window header"
created: 2026-05-07T00:00:00Z
updated: 2026-05-07T00:00:00Z
symptoms_prefilled: true
goal: find_root_cause_only
---

## Current Focus

hypothesis: "Settings button is rendered AFTER SetCursorPosX pushes the cursor to the far right to right-align Rescan Route. The SameLine() that follows Rescan Route places Settings at cursor_x + Rescan width, which overflows the right edge of the window (420px default width)."
test: "Trace cursor math in DrawProgressSection lines 119–137"
expecting: "Settings button is rendered beyond the right window boundary and is clipped"
next_action: "DIAGNOSED — root cause confirmed via static code analysis"

## Symptoms

expected: "DailyRouteWindow has a Settings button that opens ConfigWindow (D-07 second entry point)"
actual: "No Settings button visible in the route window; user can only reach settings via the plugin installer gear icon"
errors: "None reported"
reproduction: "Open DailyRouteWindow via /nflip. Look for a Settings button alongside Rescan Route in the top section."
started: "Discovered during Phase 4 UAT immediately after execution"

## Eliminated

- hypothesis: "Settings button code was never written"
  evidence: "Code exists at lines 134–137 of DailyRouteWindow.cs: ImGui.SameLine(); if (ImGui.Button(\"Settings\", new Vector2(80, 0))) plugin.OpenConfigWindow();"
  timestamp: 2026-05-07T00:00:00Z

- hypothesis: "Settings button is behind a conditional flag that is always false"
  evidence: "No conditional guard — the ImGui.Button call is unconditional"
  timestamp: 2026-05-07T00:00:00Z

- hypothesis: "plugin.OpenConfigWindow() is not public or doesn't exist"
  evidence: "NamazuFlippers.cs line 53: public void OpenConfigWindow() => configWindow.IsOpen = true; — correctly public"
  timestamp: 2026-05-07T00:00:00Z

## Evidence

- timestamp: 2026-05-07T00:00:00Z
  checked: "NamazuFlippers/UI/DailyRouteWindow.cs DrawProgressSection lines 119–137"
  found: |
    Line 119: ImGui.Text(\"Bought: {boughtCount}/{totalItems}   Listed: {listedCount}/{totalItems}\")
    Line 121: ImGui.SameLine()
    Line 122: const float buttonWidth = 110f
    Line 123: var avail = ImGui.GetContentRegionAvail().X   // remaining horizontal space AFTER the text cursor
    Line 124-125: if (avail > buttonWidth) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - buttonWidth)
      // This RIGHT-ALIGNS Rescan Route to the window's right edge
    Line 129: ImGui.Button(\"Rescan Route\", new Vector2(110, 0))   // renders at right edge; consumes 110px
    Line 135: ImGui.SameLine()
    Line 136: ImGui.Button(\"Settings\", new Vector2(80, 0))
      // SameLine after a right-edge button advances cursor BEYOND the window right edge
      // Settings button is drawn at x = (window_right_edge) + item_spacing + 110px
      // This is entirely off-screen; ImGui clips it silently
  implication: "The cursor-positioning arithmetic right-aligns only Rescan Route, then SameLine adds 80px Settings further right — past the window boundary. ImGui clips silently; no error is reported."

- timestamp: 2026-05-07T00:00:00Z
  checked: "NamazuFlippers.cs line 53"
  found: "public void OpenConfigWindow() => configWindow.IsOpen = true; — correctly declared"
  implication: "The method exists and is public; the bug is purely in DrawProgressSection layout"

- timestamp: 2026-05-07T00:00:00Z
  checked: "04-02-PLAN.md Task 1 behavior spec"
  found: "Spec says: 'A Settings button is rendered next to Rescan Route; clicking it calls plugin.OpenConfigWindow()'. No exact cursor-position instructions given for Settings — executor placed it via SameLine() AFTER the right-aligned Rescan, not BEFORE it."
  implication: "Settings must be placed to the LEFT of Rescan Route on the same row, with the combined width used in the SetCursorPosX calculation."

## Resolution

root_cause: |
  DrawProgressSection right-aligns the Rescan Route button by computing
  SetCursorPosX(cursor + avail - 110). It then calls SameLine() and renders a
  second 80px Settings button. Because Rescan Route was already pushed to the
  window's right edge, SameLine advances the cursor an additional (item_spacing +
  80)px past the edge. ImGui clips the Settings button silently — it is drawn
  outside the window content region and is invisible to the user.

fix: |
  The cursor-positioning arithmetic must account for BOTH buttons.
  Render Settings FIRST (leftmost of the two right-aligned buttons), then
  SameLine + Rescan Route (rightmost). The SetCursorPosX calculation must use
  the combined width: buttonWidth (110) + spacing + settingsWidth (80).

  Concretely, replace the block starting at line 121 with:

    ImGui.SameLine();
    const float rescanWidth = 110f;
    const float settingsWidth = 80f;
    const float spacing = 8f;   // ImGui default item spacing
    var avail = ImGui.GetContentRegionAvail().X;
    var combinedWidth = rescanWidth + spacing + settingsWidth;
    if (avail > combinedWidth)
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - combinedWidth);

    if (ImGui.Button("Settings", new Vector2(settingsWidth, 0)))
        plugin.OpenConfigWindow();
    ImGui.SameLine();
    if (plugin.ScanInProgress) ImGui.BeginDisabled();
    if (ImGui.Button("Rescan Route", new Vector2(rescanWidth, 0)))
        _ = plugin.RescanAsync(CancellationToken.None);
    if (plugin.ScanInProgress) ImGui.EndDisabled();

  This positions Settings to the left of Rescan Route, both within the window's
  right-hand boundary.

verification: "not yet verified — diagnosis only (find_root_cause_only mode)"
files_changed:
  - NamazuFlippers/UI/DailyRouteWindow.cs
