# Phase 4: Core UI - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in `04-CONTEXT.md` — this log preserves the alternatives considered.

**Date:** 2026-05-06
**Phase:** 04-core-ui
**Areas discussed:** Plan breakdown, Window registration, Bought/listed lifecycle, ConfigWindow save semantics

---

## Plan breakdown

### Plan split

| Option | Description | Selected |
|--------|-------------|----------|
| Keep ROADMAP's 3 plans | 04-01 layout, 04-02 interactions+profit/progress, 04-03 ConfigWindow. Sequential, clear seams. | ✓ |
| Merge into 2 plans | 04-01 full DailyRouteWindow, 04-02 ConfigWindow. Less plan-handoff churn. | |
| 3 plans, reordered | 04-01 WindowSystem scaffolding + FirstRunWindow migration, 04-02 full DailyRouteWindow, 04-03 ConfigWindow. | |

**User's choice:** Keep ROADMAP's 3 plans.

### Ship strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Each plan ships independently | 04-01 merges with non-interactive read-only DailyRouteWindow; subsequent plans add on top. | |
| Phase 4 ships as one unit | All 3 plans build on the same branch; nothing merges until UAT passes. Avoids broken-looking intermediate states. | ✓ |

**User's choice:** Phase 4 ships as one unit.

### Parallelism

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — 04-03 parallel with 04-01/02 | ConfigWindow only touches its own new file + small `NamazuFlippers.cs` wiring. Low merge-conflict surface. | ✓ |
| No — strictly sequential 01→02→03 | Predictable, no merge surprises. Slower wall-clock. | |
| You decide | Planner picks based on file-touch analysis. | |

**User's choice:** Yes — 04-03 parallel with 04-01/02.

### Test strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Source-validation + manual UAT | tests/phase04_nyquist.sh asserts widget calls and color tokens. CI compiles. Manual UAT validates feel. | |
| Manual UAT only | ImGui rendering can't be meaningfully tested from source. Skip the script; rely on CI compile + dogfooding. | |
| Source-validation + minimal unit tests | Source-validation script + xUnit-style assertions on pure-logic helpers (profit tally, autocollapse, state merge). | ✓ |

**User's choice:** Source-validation + minimal unit tests.

---

## Window registration

### First-run scope (asked after free-text question "will we need a first run window of some sort?")

| Option | Description | Selected |
|--------|-------------|----------|
| Keep FirstRunWindow | Dedicated modal popup for first-run home world. Already working; mirrors many Dalamud plugin patterns. | ✓ |
| Fold first-run into ConfigWindow | If HomeWorld is empty when /nflip opens, auto-open ConfigWindow. One window class to maintain. | |

**User's choice:** Keep FirstRunWindow.
**Notes:** User asked "Will we need a first run window of some sort? I assume we would right?" before selecting. Confirmed yes — FirstRunWindow stays as-is for first-run UX.

### WindowSystem migration scope

| Option | Description | Selected |
|--------|-------------|----------|
| Migrate all three to WindowSystem | FirstRunWindow + DailyRouteWindow + ConfigWindow all extend Window; one WindowSystem owns them. NamazuFlippers.cs OnDraw becomes one-liner. | ✓ |
| Only new windows on WindowSystem | DailyRouteWindow + ConfigWindow use Window base; FirstRunWindow keeps ad-hoc render. Mixed pattern but doesn't touch working first-run code. | |

**User's choice:** Migrate all three to WindowSystem.

### ConfigWindow open paths

| Option | Description | Selected |
|--------|-------------|----------|
| UiBuilder.OpenConfigUi + in-window button row | Register OpenConfigUi (Dalamud convention) AND add a Settings button in DailyRouteWindow. UI-SPEC permits both. | ✓ |
| UiBuilder.OpenConfigUi only | ConfigWindow only opens via /xlsettings. Cleanest layout but less discoverable. | |
| In-window button only | No OpenConfigUi registration; only opens from inside DailyRouteWindow. Skips a Dalamud convention. | |

**User's choice:** UiBuilder.OpenConfigUi + in-window button row.

### WindowSystem wire-up location

| Option | Description | Selected |
|--------|-------------|----------|
| Construct + own in NamazuFlippers.cs | Plugin entry point creates WindowSystem, adds windows, wires Draw callback. Mirrors existing pattern. | |
| Dedicated UI/PluginUi class | Extract a UI/PluginUi.cs that owns WindowSystem and the windows. Thinner entry point, extra indirection. | |
| You decide | Planner picks based on whether NamazuFlippers.cs is getting too heavy. | ✓ |

**User's choice:** You decide.

---

## Bought/listed lifecycle

### Rescan behavior (re-asked after free-text "you keep referring to variable names like LatestScanResult")

| Option | Description | Selected |
|--------|-------------|----------|
| Wipe checkboxes | Fresh route, fresh slate. Predictable. | ✓ |
| Keep ticks for items that reappear | Item-by-ItemId merge. Friendlier mid-session, but new scan recommendations might shift. | |
| Confirm if anything is ticked | Modal "Discard progress and rescan?". Avoids accidental wipes. UI-SPEC didn't define this. | |

**User's choice:** Wipe checkboxes.
**Notes:** First framing used internal variable names (`LatestScanResult`); user pushed back rightly. Reframed in UX terms before reasking. Also surfaced a separate question about long-term stats tracking (see below).

### Long-term stats / earnings tracker (raised by user during the rescan question)

| Option | Description | Selected |
|--------|-------------|----------|
| Defer to backlog | Capture as a deferred idea — future phase. Phase 4 stays focused on per-session tally. | ✓ |
| Add to Phase 4 scope | Expand Phase 4 to also persist completed routes + add a stats view. New persistence model + new requirements. | |
| Quick lifetime gil counter only | Single "Lifetime gil earned" number persisted to config. Minimal scope expansion. | |

**User's choice:** Defer to backlog.
**Notes:** Captured as deferred idea covering lifetime/daily earnings tracker, completed-route history, per-item P&L over time.

### Empty-state behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Just the status banner, no checkboxes | Banner replaces route area entirely. Progress bars and profit tally hidden. Cleanest empty state. | |
| Banner + dimmed empty progress section | Banner shows; progress bars (0/0) and profit row (0 gil) render in dimmed style. Layout stays consistent. | ✓ |
| You decide | Planner picks based on layout stability vs visual quietness. | |

**User's choice:** Banner + dimmed empty progress section.

### Session persistence (close + reopen within game session)

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — state persists in-memory across opens | Closing the window just hides it; dictionaries on the window class survive. Phase 5 will lift this to JSON for cross-session. | ✓ |
| No — close wipes the state | Closing acts like soft-reset. Probably bad UX. | |

**User's choice:** Yes — state persists in-memory across opens.

---

## ConfigWindow save semantics

### Edit flow (re-asked after free-text "what is common practice for other Dalamud plugins?")

| Option | Description | Selected |
|--------|-------------|----------|
| Live edit + Save persists | UI controls mutate Configuration directly; Save calls SavePluginConfig. Closing without saving leaves runtime in new state until next plugin launch. | |
| Draft buffer + Save commits | ConfigWindow holds a draft; sliders/checkboxes mutate the draft. Save copies draft → Configuration AND persists. Closing without saving discards. | |
| Snapshot + live edit + close-prompt + revert-on-discard | Snapshot Configuration on window open. Edits mutate Configuration directly. Save updates snapshot + persists. Discard restores from snapshot. | ✓ |

**User's choice:** Snapshot + live edit + close-prompt + revert-on-discard.
**Notes:** First clarified Dalamud convention is inconsistent — both live-save and explicit-save patterns ship in popular plugins. User then asked about prompt-on-close for unsaved changes, which led to picking the snapshot variant (only way to support Discard-on-close cleanly).

### Reset to Defaults behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Reset to defaults + immediately save | One-click revert + persist. UI-SPEC's red button styling matches "destructive but reversible". | |
| Reset values in-window only; user must Save | Click Reset → controls show defaults; dirty flag goes true; user clicks Save (or Discard) to commit. | |
| Confirm modal before reset | "Reset all settings to defaults?" confirmation. Then resets. Conflicts with UI-SPEC's "no modal" line. | ✓ |

**User's choice:** Confirm modal before reset.
**Notes:** Selection contradicts UI-SPEC's existing "Reset confirm: inline, no modal" line. Surfaced the conflict; user chose to update UI-SPEC. Combined with snapshot edit flow, Reset does NOT auto-save — user must click Save afterward. UI-SPEC has been updated as part of this phase.

### UI-SPEC conflict resolution

| Option | Description | Selected |
|--------|-------------|----------|
| Update UI-SPEC to add the Reset modal | Edit 04-UI-SPEC.md so it specifies the confirmation modal. UI-SPEC remains source of truth. | ✓ |
| Drop the modal; trust draft-buffer reversibility | Skip the modal; rely on Discard-on-close. UI-SPEC unchanged. | |
| Keep modal; record as CONTEXT deviation | CONTEXT supersedes UI-SPEC by precedence. UI-SPEC unchanged. | |

**User's choice:** Update UI-SPEC.

---

## Claude's Discretion

- WindowSystem ownership location (NamazuFlippers.cs vs new UI/PluginUi.cs).
- Exact widget-call greps and unit-test scope for tests/phase04_nyquist.sh.
- Concrete dictionary types for bought/listed/auto-collapsed state on DailyRouteWindow.
- In-window Settings button placement within the DailyRouteWindow top button row.
- Exact wording of the unsaved-changes close-prompt and Reset-to-Defaults confirmation modal copy.

## Deferred Ideas

- Lifetime/daily/weekly earnings tracker, completed-route history, per-item P&L over time — new capability, future phase.
- JSON persistence of bought/listed across game restarts — Phase 5.
- Mark All Bought / Mark All Listed bulk actions — Phase 5.
- Shortage Predictor toggle wiring — Phase 6.
- Market-board hook + server-travel auto-advance — Phase 7.
- Configuration schema migration UI — out of scope.
