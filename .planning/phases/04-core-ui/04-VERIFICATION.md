---
phase: 04-core-ui
verified: 2026-05-08T05:30:40Z
status: human_needed
score: 8/8 must-haves verified at source level
overrides_applied: 0
re_verification:
  previous_status: passed
  previous_score: 8/8
  trigger: "Second UAT round on build 1.0.26.0 surfaced GAP-D1 (Rescan clipped at non-1.0 UI scale) and GAP-D2 (Listed checkbox column drift); plan 04-07 closed both at the source level."
  gaps_closed:
    - "GAP-D1 (UAT Test 2, build 1.0.26.0): const float buttonSpacing = 8f replaced with var buttonSpacing = ImGui.GetStyle().ItemSpacing.X — Rescan reservation now tracks Dalamud's UI-scale-driven actual SameLine() gap. Verified at DailyRouteWindow.cs:128."
    - "GAP-D2 (UAT Test 1, build 1.0.26.0): bare ImGui.SameLine() before ##listed- replaced with ImGui.SameLine(listedAnchorX) where listedAnchorX = GetWindowContentRegionMax().X - 150f. Verified at DailyRouteWindow.cs:259-268. Bare-SameLine fallback when row width exceeds anchor."
    - "All four UAT-1 gaps (GAP-A1 profit tally, GAP-B1 Settings visible, GAP-B2 Rescan visible at scale 1.0, GAP-C1 Discard reverts) remain closed; nyquist regression assertions for 04-04 and 04-06 still pass."
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "Listed checkbox column alignment at runtime UI scale"
    expected: "Open DailyRouteWindow with today's scan loaded on a build > 1.0.26.0. Every item row's Listed checkbox lands in the same X column regardless of item-name length, [OOS] / [Vendor] badges, Buy price digit count, or +profit/day digit count. Toggling any Listed checkbox still updates the GilGold profit tally."
    why_human: "GAP-D2 fix is layout-pixel positioning that only manifests when ImGui actually renders the window in-game; source-level grep cannot validate visual column alignment across variable-width rows."
    closes_gap: "GAP-D2"
    build_required: "> 1.0.26.0 (post-merge CI build)"
  - test: "Rescan Route button visible at FFXIV UI scale > 1.0"
    expected: "On a build > 1.0.26.0, set FFXIV UI scale to a non-1.0 value (1.5x is the user's reported scale). Open DailyRouteWindow at default 420px width. Both Settings and Rescan Route buttons render fully inside the content region with no right-edge clipping. Click Rescan Route — the disabled state engages while ScanInProgress, then re-enables."
    why_human: "GAP-D1 fix replaces a hardcoded 8px spacing with a runtime ItemSpacing read; the bug only manifests at non-1.0 UI scale, which the local source-validation gate cannot exercise. The user UAT on 1.0.26.0 caught this exact bug; re-UAT on the next build is the authoritative gate."
    closes_gap: "GAP-D1"
    build_required: "> 1.0.26.0 (post-merge CI build)"
---

# Phase 4: Core UI Verification Report (Re-verification, Round 2)

**Phase Goal:** Player sees today's route in an ImGui window, clicks through items, and tracks profit
**Verified:** 2026-05-08T05:30:40Z
**Status:** human_needed
**Re-verification:** Yes — after second UAT round (build 1.0.26.0) and gap-closure plan 04-07

## Verdict

**PASS_WITH_NOTES (source level)** — all 8 roadmap success criteria are present and correctly wired in the codebase, the local source-validation gate (`tests/phase04_nyquist.sh`) passes 56/56 with all UAT-driven regression assertions for 04-04, 04-06, and 04-07 in place, and Phase 3 source files (RouteStop.cs, RouteOptimizer.cs, ScanEngine.cs) plus `tests/phase03_nyquist.sh` are unchanged across this phase except for one documented mechanical update in 04-01 (`isVisible` → `dailyRouteWindow.IsOpen` rename in a phase03 assertion, called out in 04-01-SUMMARY.md). The phase03 nyquist baseline (exit 1, 2 pre-existing SCAN-01 failures) is preserved.

The two open in-game items (`human_verification` above) are the 04-07 layout fixes that closed source-level after the 1.0.26.0 UAT but cannot be confirmed without running on a build > 1.0.26.0 in-game. The user explicitly asked for in-game UAT on the next build as the ship-level gate.

## Phase Goal Achievement

### Observable Truths (Roadmap Success Criteria — UI-01..UI-08)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DailyRouteWindow shows route with server stops, items, prices, and expected profit | VERIFIED | `DailyRouteWindow.cs:78-79` foreach over `result.RouteStops` calls `DrawRouteStop`; `DrawRouteStop` (lines 162-207) renders `CollapsingHeader` per stop; `DrawItems` renders `Buy: {item.PurchasePrice:n0}` (PurchaseCyan, line 248) and `+{item.ExpectedDailyProfit:n0}/day` (GilGold, line 250) per item; `List: {item.HomePrice:n0}` (line 273) per item. |
| 2 | Each item has a clickable checkbox to mark "bought" | VERIFIED | `DailyRouteWindow.cs:217-218` — `ImGui.Checkbox($"##bought-{item.ItemId}", ref bought)` writes to `boughtState[item.ItemId]`; bought items render in CompletedGray (line 222-223). |
| 3 | Home stop shows items to list with "listed" checkboxes (now: every item row has a Listed checkbox) | VERIFIED | `DailyRouteWindow.cs:269-271` — `##listed-{item.ItemId}` renders on every item row in every RouteStop after gap-closure 04-04 removed the unreachable `isHomeStop` gate. Anchored to a fixed-X column via `ImGui.SameLine(listedAnchorX)` (line 266) per gap-closure 04-07 GAP-D2. |
| 4 | Running profit tally updates in real time | VERIFIED | `DailyRouteWindow.cs:115-117` — `result?.Opportunities.Where(o => listedState.GetValueOrDefault(o.ItemId)).Sum(o => o.ExpectedDailyProfit)` recomputed each frame; rendered as `ImGui.TextColored(GilGold, $"Profit: {listedProfit:n0} / {totalProfit:n0} gil")` at line 159. UAT round 2 confirmed: "checking the listed checkbox does increase the accompanying status bar and updates the profit number." |
| 5 | Progress bar shows bought/total and listed/total completion | VERIFIED | `DailyRouteWindow.cs:148-157` — two `ImGui.ProgressBar` calls with `PushStyleColor(PlotHistogram, SuccessGreen)` and `PushStyleColor(PlotHistogram, PurchaseCyan)` overrides; fractions computed from `boughtState`/`listedState` counts. |
| 6 | OOS items are visually distinct (color/icon) | VERIFIED | `DailyRouteWindow.cs:224-225, 236-240` — `item.OutOfStock` renders item name in `OosOrange`; `[OOS]` badge in `OosOrange` after item name; bought items still render as CompletedGray (bought takes priority over OOS). |
| 7 | Completed server stops auto-collapse | VERIFIED | `DailyRouteWindow.cs:164-176` — `stop.Items.All(item => boughtState.GetValueOrDefault(item.ItemId))` predicate; `ImGui.SetNextItemOpen(false, ImGuiCond.Always)` fires once on first all-bought frame; `autoCollapsedStops[stop.PurchaseSource]` flag prevents re-fire; reset to false when any item un-checked. ✓ checkmark prefix in collapsed label (lines 180-191). |
| 8 | ConfigWindow exposes all settings from CONF-01 through CONF-09 | VERIFIED | `ConfigWindow.cs` — 7 `CollapsingHeader` sections covering all 14 Configuration properties; Save / Reset / unsaved-changes modal / Reset confirmation modal all present; OnOpen guarded with `!isDirty` (line 54, gap-closure 04-06) so Discard correctly reverts edits. |

**Score:** 8/8 truths verified at source level

### Phase Requirements Map

| Requirement | Description | Source Plan(s) | Status | Evidence |
|-------------|-------------|----------------|--------|----------|
| UI-01 | DailyRouteWindow renders today's route with stops, items, prices | 04-01, 04-05, 04-07 | SATISFIED (source) | DrawProgressSection layout fits at 420px (04-05 combinedWidth, 04-07 runtime ItemSpacing); DrawRouteStop CollapsingHeaders per stop. |
| UI-02 | Bought checkbox per item | 04-02 | SATISFIED | `##bought-{item.ItemId}` Checkbox wired to `boughtState`. |
| UI-03 | Listed checkbox per item to track listing | 04-02, 04-04, 04-07 | SATISFIED (source) | `##listed-{item.ItemId}` renders on every item row (04-04 removed isHomeStop gate); column anchored to fixed X via SameLine(listedAnchorX) (04-07 GAP-D2). |
| UI-04 | Running profit tally updates in real time | 04-02, 04-04 | SATISFIED | LINQ over `listedState` → `Sum(o.ExpectedDailyProfit)` per frame in GilGold; works because listedState now populates (UAT round 2 user-confirmed: "increase the accompanying status bar and updates the profit number"). |
| UI-05 | Progress bars (bought + listed) | 04-02 | SATISFIED | Two ProgressBar calls with PlotHistogram color push, real fractions from state dicts. |
| UI-06 | OOS visually distinct | 04-02 | SATISFIED | OosOrange item name + `[OOS]` badge; bought-CompletedGray takes priority. |
| UI-07 | Server stops auto-collapse on completion | 04-02 | SATISFIED | SetNextItemOpen(false, ImGuiCond.Always) + per-stop autoCollapsedStops flag with reset logic. |
| UI-08 | ConfigWindow exposes all CONF-01..09 settings | 04-03, 04-05, 04-06 | SATISFIED (source) | 14 Configuration properties wired across 7 CollapsingHeader sections; Settings button visible at 420px (04-05); Discard reverts correctly via `!isDirty` snapshot guard (04-06). |

(CONF-01..09 individually covered in original 04-VERIFICATION.md table — preserved intact below by re-running the same nyquist assertions; all 17 CONF widgets pass.)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `tests/phase04_nyquist.sh` | Source-validation gate, exit 0 | VERIFIED | 56 assertions all pass; includes 4 GAP-A1 (04-04) regressions, 2 GAP-C1 (04-06) regressions, 3 GAP-D1 (04-07) regressions, 1 GAP-D2 (04-07) regression. Exit 0. |
| `NamazuFlippers/UI/DailyRouteWindow.cs` | Window subclass with route + checkboxes + profit + layout | VERIFIED | 282 lines; all 5 ScanEngineStatus banners; combinedWidth right-alignment using runtime ItemSpacing; ##listed- on every row anchored to listedAnchorX. |
| `NamazuFlippers/UI/ConfigWindow.cs` | Full settings editor with all CONF-01..09 + Save/Reset/Discard semantics | VERIFIED | 408 lines; snapshot/dirty/discard plumbing; OnOpen guarded with `!isDirty` (line 54); 3 isDirty=false sites (Save btn:267, modal Save:304, modal Discard:312); both modals (ConfirmReset, UnsavedChanges) present. |
| `NamazuFlippers/UI/UiColors.cs` | 9 locked Vector4 color constants per UI-SPEC | VERIFIED | 9 `public static readonly Vector4` fields; all 6 nyquist color-token assertions pass. |
| `NamazuFlippers/UI/FirstRunWindow.cs` | Migrated to Window base class, no Func<bool> | VERIFIED | `class FirstRunWindow : Window`; root copy deleted; nyquist D-05/D-06 assertion passes. |
| `NamazuFlippers/NamazuFlippers.cs` | WindowSystem owner with all wiring | VERIFIED | WindowSystem field (line 30), 3x AddWindow (lines 78-80), named OnOpenConfigUi handler (line 140), public ScanInProgress / RescanAsync / OpenConfigWindow surface (lines 47-53), clean Dispose (lines 101-111). |
| `NamazuFlippers/FirstRunWindow.cs` (root) | Must not exist | VERIFIED | Confirmed absent (`ls` returns "No such file or directory"). |

### Key Link Verification (post-04-07)

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `NamazuFlippers.cs` ctor | `windowSystem.Draw` | `UiBuilder.Draw += windowSystem.Draw` | WIRED | Subscribe line 92, unsubscribe line 106. |
| `NamazuFlippers.cs` | `configWindow.IsOpen = true` | `OnOpenConfigUi` named method (line 140) | WIRED | Pitfall 1 mitigation — named handler, not anonymous lambda. |
| `DailyRouteWindow.Draw()` | `plugin.LatestScanResult` | Frame-level read with null guard | WIRED | Line 54 reads result; line 65 null/Empty/Error early return. |
| Rescan button | `plugin.RescanAsync(CancellationToken.None)` | Button click handler | WIRED | Line 144; BeginDisabled/EndDisabled (lines 141-146) guards on `plugin.ScanInProgress`. |
| Settings button | `plugin.OpenConfigWindow()` | Button click handler | WIRED | Line 138; rendered first (left), Rescan rendered second (right) — combinedWidth right-alignment. |
| `DrawProgressSection` reservation | `ImGui.GetStyle().ItemSpacing.X` | Runtime read each frame (line 128) | WIRED | GAP-D1 fix — replaces 04-05's `const float buttonSpacing = 8f` with runtime style read so reservation tracks Dalamud's UI-scale-driven actual gap. |
| `DrawItems` listed-checkbox | `listedState[item.ItemId]` | Unconditional render in per-item loop | WIRED | Lines 269-271 — gap-closure 04-04 removed isHomeStop gate. |
| `DrawItems` listed-checkbox | Fixed-X column | `ImGui.SameLine(listedAnchorX)` (line 266) with bare-SameLine fallback (line 268) | WIRED | GAP-D2 fix — listedAnchorX = GetWindowContentRegionMax().X - 150f computed each frame. |
| `DrawProgressSection` profit tally | `listedState × ExpectedDailyProfit` | LINQ Where + Sum per frame | WIRED | Lines 115-117. |
| `ConfigWindow.OnOpen` | `Snapshot(plugin.Configuration)` | Guarded by `!isDirty` (line 54) | WIRED | gap-closure 04-06 — distinguishes genuine open from Dalamud spurious post-OnClose re-open. |
| Discard modal button | `RestoreFrom(snapshot, plugin.Configuration)` | Modal handler (line 311) | WIRED | Snapshot is captured at genuine open, not corrupted by spurious re-open — UAT round 2 confirmed pass. |
| Save buttons (×2) | `pluginInterface.SavePluginConfig(plugin.Configuration)` | Direct call | WIRED | Save button line 265, modal Save line 302. |
| Reset button | `BeginPopupModal("ConfirmReset##config")` | OpenPopup on click | WIRED | Line 273 opens, line 278 renders modal. |
| `OnClose` | Unsaved-changes modal | `showUnsavedModal = true` + `IsOpen = true` | WIRED | Lines 63-67; trigger flushed at top of Draw (lines 73-77). |
| `DailyRouteWindow` state wipe | `ReferenceEquals(result, lastSeenResult)` | Result-change detection | WIRED | Lines 69-75 — all 3 dicts cleared on result reference change (D-09). |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Phase 04 nyquist passes | `bash tests/phase04_nyquist.sh` | exit 0; "Phase 04 Nyquist validation passed." | PASS |
| Phase 03 nyquist baseline preserved | `bash tests/phase03_nyquist.sh` | exit 1; 2 pre-existing SCAN-01 failures (normalizer wrapper shapes, Where(IsUsable) filter) — count unchanged from documented baseline | PASS (baseline) |
| isHomeStop string-compare absent | grep -nE "(isHomeStop\|Configuration\\.HomeWorld)" DailyRouteWindow.cs | (no matches) | PASS |
| ##listed- renders unconditionally | grep -nE "##listed-" DailyRouteWindow.cs | line 270 inside DrawItems loop | PASS |
| Runtime buttonSpacing read | grep -nE "buttonSpacing" DailyRouteWindow.cs | lines 128 (assigned from ImGui.GetStyle().ItemSpacing.X), 130 (used in combinedWidth) | PASS |
| Const buttonSpacing = 8f gone | grep "const float buttonSpacing" DailyRouteWindow.cs | (no match) | PASS |
| Listed anchor SameLine | grep -nE "SameLine\\(listedAnchorX\\)" DailyRouteWindow.cs | line 266 | PASS |
| GetWindowContentRegionMax used | grep -nE "GetWindowContentRegionMax" DailyRouteWindow.cs | line 261 | PASS |
| !isDirty guard | grep -nE "if \\(!isDirty\\)" ConfigWindow.cs | line 54 | PASS |
| isDirty=false count | grep -cE "isDirty = false" ConfigWindow.cs | 3 (Save btn line 267, modal Save 304, modal Discard 312) | PASS |
| Phase 3 source unchanged | git log --oneline -- NamazuFlippers/Core/RouteStop.cs RouteOptimizer.cs ScanEngine.cs | last commits: 2db776d (scan), 0ba88eb (scan), d4fb4a8 (03-02), 0cdcc23 (03-02), 537ca72 (03-02), 249237f (03-02), a60a19b (03-01) — none from phase 04 | PASS |
| Phase 3 nyquist log | git log --oneline -- tests/phase03_nyquist.sh | 7fa7037 (orig) + cdeb1d8 (04-01 mechanical isVisible→IsOpen rename, documented in 04-01-SUMMARY) | PASS (deliberate, baseline) |
| Root FirstRunWindow.cs absent | ls NamazuFlippers/FirstRunWindow.cs | "No such file or directory" | PASS |
| Phase 4 commit 6607f56 (GAP-D1+D2 fix) | git log --oneline | 6607f56 fix(04-07): scale-aware buttonSpacing + listed checkbox column anchor | PASS |
| Phase 4 commit 2424bff (GAP-D1+D2 nyquist) | git log --oneline | 2424bff test(04-07): nyquist regressions for GAP-D1 and GAP-D2 | PASS |
| Phase 4 commit 141eba5 (04-07 SUMMARY) | git log --oneline | 141eba5 docs(04-07): complete Rescan clip + Listed alignment gap-closure plan | PASS |

### UAT Closure Map (Round 1 — build 1.0.25.0; Round 2 — build 1.0.26.0)

| Gap (build) | UAT Test | Severity | Closed By | Source-Level Verified | In-Game UAT |
|-------------|----------|----------|-----------|-----------------------|-------------|
| GAP-A1 (1.0.25.0) — profit tally shows zero | UAT-1 Test 1 | major | 04-04 | YES (isHomeStop gate removed; ##listed- on every row; LINQ over listedState) | Round 2 PASS — user: "checking the listed checkbox does increase the accompanying status bar and updates the profit number" |
| GAP-B1 (1.0.25.0) — Settings button missing | UAT-1 Test 3 | major | 04-05 | YES (combinedWidth right-alignment) | Round 2 PASS — user: "Settings is there though" |
| GAP-B2 (1.0.25.0) — Rescan clipped at 420px | UAT-1 Test 3 | minor | 04-05 (partial) + 04-07 (full) | YES (combinedWidth + runtime ItemSpacing) | Round 2 ISSUE on 1.0.26.0 ("Rescan route is still cut off") → re-closed in source by 04-07 GAP-D1; pending in-game UAT on > 1.0.26.0 |
| GAP-C1 (1.0.25.0) — Discard does not revert | UAT-1 Test 3 | major | 04-06 | YES (`if (!isDirty)` snapshot guard) | Round 2 PASS — Test 3 result: pass |
| GAP-D1 (1.0.26.0) — Rescan clipped at FFXIV UI scale > 1.0 | UAT-2 Test 2 | major | 04-07 | YES (`var buttonSpacing = ImGui.GetStyle().ItemSpacing.X`) | PENDING in-game UAT on build > 1.0.26.0 |
| GAP-D2 (1.0.26.0) — Listed checkbox column drift | UAT-2 Test 1 | cosmetic | 04-07 | YES (`ImGui.SameLine(listedAnchorX)` with fallback) | PENDING in-game UAT on build > 1.0.26.0 |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `.planning/REQUIREMENTS.md` | 41 | `[x]` UI-01 (resolved by 2026-05-08 update) | Info | (Now correct — REQUIREMENTS.md was updated; UI-01..UI-08 all show `[x]` in current file. No action.) |
| `.planning/debug/listed-checkbox-not-aligned.md` | — | Debug session not yet moved to `resolved/` | Info | Housekeeping; the underlying GAP-D2 is closed in source. Move post-merge once in-game UAT confirms. |
| `.planning/debug/rescan-button-still-cut-off.md` | — | Debug session not yet moved to `resolved/` | Info | Housekeeping; the underlying GAP-D1 is closed in source. Move post-merge once in-game UAT confirms. |

No code anti-patterns. No TODO / FIXME / PLACEHOLDER comments in any Phase 4 source files. No stub implementations. No hardcoded empty-state returns.

### Code Review Advisory Carry-Forward (from 04-REVIEW.md)

**WR-01 (Advisory, ConfigWindow):** OnOpen no longer resets `isDirty = false`. Plan invariant ("`isDirty == true` on OnOpen entry implies a Dalamud spurious re-fire") is not enforced if the user dismisses the unsaved-changes modal via the modal's built-in X / ESC. No user-visible defect today; advisory only. Still open after 04-07; consider tightening in Phase 5 if session-store invariants require strict bought-before-listed semantics.

**WR-02 (closed by 04-07):** `buttonSpacing = 8f` constant drift under Dalamud UI scaling. **CLOSED** — 04-07 replaced the constant with `ImGui.GetStyle().ItemSpacing.X` runtime read. The 04-VERIFICATION (round 1) downgraded WR-02 to advisory; the 1.0.26.0 UAT proved that downgrade wrong, and the 04-07 plan re-promotes the lesson: do not dismiss runtime-style mismatch findings without explicit non-1.0-UI-scale verification.

**WR-03 (Advisory, semantic):** Listed checkbox is independent of bought state — a user can mark an item listed without first marking it bought, including on OOS items. Profit tally counts any listed item. Acceptable today (listing from pre-existing stockpile is a valid workflow); revisit in Phase 5 if SESS-01 introduces bought-before-listed invariants.

### Phase Boundary Compliance

| Constraint | Expected | Actual | Status |
|------------|----------|--------|--------|
| `NamazuFlippers/Core/RouteStop.cs` byte-unchanged across phase 04 | No phase-04 commits in log | Last commit: 249237f (03-02 add route optimizer) | PASS |
| `NamazuFlippers/Core/RouteOptimizer.cs` byte-unchanged | No phase-04 commits | Last commits: 03-02 series only | PASS |
| `NamazuFlippers/Core/ScanEngine.cs` byte-unchanged | No phase-04 commits | Last commits: 2db776d / 0ba88eb / d4fb4a8 / 537ca72 / a60a19b — all phase 03 | PASS |
| `tests/phase03_nyquist.sh` baseline preserved | Pre-existing 2 SCAN-01 failures unchanged; no new failures introduced | Exit 1, 2 failures (normalizer wrapper shapes, Where(IsUsable) filter) — both documented in 04-04-SUMMARY and 04-05-SUMMARY as pre-existing baseline | PASS |
| `tests/phase03_nyquist.sh` strictly byte-unchanged | (Stricter reading of user constraint) | Modified ONCE in commit cdeb1d8 (04-01) — single line: `isVisible = !isVisible` → `dailyRouteWindow.IsOpen = !dailyRouteWindow.IsOpen` to keep the "bare command still toggles UI" assertion synced with the windowSystem refactor | PASS_WITH_NOTES (deliberate; documented in 04-01-SUMMARY.md "Phase 03 Nyquist Update") |
| Local `dotnet build` not attempted | Per PROJECT.md / STATE.md, macOS local build is not the gate | Not attempted; nyquist-only validation per build verification policy | PASS |

The single phase03_nyquist.sh edit in 04-01 is mechanical (renaming a referenced symbol the assertion was checking against). It does not change what the assertion validates — it keeps the same SCAN-04 invariant ("bare command still toggles UI") aligned with the new entry-point method. This was disclosed in 04-01-SUMMARY.md and is the only deviation from a strict byte-for-byte reading of the constraint.

### Human Verification Required (post-merge in-game UAT on build > 1.0.26.0)

The local source-validation gate cannot exercise either of the 04-07 fixes — both manifest only at runtime under specific conditions (non-1.0 UI scale, variable-width row content). The user's stated ship-level gate is in-game UAT on the next CI build. Two items pending:

#### 1. Listed checkbox column alignment (closes GAP-D2)

- **Test:** Open DailyRouteWindow with today's scan loaded on a build > 1.0.26.0. Visually inspect the Listed checkbox column across multiple rows with varying name lengths, OOS / Vendor badges, and price digit counts. Toggle one or more Listed checkboxes and observe profit-tally update.
- **Expected:** All Listed checkboxes line up in the same X column regardless of preceding row content; the GilGold profit tally still updates as items are checked / unchecked.
- **Why human:** Visual column alignment can only be verified at render time. Source grep confirms the `SameLine(listedAnchorX)` anchor and the `listedAnchorX = GetWindowContentRegionMax().X - 150f` arithmetic; only an in-game render confirms the column lands consistently in the actual content region.

#### 2. Rescan Route button visible at non-1.0 UI scale (closes GAP-D1)

- **Test:** On a build > 1.0.26.0 with the user's reported FFXIV UI scale (1.5x suspected), open DailyRouteWindow at default 420px width. Observe Settings and Rescan Route buttons in the progress section.
- **Expected:** Both buttons render fully inside the right edge of the window's content region, with no clipping. Click Rescan Route — disabled state engages while ScanInProgress, then re-enables.
- **Why human:** The bug only manifests at non-1.0 UI scale; local source-validation cannot exercise Dalamud's runtime UI-scale multiplier. The 04-07 fix replaces the hardcoded 8 px gap with `ImGui.GetStyle().ItemSpacing.X` so the reservation tracks the actual SameLine() gap, but only an in-game render at the user's scale confirms the math.

### Gaps Summary

No source-level gaps. All 8 roadmap success criteria are present and correctly wired. The local source-validation gate (`tests/phase04_nyquist.sh`) passes 56/56. All UAT-driven regression assertions from 04-04, 04-06, and 04-07 are present and pass. Phase 3 deliverables are unchanged. Two items require in-game UAT on the next CI build — these are NOT source-level gaps; they are runtime behaviors the local gate cannot exercise.

### Notes for Phase 5 (session-store) readiness

- The bought / listed dictionaries (`Dictionary<int, bool>` keyed by `ItemId`) are the surface Phase 5 will persist to JSON. The current in-memory contract: dicts wipe on result reference change (D-09); survive window close + re-open within the same session (D-11). Phase 5 lifts this same contract to disk.
- The `if (!isDirty)` snapshot guard in `ConfigWindow.OnOpen` is independent of session state and will not interact with Phase 5 changes.
- The 04-07 layout fixes (runtime ItemSpacing, listedAnchorX) are independent of any persistence layer; Phase 5 should not perturb them.

---

_Verified: 2026-05-08T05:30:40Z_
_Verifier: Claude (gsd-verifier, goal-backward re-verification round 2)_
_Phase 4 source-level: PASS_WITH_NOTES_
_Phase 4 in-game ship gate: pending UAT on CI build > 1.0.26.0_
