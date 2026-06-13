# Phase 6: Runtime Hardening & Ledger Foundation - Context

**Gathered:** 2026-06-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 6 stabilizes the existing route workflow under real in-game use and introduces a durable flip-position foundation. It should not become the full realized-profit feature yet. The phase delivers runtime/persistence hardening, a schema-backed ledger store for bought lots, a mark-bought workflow from route results, and a simple open-position correction/debug view.

</domain>

<decisions>
## Implementation Decisions

### Ledger Creation Timing
- **D-01:** Create a durable flip position only when the user marks an item as actually bought. Routed recommendations and listed-only actions should not create ledger history by themselves.

### Position Identity and Duplicates
- **D-02:** Duplicate purchases are expected. The ledger must track quantities, not just item identity.
- **D-03:** Use one lot per buy action. If the user buys multiple copies at once, create one position with that quantity; a later buy creates a separate position.
- **D-04:** The data model should include bought, listed, sold, and remaining quantity counters so partial listing and partial sale workflows can be added cleanly.
- **D-05:** When multiple open lots match the same sold item in later phases, default sale matching should use oldest listed lot first unless the user overrides it.

### Persistence Boundary
- **D-06:** Phase 6 should persist purchase-side lot records first: item id/name, quantity bought, buy date/time, source world, actual unit buy price, expected sell price, planned profit, notes/status, and enough route/session context to trace the buy.
- **D-07:** Include quantity lifecycle fields in the schema now, but Phase 6 only needs to populate bought and remaining quantities from the mark-bought workflow.
- **D-08:** Keep ledger history indefinitely unless the user manually archives or deletes records. Do not use an automatic rolling retention window.
- **D-09:** Ledger storage should use a versioned schema and backup-on-write before saving ledger changes.
- **D-10:** Durable flip positions must survive independently of the current scan cache freshness. The current `scan-cache.json` session envelope is not sufficient as the ledger's canonical storage.

### Hardening Behavior During Scans
- **D-11:** If scan data partially fails or contains stale/invalid segments, keep usable results, mark incomplete/stale parts clearly, and avoid crashing.
- **D-12:** Warnings should be inline but compact: small banners or row-level indicators in the route UI, with detail available through diagnostics/logs.
- **D-13:** Transient scan failures such as timeouts or rate limits should use a small bounded retry/backoff, then continue with partial or stale-safe results plus warnings.
- **D-14:** Diagnostics should be structured summaries, not just raw logs: failure type, affected item/world, timestamp, retry count, and user-facing message.
- **D-15:** Runtime diagnostics should be release-appropriate. Phase 6 should review broad/global exception hooks and heartbeat logging so they do not mask unrelated failures or leave temporary troubleshooting noise in release behavior.

### Phase 6 Visible Surface
- **D-16:** Phase 6 should expose a minimal foundation UI: a "mark bought" action from route results and a simple open positions/debug view.
- **D-17:** Mark bought should confirm quantity and actual unit buy price, defaulting from the route result while allowing correction before saving.
- **D-18:** The Phase 6 positions/debug view should show open lots and allow basic edits/delete for mistakes. It should not become the full polished history UI; that belongs to later phases.

### the agent's Discretion
No broad "you decide" areas were granted. The agent/planner may choose implementation structure, file naming, and exact ImGui layout as long as the decisions above and existing code patterns are respected.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Scope
- `.planning/PROJECT.md` — Defines the current product target: daily route generation plus lightweight flip-profit journaling, not full accounting.
- `.planning/REQUIREMENTS.md` — Defines Phase 6 requirements HARD-01 through HARD-03 and LEDGER-01 through LEDGER-03.
- `.planning/ROADMAP.md` — Defines Phase 6 boundary and downstream phases 7-10 so ledger work does not absorb realized-profit/history/reconciliation scope.

### Prior Phase Context
- `.planning/phases/05-session-persistence/05-CONTEXT.md` — Session persistence decisions and cache-envelope behavior that Phase 6 must harden.
- `.planning/phases/04-core-ui/04-CONTEXT.md` — DailyRouteWindow interaction and UI constraints.
- `.planning/phases/03-scan-engine-route-optimizer/03-CONTEXT.md` — Scan engine, route optimizer, cache, and API assumptions.

### Existing Validation
- `tests/phase05_nyquist.sh` — Source-validation expectations for session persistence; Phase 6 should update/add tests instead of breaking these semantics silently.
- `tests/phase04_nyquist.sh` — UI source-validation constraints for DailyRouteWindow layout and colors.
- `tests/phase03_nyquist.sh` — Scan/cache source-validation constraints.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `NamazuFlippers/Data/ScanCacheStore.cs`: Existing local JSON persistence, config-directory pathing, source-generated JSON serialization, temp-file write, and session save locking. Useful as a pattern, but scan saves and session saves are not fully serialized together yet.
- `NamazuFlippers/Data/ScanCacheEnvelope.cs`: Current cache envelope has schema version 2 and embeds `SessionState`. Good reference for versioned JSON shape, but ledger records should be independent of cache expiry.
- `NamazuFlippers/Data/SessionState.cs`: Current session state stores `Dictionary<int, bool>` for bought/listed. This is intentionally too coarse for duplicate quantities and should not become the durable ledger model.
- `NamazuFlippers/UI/DailyRouteWindow.cs`: Existing route UI has bought/listed interactions, Mark All buttons, status banners, route rows, item name copy behavior, and compact ImGui layout. Mark-bought should integrate here or in a closely related window.
- `NamazuFlippers/Core/RankedOpportunity.cs` and `NamazuFlippers/Core/RouteStop.cs`: Route result types supply item id/name, source world, purchase price, home/list price, expected profit, OOS/vendor flags, and route grouping context needed to seed bought lots.

### Established Patterns
- JSON persistence currently lives under Dalamud's plugin config directory and uses `System.Text.Json` source generation via `ApiJsonContext`.
- The main plugin class owns services and exposes narrow methods/properties to UI windows. Phase 6 should follow that pattern instead of making UI windows own file persistence directly.
- UI is compact ImGui inside `DailyRouteWindow`, with practical inline status text rather than modal-heavy flows.
- Local verification is source validation through shell scripts. GitHub Actions remains the authoritative compile/package gate because local macOS builds may lack Dalamud assemblies.

### Integration Points
- Add a ledger store/service alongside `ScanCacheStore`, not inside the scan cache envelope as the canonical source.
- Add ledger model types under `NamazuFlippers/Data` or a similarly established namespace, and register them in `NamazuFlippers/API/Models/ApiJsonContext.cs` if using source-generated serialization.
- Add a plugin-level method for mark-bought creation so `DailyRouteWindow` can request a durable position without owning persistence.
- Add a minimal positions/debug window or panel reachable from the main UI; it must support viewing and correcting open positions.
- Revisit `NamazuFlippers/NamazuFlippers.cs` scan/save concurrency and diagnostics, especially `QueueSessionSave`, `RunScanAsync`, `DrawWithDiagnostics`, `TaskScheduler.UnobservedTaskException`, and `AppDomain.CurrentDomain.UnhandledException`.

</code_context>

<specifics>
## Specific Ideas

- The completed product should tell the user which bought items sold, what they sold for, net profit, and the buy date tied to that result.
- Teleports, repairs, incidental purchases, and other unrelated gil changes are accepted blind spots.
- The strongest realized-profit signal is expected to come from items sold through three retainers, but Phase 6 should not depend on retainer observability.
- The user currently estimates profit by adding character gil plus gil held by three retainers day to day; the ledger should replace memory-based tracking at the item level.
- Mark-bought confirmation should be lightweight: quantity and actual unit buy price, with defaults from the routed opportunity.

</specifics>

<deferred>
## Deferred Ideas

- Manual sold-state entry, actual sale price capture, tax-adjusted realized profit, and closing positions belong in Phase 7.
- Daily/weekly/monthly realized-profit history and polished open/sold position review belong in Phase 8.
- Retainer/gil observability and any assisted sale matching belong in Phase 9/10 after the durable ledger exists.
- Shortage predictor and opportunity expansion remain backlog work after the profit-history loop is trustworthy.

</deferred>

---

*Phase: 6-Runtime Hardening & Ledger Foundation*
*Context gathered: 2026-06-13*
