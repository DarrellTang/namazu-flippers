# Phase 4: Core UI - Context

**Gathered:** 2026-05-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the player-facing ImGui surface that turns the Phase 3 `ScanEngineResult` into an
interactive daily-route experience, plus expose the existing `Configuration` model through
a settings window. Phase 4 delivers two new windows (`DailyRouteWindow`, `ConfigWindow`),
migrates the existing `FirstRunWindow` to the same `WindowSystem` foundation, and wires
the windows into the plugin lifecycle.

In scope: route rendering, buy/list checkboxes, profit tally, progress bars, status banners,
OOS highlighting, vendor stop styling, auto-collapse on completion, ConfigWindow controls
for CONF-01..09, gear/Settings access via both Dalamud's `UiBuilder.OpenConfigUi` and an
in-window button.

Out of scope: JSON persistence of bought/listed state across game restarts (Phase 5),
shortage predictor toggle behavior (Phase 6 — control is rendered but inert),
market-board / server-travel hooks (Phase 7), lifetime/historical earnings tracking
(deferred — see Deferred Ideas).

</domain>

<spec_lock>
## Design Locked via UI-SPEC.md

**`04-UI-SPEC.md` is approved and locks the visual/interaction contract.**
Downstream agents MUST read it before planning or implementing.

The UI-SPEC pins down: window dimensions and flags, layout structure, color palette
(Vector4 values + usage rules), typography strategy, status state copy for all five
`ScanEngineStatus` values, widget choice per data field, auto-collapse mechanism,
tooltip behavior, density (`ItemSpacing` overrides, scroll strategy), number formatting,
and the in-memory state model.

Functional requirements UI-01 through UI-08 from `.planning/REQUIREMENTS.md` map directly
into the UI-SPEC and are not re-decided here.

**One UI-SPEC update is required as part of this phase** (see D-12 below):
the `Reset to Defaults` button now uses a confirmation modal instead of inline-only.

</spec_lock>

<decisions>
## Implementation Decisions

### Plan Structure
- **D-01:** Keep ROADMAP's three-plan split: `04-01` DailyRouteWindow layout + item rendering, `04-02` interactions (buy/list checkboxes, profit tally, progress, auto-collapse), `04-03` ConfigWindow.
- **D-02:** Phase 4 ships as a single unit. No intermediate merges to `main`; the three plans build on the same branch and only merge once all UAT passes. Avoids shipping a non-interactive route window users would perceive as broken.
- **D-03:** `04-03` ConfigWindow may run in parallel with `04-01` and `04-02`. ConfigWindow only adds a new file plus a small wiring change in `NamazuFlippers.cs` (`UiBuilder.OpenConfigUi` registration), so merge-conflict surface is minimal. The planner is free to mark it as a parallel-eligible plan.
- **D-04:** Test strategy: a `tests/phase04_nyquist.sh` source-validation script asserts the required ImGui calls and color-usage rules from UI-SPEC are present, plus minimal xUnit-style unit tests for any pure-logic helpers we extract (profit tally calculation, auto-collapse decision predicate, bought/listed state-merge function if we add one). CI remains the authoritative compile/package gate; manual in-game UAT validates feel.

### Window Registration
- **D-05:** Adopt `Dalamud.Interface.Windowing.WindowSystem` with the `Window` base class for **all three** windows: existing `FirstRunWindow` migrates, new `DailyRouteWindow` and `ConfigWindow` start there. One consistent pattern across the plugin.
- **D-06:** Keep `FirstRunWindow` as a dedicated first-run popup (don't fold its single-purpose role into ConfigWindow). The first-run code already works; migrating it to the `Window` base class is a mechanical refactor, not a behavior change.
- **D-07:** Open ConfigWindow through **two** entry points: register `UiBuilder.OpenConfigUi` so XIVLauncher's plugin-list gear icon and `/xlsettings` open it (Dalamud convention), AND add an in-window "Settings" button in the DailyRouteWindow top button row alongside `Rescan Route`. UI-SPEC permits both; we want both for discoverability.
- **D-08:** Where the `WindowSystem` lives (still in `NamazuFlippers.cs` vs a new `UI/PluginUi.cs` indirection) is at the planner's discretion. Decide based on whether the entry point is getting unwieldy after the new wiring.

### Bought/Listed State Lifecycle
- **D-09:** When `LatestScanResult` changes (Rescan, login auto-scan), wipe both `boughtState` and `listedState` dictionaries. Fresh route = fresh slate. No item-level merge, no confirmation modal. Manual Rescan is a deliberate act and the cache normally serves the daily session, so wiping on explicit refresh is acceptable.
- **D-10:** Empty-state behavior: when no scan has run yet OR the scan returned no opportunities, render the status banner copy from UI-SPEC PLUS a dimmed empty progress section (zeroed progress bars, zero profit row). This keeps the layout stable so the window doesn't visually jump when results arrive.
- **D-11:** In-memory bought/listed state survives DailyRouteWindow close+reopen within a single game session. The dictionaries live on the window instance and are NOT cleared when the window is hidden. Phase 5 will lift this same state into JSON for cross-session persistence.

### ConfigWindow Save Semantics
- **D-12:** Edit flow: snapshot Configuration on window open + live-edit + dirty flag + close-prompt + revert-from-snapshot on Discard. Concretely:
  1. When the window opens, copy the current Configuration into a snapshot held by the window.
  2. Sliders/checkboxes/inputs mutate the live Configuration directly.
  3. Track a dirty flag flipped to true on any control change; reset to false on Save.
  4. Save calls `pluginInterface.SavePluginConfig(configuration)` and updates the snapshot to match.
  5. Closing the window while dirty opens a modal: `[Save] [Discard] [Cancel]`. Discard copies the snapshot back into Configuration. Cancel keeps the window open.
- **D-13:** Reset to Defaults requires a confirmation modal: `Reset all settings to defaults?` with `[Reset] [Cancel]`. **This deviates from UI-SPEC's current "no modal" line and the UI-SPEC must be updated as part of this phase.** The button stays red-colored as UI-SPEC defined; only the modal is added. After confirmation, all values revert to the hardcoded defaults from `Configuration.cs`, the dirty flag flips true, and the user must still click Save (consistent with the rest of the edit flow — Reset doesn't auto-save).

### Claude's Discretion
- The exact location of `WindowSystem` ownership (`NamazuFlippers.cs` vs new `UI/PluginUi.cs`) — D-08.
- The exact shape of the `tests/phase04_nyquist.sh` checks (which ImGui calls to grep, which color tokens to assert) and the exact set of pure-logic helpers extracted for unit testing — D-04.
- The exact dictionary types for the bought/listed/auto-collapsed state on `DailyRouteWindow` (the UI-SPEC suggests `Dictionary<int, bool>` and `Dictionary<string, bool>`; the planner may pick different concrete types as long as semantics match).
- The exact placement of the in-window "Settings" button within the DailyRouteWindow top row (button order, spacing, label text — UI-SPEC's button row is the only constraint) — D-07.
- The exact wording of the "unsaved changes" close-prompt and the Reset-to-Defaults confirmation modal — keep them friendly and short.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Specs
- `.planning/phases/04-core-ui/04-UI-SPEC.md` — Approved design contract: window dimensions, layout structure, colors, typography, widget inventory, status states, copywriting, density, number formatting, in-memory state model. **Locked except D-13's Reset modal update.**
- `.planning/ROADMAP.md` §Phase 4 — Goal, requirements list, success criteria, three-plan split.
- `.planning/REQUIREMENTS.md` §Core UI — UI-01 through UI-08 (route rendering, checkboxes, OOS highlighting, profit tally, progress bars, auto-collapse, ConfigWindow CONF-01..09 coverage).
- `.planning/REQUIREMENTS.md` §Configuration — CONF-01 through CONF-09 (the settings ConfigWindow exposes).

### Prior Phase Context
- `.planning/phases/01-plugin-shell/01-CONTEXT.md` — Plugin identity (`/nflip`, `NamazuFlippers` namespace), Dalamud built-in config serialization, minimal-scaffold approach, FirstRunWindow popup pattern.
- `.planning/phases/02-api-integration/02-CONTEXT.md` — API client boundary, deferred shortage predictor (Phase 6 — ConfigWindow renders the toggle but it's inert in Phase 4).
- `.planning/phases/03-scan-engine-route-optimizer/03-CONTEXT.md` — `/nflip scan` command (D-01, D-02), `LatestScanResult` exposure (D-05), auto-scan-on-login behavior (D-06..D-09), distinct vendor stops (D-19), structured `ScanEngineStatus` for empty/error/cache states (D-31..D-37). The UI must consume these without re-defining them.

### Project Context
- `.planning/PROJECT.md` — Daily-session value proposition, technical constraints (Dalamud API, .NET 10, ImGui, single-player, single home world), Out of Scope list.

### Existing Code (mandatory reading before implementation)
- `NamazuFlippers/NamazuFlippers.cs` — Plugin entry point. Owns `LatestScanResult`, `LastApiError`, `scanInProgress`, the `pluginInterface.UiBuilder.Draw += OnDraw` subscription, the existing `firstRunWindow.Draw()` call, and `RunScanAsync(forceRefresh, ct)` which the Rescan button must invoke.
- `NamazuFlippers/FirstRunWindow.cs` — Existing first-run popup. Migrates to `Window` base class in this phase. Pattern reference for ImGui dropdown over `WorldData.KnownWorlds` (ConfigWindow's HomeWorld dropdown reuses this approach).
- `NamazuFlippers/Configuration.cs` — All 14 properties ConfigWindow controls. Notes on defaults, valid ranges, and `MaxBudgetPerItem == 0 disables cap` semantics. The snapshot/discard pattern (D-12) operates on instances of this class.
- `NamazuFlippers/Core/ScanEngineResult.cs` — `ScanEngineStatus` enum (Success / Empty / Error / UsingCache / UsingStaleCache), `Opportunities`, `RouteStops`, `TotalExpectedDailyProfit`, `UserMessage`, `IsFresh`, `CreatedAtUtc`. Banner copy and section visibility key off `Status`.
- `NamazuFlippers/Core/RouteStop.cs` — `PurchaseSource`, `DataCenter`, `IsVendorStop`, `Items`, `TotalExpectedDailyProfit`. The unit `CollapsingHeader`s render around.
- `NamazuFlippers/Core/RankedOpportunity.cs` — `ItemId`, `Name`, `HomePrice`, `PurchaseSource`, `PurchasePrice`, `SalesPerDay`, `ExpectedDailyProfit`, `OutOfStock`, `IsVendorSource`. The fields each item row reads.
- `NamazuFlippers/Data/WorldData.cs` — Existing 85-world list reused by ConfigWindow's HomeWorld dropdown.

### External (for reference; not required reading every plan)
- Dalamud `IDalamudPluginInterface.UiBuilder.OpenConfigUi`: https://dalamud.dev/api/Dalamud.Plugin/Interfaces/UiBuilder
- Dalamud `Dalamud.Interface.Windowing.WindowSystem` + `Window`: https://dalamud.dev/api/Dalamud.Interface.Windowing

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WorldData.KnownWorlds` — already an alphabetical 85-world array. ConfigWindow's HomeWorld dropdown reuses it directly.
- `Configuration.DefaultCategoryFilters`, `FurnitureIds`, `CollectibleIds`, `GlamourIds` — ConfigWindow's three category toggles map cleanly onto these named presets; toggling a category copies/removes the relevant ID array.
- `NamazuFlippers.RunScanAsync(forceRefresh, ct)` — the `Rescan Route` button calls this directly. Already guarded against double-runs via `Interlocked.Exchange`.
- `NamazuFlippers.LatestScanResult` and `LastApiError` — already-exposed view-state for the window to read each frame.
- `FirstRunWindow.cs` — ImGui dropdown precedent (`BeginCombo` + alphabetical world list). ConfigWindow's HomeWorld control mirrors this layout.

### Established Patterns
- Thin plugin entry point that delegates to dedicated classes (Phase 1, D-01..D-08). Phase 4 continues this — the plugin entry point gains a `WindowSystem`, but window logic lives in the window classes.
- Dalamud DI through plugin constructor (Phase 2, Phase 3). New windows take `IDalamudPluginInterface` (for `SavePluginConfig`), `IPluginLog`, and the plugin reference itself (for `LatestScanResult`, `RunScanAsync`).
- Add directories only when needed (Phase 1 D-07). Phase 4 introduces `UI/` containing `DailyRouteWindow.cs`, `ConfigWindow.cs`, plus the migrated `FirstRunWindow.cs` (move from project root into `UI/`). Optionally `UI/PluginUi.cs` if the planner judges D-08 warrants it.
- Configuration mutation pattern (Phase 1) — `pluginInterface.SavePluginConfig(configuration)` is the persistence call. ConfigWindow's Save invokes this; nothing else in Phase 4 writes to disk.
- Source-validation testing (Phase 3, `tests/phase03_nyquist.sh`) — Phase 4 follows the same shape with `tests/phase04_nyquist.sh` (D-04).

### Integration Points
- `NamazuFlippers.cs` constructor: instantiate `WindowSystem`, add the three windows, replace `pluginInterface.UiBuilder.Draw += OnDraw` (or its body) with `pluginInterface.UiBuilder.Draw += windowSystem.Draw`. Register `pluginInterface.UiBuilder.OpenConfigUi += () => configWindow.IsOpen = true`.
- `NamazuFlippers.OnCommand` continues toggling DailyRouteWindow's `IsOpen`. The `/nflip scan` subcommand path is unchanged.
- `NamazuFlippers.Dispose`: ensure the new `WindowSystem` is removed from the draw chain and `OpenConfigUi` handler is unsubscribed.
- DailyRouteWindow reads `LatestScanResult` each frame (already changes detection) and clears its bought/listed dictionaries when a new result is detected (D-09).
- ConfigWindow reads/writes the live `Configuration` instance the plugin already holds; snapshot lives only inside the window (D-12).

</code_context>

<specifics>
## Specific Ideas

- The `Settings` button in DailyRouteWindow's top row should sit next to (not replace) the existing `Rescan Route` button location specified in UI-SPEC. Both need to be visible at once.
- The "unsaved changes" prompt and "Reset to defaults" confirmation are the only two new modals introduced in Phase 4. Both should use plain, friendly copy — no exclamation marks, no jargon. The user wants "Are you sure?"-style clarity, not corporate-app warnings.
- The dimmed-empty progress section (D-10) should reuse the same `Vector4` colors with reduced alpha (e.g., `CompletedGray` for the 0/0 row) rather than introducing a new "empty" color. UI-SPEC's existing palette is sufficient.

</specifics>

<deferred>
## Deferred Ideas

- **Lifetime / daily / weekly earnings tracker, completed-route history, per-item P&L over time.** New capability — needs its own data model, retention policy, probably a stats window. Not a Phase 4 clarification. Capture for the roadmap backlog as a candidate post-v1 phase.
- **JSON persistence of bought/listed state across game restarts** — Phase 5 (SESS-01, SESS-02).
- **Mark All Bought / Mark All Listed bulk actions** — Phase 5 (SESS-03).
- **Shortage Predictor toggle wired up** — Phase 6 (the ConfigWindow control is rendered but inert in Phase 4).
- **Market-board hook + server-travel auto-advance** — Phase 7 (INTG-01, INTG-02).
- **Cross-Configuration migrations / schema versioning UI** — out of scope; Configuration.Version exists for future migration support but no UI is needed yet.

</deferred>

---

*Phase: 04-core-ui*
*Context gathered: 2026-05-06*
