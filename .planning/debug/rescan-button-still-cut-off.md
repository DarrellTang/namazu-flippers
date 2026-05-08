---
status: diagnosed
trigger: "Rescan Route button still cut off at default 420px window width after 04-05 combinedWidth fix"
created: 2026-05-08T00:00:00Z
updated: 2026-05-08T00:00:00Z
symptoms_prefilled: true
goal: find_root_cause_only
---

## Current Focus

hypothesis: |
  SameLine() between Settings and Rescan uses the runtime ImGui.GetStyle().ItemSpacing.X,
  but combinedWidth hardcodes buttonSpacing = 8f. When FFXIV UI scale > 1.0, the actual
  ItemSpacing.X = 8 * scale, so Rescan's right edge lands at content_right - 8 + (8*scale)
  = content_right + 8*(scale-1) — which is beyond the content boundary by 8*(scale-1) px.
test: Arithmetic trace of DrawProgressSection layout for scale 1.0 vs scale > 1.0
expecting: Confirmed — overflow equals 8*(scale-1) which at scale 1.5 is 4 imgui units = 6 screen pixels
next_action: DIAGNOSED — return ROOT CAUSE FOUND

## Symptoms

expected: At default window size (locked Vector2(420, 560), FirstUseEver), DrawProgressSection's right-aligned button row should show BOTH Settings (left of pair) and Rescan Route (right of pair) entirely within the window's content region — neither button clipped.
actual: "Rescan route is still cut off. Settings is there though" (build 1.0.26.0)
errors: None (visual/layout only)
reproduction: Open DailyRouteWindow at default size, observe progress section header row (Test 2 in 04-UAT.md)
started: Same failure as GAP-B2; 04-05 closed Settings visibility (GAP-B1) but not Rescan clipping (GAP-B2)

## Eliminated

- hypothesis: The original pre-04-05 bug (Settings pushed off-screen via SameLine AFTER right-aligned Rescan)
  evidence: 04-05 reordered buttons so Settings is drawn FIRST (leftmost), Rescan after. User confirms Settings is now visible. The original render order bug is fixed.
  timestamp: 2026-05-08T00:00:00Z

- hypothesis: FramePadding inflates the actual button render width beyond the Vector2(110,0) spec
  evidence: When a button is called with an explicit non-zero width in Vector2, ImGui uses that width as the bounding box exactly. FramePadding affects label positioning within the button, not the button's total width. The button does not grow beyond 110px due to FramePadding.
  timestamp: 2026-05-08T00:00:00Z

- hypothesis: The avail > combinedWidth guard fails, causing buttons to draw from cursor position after the text (guard never jumps cursor)
  evidence: User reports Settings IS visible at the right side of the window, which is only possible if SetCursorPosX was called (guard passed). So avail > 198 at the user's setup. The guard is not the failure path.
  timestamp: 2026-05-08T00:00:00Z

- hypothesis: Vertical scrollbar reduces content width enough to cause overflow
  evidence: GetContentRegionAvail().X already accounts for the vertical scrollbar width when one is present — the scrollbar reserve is subtracted from avail before it's returned. So the button placement formula automatically adapts. The scrollbar narrows the content region but the formula reads that narrowed width directly.
  timestamp: 2026-05-08T00:00:00Z

## Evidence

- timestamp: 2026-05-08T00:00:00Z
  checked: DailyRouteWindow.cs L121-142 — full DrawProgressSection button row
  found: |
    Line 121: SameLine() — cursor moves to end of Text widget + ItemSpacing.X
    Line 125: avail = GetContentRegionAvail().X  (= content_right - current_cursor_x)
    Line 127-128: if (avail > combinedWidth) SetCursorPosX(GetCursorPosX() + avail - combinedWidth)
      → simplifies to: cursor_x = content_right - combinedWidth = content_right - 198
    Line 133: Button("Settings", Vector2(80,0)) → renders [content_right-198 .. content_right-118]
    Line 136: SameLine() — NO args → gap = ImGui.GetStyle().ItemSpacing.X at runtime (call it S)
      → cursor_x = content_right - 118 + S
    Line 139: Button("Rescan Route", Vector2(110,0)) → renders [content_right-118+S .. content_right-8+S]
    Rescan's right edge = content_right - 8 + S
    For no overflow: S <= 8
    combinedWidth hardcodes buttonSpacing = 8f (assumes S = 8 exactly)
  implication: If actual runtime ItemSpacing.X > 8, Rescan overflows by (S - 8) pixels.

- timestamp: 2026-05-08T00:00:00Z
  checked: 04-REVIEW.md WR-02 (DailyRouteWindow.cs:124-128)
  found: |
    "Dalamud applies a global UI scale to ImGui style, and players can configure that
    scale (typically 0.85x to 1.5x). When scale is non-1.0, the actual ItemSpacing.x
    between Settings and Rescan is ~8 * scale, so the right-alignment math is off by
    8 * (scale - 1) pixels. At scale=1.5 that's a 4px discrepancy."
    WR-02 was marked Advisory and dismissed as "cosmetic drift."
  implication: The review identified the exact failure mechanism but dismissed it. The user's report confirms it produces visible clipping, not merely cosmetic drift.

- timestamp: 2026-05-08T00:00:00Z
  checked: 04-VERIFICATION.md WR-02 note
  found: "Under Dalamud UI scale != 1.0, the actual gap differs from 8px by a small amount. Cosmetic drift only; buttons remain in the content region at all practical scales."
  implication: This assertion was incorrect. "Buttons remain in the content region at all practical scales" is contradicted by the UAT result. At the user's actual FFXIV UI scale, the overflow is large enough to clip Rescan visibly.

- timestamp: 2026-05-08T00:00:00Z
  checked: Arithmetic for scale 1.5 (a common FFXIV UI scale)
  found: |
    ItemSpacing.X at scale 1.5 = 8 * 1.5 = 12 imgui units
    combinedWidth hardcodes spacing = 8
    Rescan right edge = content_right - 8 + 12 = content_right + 4 imgui units
    At 1.5x scale, 4 imgui units = 6 screen pixels overflow
    This is visible clipping, consistent with user report.
  implication: The fix is scale-aware: replace `const float buttonSpacing = 8f` with `var buttonSpacing = ImGui.GetStyle().ItemSpacing.X`.

- timestamp: 2026-05-08T00:00:00Z
  checked: Whether Vector2(110,0) is the correct total width including any ImGui internal additions
  found: |
    ImGui.Button() with an explicit non-zero width uses that exact value as the widget's
    bounding box width. FramePadding affects label centering inside the box, not the box
    size. So the 110px and 80px values are the full widths as laid out by ImGui.
    The text "Rescan Route" at default font + default FramePadding fits within 110px
    (CalcTextSize("Rescan Route") ≈ ~80px at default font scale, well under 110).
  implication: Button width literals are not the problem. The label fits and the box is exactly as wide as specified.

- timestamp: 2026-05-08T00:00:00Z
  checked: SameLine() with no arguments — what spacing does it use?
  found: |
    From ImGui source and Dalamud documentation: SameLine() with no arguments (or offset_from_start_x=0)
    uses GetStyle().ItemSpacing.X as the gap between the previous widget and the next cursor position.
    This is a runtime value that scales with Dalamud's global UI scale factor.
    The hardcoded buttonSpacing = 8f only matches at scale 1.0.
  implication: The mismatch between hardcoded 8f and runtime ItemSpacing.X is the root cause.

## Resolution

root_cause: |
  In DrawProgressSection (DailyRouteWindow.cs L124-128), `combinedWidth` is computed as:
    const float buttonSpacing = 8f;
    var combinedWidth = rescanWidth + buttonSpacing + settingsWidth;  // = 198
  This hardcodes buttonSpacing = 8f, assuming ImGui's default ItemSpacing.X is always 8.

  However, `ImGui.SameLine()` on line 136 (between Settings and Rescan) uses the RUNTIME
  ImGui.GetStyle().ItemSpacing.X, which Dalamud scales by the FFXIV global UI scale.
  At UI scale != 1.0, the actual gap exceeds 8f.

  Result:
    - SetCursorPosX places the group's left edge at (content_right - 198). [Correct]
    - Settings (80px) renders from (content_right-198) to (content_right-118). [Correct — user confirms visible]
    - SameLine() advances cursor by actual_ItemSpacing_X (runtime value, NOT necessarily 8).
    - Rescan (110px) renders from (content_right-118+S) to (content_right-8+S).
    - Rescan's right edge = content_right + (S-8), which overflows by (S-8) px when S > 8.

  At FFXIV UI scale 1.5: S = 12, overflow = 4 imgui units = 6 screen pixels. Visibly clipped.

  The code review WR-02 identified this exact mechanism and proposed the fix, but
  04-VERIFICATION.md incorrectly dismissed it as "cosmetic drift only; buttons remain in
  the content region at all practical scales." The user's UAT result disproves that claim.

fix: empty — diagnosis only (find_root_cause_only mode)
verification: empty — diagnosis only
files_changed: []
