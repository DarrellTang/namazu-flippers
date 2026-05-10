# Phase 5: Session Persistence - Context

**Gathered:** 2026-05-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Persist mid-route session state — which items the player has marked **bought** and **listed** — to disk so a player who scans, walks away, and comes back later (next login, plugin reload, game crash) picks up exactly where they left off. Add **Mark All Bought** and **Mark All Listed** bulk actions on the route as a whole.

In scope: extending the Phase 3 `ScanCacheEnvelope` with a `SessionState` payload (Bought + Listed dictionaries), wiring `DailyRouteWindow` to hydrate that state on startup and persist it on every checkbox toggle, and adding two whole-route bulk-action buttons above the progress bars.

Out of scope: shortage predictor merge (Phase 6), market-board / server-travel hooks (Phase 7), per-stop bulk-action controls, lifetime/historical earnings tracking (deferred — see Phase 4 deferred ideas), undercut monitoring (Out of Scope project-wide), `current_stop` cursor tracking (the route is value-ordered per Phase 3 D-21; there is no single "current stop" concept).

</domain>

<decisions>
## Implementation Decisions

### Storage Layout
- **D-01:** Persist session state inside the existing `ScanCacheEnvelope` (one file: `scan-cache.json`). Bump `ScanCacheEnvelope.CurrentSchemaVersion` from `1` to `2`. One atomic write covers both cache and session, the session is structurally bound to the scan it belongs to, and there is no second file to keep in sync.
- **D-02:** When `LoadAnyAsync` finds a v1 envelope, the existing `IsValid` `SchemaVersion` mismatch path already returns `null` and triggers a fresh scan. Reuse that — **no v1→v2 migration code**. Cost is one extra scan after the v1.0.33 update; the developer accepts that.
- **D-03:** `SessionState` carries only `Dictionary<int, bool> Bought` and `Dictionary<int, bool> Listed` keyed by `RankedOpportunity.ItemId`. No `AutoCollapsed` (derivable from bought state on render — Phase 4 D-09 already recomputes it each frame). No `LastModifiedUtc` (not needed for any decision in Phase 5).

### Save Trigger / I/O Cadence
- **D-04:** Flush to disk **on every checkbox toggle** in `DailyRouteWindow.DrawItems` — both for individual checkboxes and for the bulk Mark All buttons. Typical session is 5–10 items × ~2 toggles = ~20 writes total; the existing temp-file-then-rename atomic pattern absorbs that easily.
- **D-05:** Dispatch saves as **fire-and-forget** `Task.Run(() => sessionStore.SaveAsync(...))` from inside the ImGui frame callback so the UI thread never blocks on disk I/O. Last-write-wins is acceptable because state is monotonic in normal use (each checkbox click flips one bit; the most recent click is the truth). A small internal lock (or `Interlocked` flag) inside the store serializes overlapping writes.
- **D-06:** **Silent log on save failure.** If `SaveAsync` throws (disk full, AV scan locking the file, etc.), `log.Warning("/nflip: could not save session state: {Message}")` and continue. No status banner, no chat message. Session state is convenience, not gameplay-critical, and the next successful click writes the same dictionaries again.

### Session Validity / Wipe Rules
- **D-07:** Session lifetime **equals cache envelope lifetime**. No separate wipe triggers. If `ScanCacheStore.IsValid` accepts the envelope (schema match, not expired, fingerprint match), the embedded session is restored too. If the envelope is invalid (cache expired by `CacheDurationHours`, scan-affecting config changed → fingerprint differs, schema bumped), the envelope is discarded and the next scan starts a clean session. **No FFXIV daily-reset (8am UTC) wipe** — the 4h default cache duration already enforces freshness, and a player mid-session at 7:55 UTC should not lose state at 8:00.
- **D-08:** **Hydrate on first load; wipe only on subsequent Rescan.** `DailyRouteWindow` reads bought/listed from `LatestScanResult`'s associated envelope on the first frame after the result becomes non-null. The Phase 4 D-09 reference-change wipe still fires for actual `RescanAsync` calls — Rescan writes a brand-new envelope with empty `SessionState`, and the reference-change detection sees the new result and wipes the in-memory dicts. Net effect: cache load = restore; rescan = clean slate. Auto-scan-on-login that returns a `UsingCache` result restores; auto-scan-on-login that hits the API (no valid cache) starts a clean session.
- **D-09:** **Restore is transparent** — no banner, no toast, no log entry beyond what already exists. The progress bars read `3/7`, the bought items are gray/checked, the route renders as the player left it. No "Resumed your session from HH:mm" message. Matches the no-friction, fire-and-forget daily-session feel.

### Bulk Actions UX (SESS-03)
- **D-10:** **Two buttons total**, scoped to the **whole route**: `Mark All Bought` and `Mark All Listed`. No per-stop variants. Matches SESS-03 verbatim and keeps the surface minimal.
- **D-11:** **Placement: a new row above the progress bars**, after the `Bought: X/Y   Listed: X/Y` text and before the two `ProgressBar` calls. Logically grouped with the counters they affect, and avoids the GAP-E1 / 04-08 right-edge pixel-budget fight in the Settings/Rescan row (Settings 80 + Rescan 110 + 2× ~120px Mark All would overflow at `GlobalScale > 1.0`).
- **D-12:** **No confirmation modal.** Each individual checkbox is reversible by un-ticking it, so a misclicked Mark All is recoverable item-by-item. Phase 4's modal pattern (D-12 unsaved-changes prompt, D-13 Reset-to-defaults) is reserved for genuinely destructive ops; Mark All is not destructive — just bulk-reversible.
- **D-13:** **Both buttons always enabled.** No "must finish bought before listed" gate. The per-item checkboxes already permit listing an unbought item, and the Mark All buttons should mirror that permissiveness. Consistency over enforcement.

### Claude's Discretion
- Where the new `SessionStore` (or extended `ScanCacheStore`) class lives in code, and whether session save/load is a new class or a method-pair on `ScanCacheStore` — both are equally clean given session lives inside the existing envelope.
- The exact `SessionState` class name, namespace placement (under `Data/` is consistent with `ScanCacheEnvelope`), and whether it's a separate file or nested inside `ScanCacheEnvelope.cs`.
- The exact serialization wiring for `Dictionary<int, bool>` under `ApiJsonContext` source-generation (System.Text.Json with the existing `JsonSerializerContext` pattern).
- The lock/serialization strategy for overlapping fire-and-forget saves (a simple `SemaphoreSlim`, an `Interlocked`-guarded "save pending" flag, or a single-slot `Channel<>`) — pick the simplest that prevents partial-write races.
- The exact button labels and styling (`Mark All Bought` vs `Mark All as Bought` vs `All Bought`), button widths, and whether the new row uses a `SameLine` pair or the `Columns`/`Table` API.
- The shape of `tests/phase05_nyquist.sh` source-validation checks (which calls/types to assert: `SchemaVersion = 2`, `SessionState` type presence, save-on-toggle wiring, hydrate-on-load path, Mark All button labels).
- Whether the v1→v2 schema bump warrants a changelog/release-notes line for users on the custom Dalamud repo (small UX courtesy, not a hard requirement).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase and Requirements
- `.planning/ROADMAP.md` §Phase 5 — Goal, success criteria (4 items), 1-plan split.
- `.planning/REQUIREMENTS.md` §Session Persistence — SESS-01 (JSON persistence of route + bought/listed + current stop), SESS-02 (resume on next login if scan not expired), SESS-03 (Mark All Bought / Mark All Listed bulk actions).
- `.planning/PROJECT.md` — Daily-session value proposition, JSON-file persistence as the chosen storage model, single-player / single-home-world constraints.

### Prior Phase Context
- `.planning/phases/03-scan-engine-route-optimizer/03-CONTEXT.md` §Scan Cache Boundary (D-25..D-30) — Cache lives in `pluginInterface.ConfigDirectory/scan-cache.json`, schema-versioned envelope, config fingerprint, atomic temp-file-then-rename writes. **Phase 5 extends this envelope rather than introducing a second file.**
- `.planning/phases/04-core-ui/04-CONTEXT.md` §Bought/Listed State Lifecycle (D-09, D-10, D-11) — Reference-change wipe rule, dimmed empty progress section, in-memory state survives close+reopen but not cross-session ("Phase 5 will lift this same state into JSON for cross-session persistence" — explicit hand-off).
- `.planning/phases/04-core-ui/04-UI-SPEC.md` §Interaction Contracts — Rescan Route button placement and the GAP-E1 right-edge pixel budget (informs D-11's "new row above progress bars" placement).

### Product / Spec
- `SPEC.md` §Session Persistence — Reference JSON shape (`scan_date`, `home_world`, `items[].bought`, `items[].listed`, `route[]`, `current_stop`). **Note:** `current_stop` is intentionally NOT modeled in Phase 5 — Phase 3 D-21 makes the route value-ordered, so there is no "current stop" cursor concept. Bought/listed dictionaries fully represent progress.
- `SPEC.md` §Caching Strategy — "Session state (bought/listed checkmarks) persists in the same cache file" matches D-01.
- `SPEC.md` §UI: DailyRouteWindow — Confirms `[Rescan] [Mark All Bought] [Mark All Listed]` action set; D-10/D-11 implement it.

### Existing Code (mandatory reading before implementation)
- `NamazuFlippers/Data/ScanCacheStore.cs` — `LoadAnyAsync`, `LoadValidAsync`, `SaveAsync`, `IsValid`, `CreateConfigFingerprint`. **Phase 5 either extends this class with session-aware methods or adds a sibling `SessionStore` that shares the same envelope file.**
- `NamazuFlippers/Data/ScanCacheEnvelope.cs` — `CurrentSchemaVersion = 1`, `RawResponse`, `DerivedResult`. **Phase 5 bumps to `2` and adds `SessionState`.**
- `NamazuFlippers/UI/DailyRouteWindow.cs` — `boughtState`, `listedState`, `autoCollapsedStops`, `lastSeenResult` reference-change detection (line 70), `DrawItems` checkbox handlers (lines 227–298), `DrawProgressSection` button row layout (lines 110–171). **Phase 5 wires saves into the checkbox handlers and adds the Mark All row.**
- `NamazuFlippers/Core/ScanEngineResult.cs` — `RouteStops`, `Opportunities`, `CreatedAtUtc`. Bought/Listed dictionaries key off `RankedOpportunity.ItemId` from inside `RouteStops[].Items`.
- `NamazuFlippers/Core/RankedOpportunity.cs` — `ItemId` (the dictionary key), `Name`, `ExpectedDailyProfit` (used in profit tally already).
- `NamazuFlippers/NamazuFlippers.cs` — Plugin entry point. Owns `LatestScanResult`, `ScanCacheStore`, `ScanEngine`, `RescanAsync`, `OpenConfigWindow`. **Phase 5 needs a new `SessionStore` (or extended cache store) wired here, plus a way for `DailyRouteWindow` to call save and load.**
- `NamazuFlippers/API/Models/ApiJsonContext.cs` — Source-generated `JsonSerializerContext`. New `SessionState` type and `Dictionary<int, bool>` need entries.
- `tests/phase03_nyquist.sh`, `tests/phase04_nyquist.sh` — Source-validation precedent. Phase 5 follows the same shape.

### Build Verification Policy
- `.planning/STATE.md` and `.planning/PROJECT.md` Constraints — **GitHub Actions is the authoritative compile/package gate.** macOS `dotnet build` is expected to fail locally because Dalamud assemblies are unavailable. Use `tests/phase05_nyquist.sh` (to be authored) for local source validation, and rely on CI for compile + package + release.

### External (reference)
- Dalamud `IDalamudPluginInterface.ConfigDirectory`: https://dalamud.dev/api/Dalamud.Plugin/Interfaces/IDalamudPluginInterface — already used by `ScanCacheStore`; same path for the extended envelope.
- System.Text.Json source-generation guide: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation — for `Dictionary<int, bool>` registration in `ApiJsonContext`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ScanCacheStore.SaveAsync` already implements the temp-file-then-rename atomic write pattern. Phase 5 either reuses this method (if it gains a `SessionState` parameter) or copies the pattern into a parallel `SessionStore.SaveAsync` that targets the same file.
- `ScanCacheStore.LoadAnyAsync` returns the full envelope, so adding `SessionState` as an envelope field means session restore is "free" — read the same envelope, take the new field.
- `ScanCacheStore.IsValid` already gates schema-version, expiry, and fingerprint mismatches; the v1 envelope auto-discard relies on this with no new code (D-02).
- `DailyRouteWindow.lastSeenResult` reference-change detection (line 70) is the exact hook point for the wipe-on-rescan side of D-08; Phase 5 just needs to add a parallel "if first time we see this result and envelope has SessionState, hydrate" branch.
- `DailyRouteWindow.boughtState` / `listedState` dictionaries already store exactly the data shape we want to persist (`Dictionary<int, bool>` keyed by `ItemId`).

### Established Patterns
- **Dalamud plugin-config-directory file persistence** with schema-versioned JSON envelopes (Phase 3 D-25..D-30). Phase 5 stays inside this pattern by extending the same envelope.
- **Discard-on-mismatch is preferred over migration** for caches (Phase 3 D-27 invalidates by schema version). Phase 5 D-02 reaffirms this for the v1→v2 jump.
- **Source-validation tests via `tests/phaseNN_nyquist.sh`** (Phase 3, Phase 4). Phase 5 follows with `tests/phase05_nyquist.sh`.
- **Fire-and-forget async dispatch from ImGui frame callbacks** (`NamazuFlippers.OnCommand` line 118 → `_ = RunScanAsync(...)`). Phase 5 D-05 reuses this idiom for save dispatch.
- **Single-bundled-plan for small phases** — per `feedback_bundling.md`, Phase 5's three concerns (envelope extension, save/load wiring, Mark All UI) all touch the same handful of files (`ScanCacheStore.cs` / `ScanCacheEnvelope.cs` / `DailyRouteWindow.cs` / `NamazuFlippers.cs`) and share one re-test path; the ROADMAP's 1-plan split is the right granularity.

### Integration Points
- `NamazuFlippers.cs` constructor (line 71): `ScanCacheStore` is constructed here. Phase 5 either extends this construction or adds a sibling `SessionStore` constructed alongside, then passes the relevant interface into `DailyRouteWindow` via its constructor (currently takes `(this, log)` — line 75).
- `NamazuFlippers.RunScanAsync` (line 160): on `forceRefresh: true`, the new envelope written by `ScanEngine` (via `ScanCacheStore.SaveAsync`) starts with empty `SessionState`. The reference-change in `LatestScanResult` then triggers `DailyRouteWindow`'s wipe — which is now also a "use envelope's empty SessionState" hydrate. No additional Rescan plumbing needed.
- `NamazuFlippers.QueueAutoScan` (line 144): `forceRefresh: false` returns `UsingCache` when valid; the cache load now also brings session state into the window's view.
- `DailyRouteWindow.DrawItems` checkbox handlers (lines 228–229 for bought, 297–298 for listed): each `if (ImGui.Checkbox(...)) dict[key] = value` branch is the exact site to add `_ = sessionStore.SaveAsync(...)`.
- `DailyRouteWindow.DrawProgressSection` (after line 125's `ImGui.Text` Bought/Listed counter, before line 159's first `ProgressBar`): the insertion point for the new Mark All button row (D-11).
- `ApiJsonContext` (`API/Models/ApiJsonContext.cs`): needs entries for the new `SessionState` type and `Dictionary<int, bool>` so source-generated serialization handles them.

</code_context>

<specifics>
## Specific Ideas

- **The `scan-cache.json` filename does not change** — the file path stays at `pluginInterface.ConfigDirectory/scan-cache.json`. Only the envelope's internal shape grows.
- **`SessionState` should round-trip cleanly through `System.Text.Json` source-generation** — `Dictionary<int, bool>` serializes as `{"12345": true}` with string-stringified int keys. That's fine; the `ItemId` keys are the canonical wire format too.
- **Mark All button order: Bought first, Listed second**, matching the `Bought: X/Y   Listed: X/Y` counter row above. Visual left-to-right alignment with the labels users already read.
- **Mark All Bought sets `bought = true` for every item in `result.RouteStops.SelectMany(stop => stop.Items)`** — same iteration the progress section already uses (line 116). Mark All Listed does the same against `listed`. Both then trigger one save.
- **Test posture stays the same as Phase 3/4**: source-validation locally via `tests/phase05_nyquist.sh`, in-game UAT after CI build 1.0.33.x produces a release.

</specifics>

<deferred>
## Deferred Ideas

- **`current_stop` cursor / "where am I in the route" pointer** — Phase 3 D-21 chose value-first stop ordering, which makes a single "current stop" concept incoherent. SPEC.md's reference JSON shape includes `current_stop` but Phase 5 explicitly does not model it.
- **Auto-collapsed stop persistence** — Considered and rejected (D-03). If a future phase wants snappier first-frame restore, lift the collapsed-stop map into `SessionState`. Today's redraw recomputes it without user-visible delay.
- **`LastModifiedUtc` on session state** — Considered for "you last touched this 2h ago" UX (D-03 alt). Not needed for any Phase 5 behavior; revisit if a future stats/history phase wants it.
- **Per-stop "Mark all in this stop" buttons** — Considered (D-10 alt). Defer to a future polish phase if player feedback asks for it; SESS-03 only covers whole-route bulk actions.
- **Save retry / queued retry on failure** — D-06 chose silent log because Phase 5 saves are idempotent (each click rewrites the dictionaries). If telemetry later shows real save loss, add a one-shot retry inside the dispatch path.
- **Lifetime / daily / weekly earnings tracker** — Already deferred from Phase 4. Stays deferred. New stats UI is its own phase, not a Phase 5 concern.
- **FFXIV daily-reset (8am UTC) wipe** — Considered (D-07 alt). Cache duration (4h default) already enforces freshness; a player scanning at 7:55 UTC should not lose state at 8:00.
- **v1→v2 envelope migration code path** — Considered (D-02 alt). Discard-and-rescan is the right cost trade for a single-player local cache.

</deferred>

---

*Phase: 05-session-persistence*
*Context gathered: 2026-05-09*
