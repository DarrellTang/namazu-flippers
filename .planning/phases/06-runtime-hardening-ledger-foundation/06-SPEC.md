# Phase 6: Runtime Hardening & Ledger Foundation - Specification

**Created:** 2026-06-13
**Ambiguity score:** 0.09 (gate: <= 0.20)
**Requirements:** 9 locked

## Goal

The existing route workflow changes from cache-bound bought/listed checkboxes to a hardened runtime flow with independent durable bought-lot records that survive reloads and seed later realized-profit tracking.

## Background

Phase 5 persists route session state inside `scan-cache.json` by adding `SessionState` to `ScanCacheEnvelope`. `DailyRouteWindow` currently tracks bought/listed state with `Dictionary<int, bool>` keyed by item id, and `NamazuFlippers.QueueSessionSave` writes those dictionaries through `ScanCacheStore.SaveSessionAsync`. This works for a single active route, but it is too coarse for duplicate purchases and is tied to cache lifetime.

The current code also has known hardening risks before ledger work should depend on it: `ScanCacheStore.SaveAsync` and `SaveSessionAsync` do not share one write gate, UI actions can occur while scans replace route/session state, `CreateConfigFingerprint` omits `MinSalesPerDay`, and broad runtime diagnostics such as draw heartbeat logging plus global exception hooks were added during live troubleshooting. No durable flip-position store exists today.

## Requirements

1. **Serialized cache writes**: All writes to `scan-cache.json` must be serialized through one write path so scan saves and session saves cannot interleave, corrupt the file, or roll back newer session state.
   - Current: `SaveSessionAsync` has `sessionSaveLock`, but `SaveAsync` writes the same file independently with its own temp-file move.
   - Target: Scan-cache writes and session-state writes share one serialization mechanism and one atomic temp-file replacement pattern.
   - Acceptance: A source-validation check can prove both `SaveAsync` and `SaveSessionAsync` acquire the same write gate before writing or moving `scan-cache.json`, and no other cache write path bypasses it.

2. **Cache validity correctness**: Cache fingerprinting must include every configuration value that changes routed scan results.
   - Current: `ScanEngine.IsUsable` filters by `MinSalesPerDay`, but `ScanCacheStore.CreateConfigFingerprint` does not include `MinSalesPerDay`.
   - Target: Changing `MinSalesPerDay` invalidates the existing cache just like changing other scan-affecting settings.
   - Acceptance: A source-validation check confirms `MinSalesPerDay` is included in the fingerprint input and that `tests/phase03_nyquist.sh` or the Phase 6 validation script covers it.

3. **Deterministic in-flight UI behavior**: User actions that mutate route/session/ledger state during an in-flight scan must have deterministic behavior and must not silently lose user state.
   - Current: Mark All and checkbox actions can fire while a scan is in progress; a new scan result can wipe in-memory dictionaries through `lastSeenResult` handling.
   - Target: Mutating route actions are either disabled during scans, queued against a stable route generation, or merged through an explicit rule documented in code and validation.
   - Acceptance: A verifier can identify the chosen in-flight behavior in `DailyRouteWindow`/plugin code, and validation covers toggles or bulk actions while `ScanInProgress` is true.

4. **Release-appropriate diagnostics**: Runtime diagnostics must be scoped to plugin failures and release behavior, not broad temporary hooks from live troubleshooting.
   - Current: `NamazuFlippers` subscribes to `TaskScheduler.UnobservedTaskException` and `AppDomain.CurrentDomain.UnhandledException`, logs draw heartbeats every 60 seconds, and calls `SetObserved` on unobserved task exceptions.
   - Target: Diagnostics needed for release remain intentional, scoped, and non-suppressing; temporary heartbeat/global exception behavior is removed or gated behind an explicit diagnostics setting.
   - Acceptance: A source-validation check confirms release code does not globally suppress unrelated task/application failures and does not emit periodic draw-heartbeat logs during normal use.

5. **Independent ledger persistence**: Bought-lot records must persist independently of scan-cache freshness.
   - Current: There is no ledger store; the only persisted route state is the cache envelope, which expires or invalidates with scan settings.
   - Target: A local JSON ledger file under the Dalamud plugin config directory stores durable flip positions with schema versioning and backup-on-write.
   - Acceptance: A verifier can delete or invalidate `scan-cache.json` without deleting the ledger file, reload the plugin, and still load existing open positions; source validation confirms schema version and backup-on-write behavior.

6. **Bought-lot schema**: Each durable position must represent one buy action and must support duplicate quantities.
   - Current: Bought/listed state is `Dictionary<int, bool>` keyed by `ItemId`, so duplicate lots and quantities cannot be represented.
   - Target: Each mark-bought action creates one lot with item id/name, buy timestamp, source world, actual unit buy price, expected sell price, planned unit profit, bought quantity, listed quantity, sold quantity, remaining quantity, status, and route/session trace fields.
   - Acceptance: Source validation confirms the ledger model includes these fields or equivalent named fields, and that multiple positions can exist for the same item id.

7. **Mark-bought workflow**: Route results must expose a mark-bought action that creates a durable lot only after user confirmation.
   - Current: Route rows expose a bought checkbox that only mutates session state and cannot capture actual quantity or actual buy price.
   - Target: From a route row, the user can confirm quantity and actual unit buy price, defaulted from the route result, and save a durable lot.
   - Acceptance: In the UI code, mark-bought opens or renders a confirmation path with editable quantity and unit buy price; saving creates a ledger position and cancellation creates none.

8. **Open-position correction view**: Phase 6 must include a minimal user-visible view for open ledger positions.
   - Current: No position view exists.
   - Target: The user can open a simple positions/debug view that lists open lots and allows basic correction or deletion for mistakes.
   - Acceptance: A verifier can create an open position, see it in the view after save/reload, edit at least quantity or unit buy price, and delete the mistaken position.

9. **Partial scan warnings and validation coverage**: Scan/runtime failure handling must keep usable results where possible and expose structured warnings.
   - Current: `ScanEngine` can fall back to stale cache on refresh failure and has simple `UserMessage`/`TechnicalDetails`, but no structured diagnostic summary for affected item/world/retry count.
   - Target: Transient scan failures use bounded retry/backoff, usable partial or stale-safe results remain visible, and warnings are compactly visible with structured diagnostic detail.
   - Acceptance: Source validation or tests confirm bounded retry behavior, structured diagnostic fields, inline warning rendering, and updated Phase 3/4/5 validation scripts so they reflect current runtime-discovered semantics.

## Boundaries

**In scope:**
- Harden `scan-cache.json` persistence and session-state mutation behavior.
- Fix scan-affecting cache fingerprint gaps discovered after Phase 5.
- Replace temporary live-troubleshooting diagnostics with release-appropriate diagnostics.
- Add independent local JSON ledger persistence for bought lots.
- Add a bought-lot schema that supports duplicate purchases and quantity lifecycle fields.
- Add a mark-bought route-row workflow with quantity and actual unit buy price confirmation.
- Add a minimal open-position view with basic edit/delete.
- Add or update source-validation scripts for Phase 6 behavior.

**Out of scope:**
- Marking positions sold and entering actual sale price - Phase 7 owns sold-state workflow.
- Realized profit calculation from sale price after tax - Phase 7 owns realized-profit math.
- Daily, weekly, or monthly profit history UI - Phase 8 owns history views.
- Retainer/gil detection or automatic sale matching - Phase 9/10 own game-observed reconciliation.
- Full accounting for teleports, repairs, unrelated income, or incidental purchases - project scope is item-level flip outcomes.
- Shortage predictor or additional opportunity sources - deferred backlog until the profit-history loop is trustworthy.
- A polished final ledger/history dashboard - Phase 6 only needs a minimal open-position correction/debug surface.

## Constraints

- Persistence must use local files under Dalamud's plugin config directory.
- JSON persistence should follow the existing `System.Text.Json` source-generation pattern where practical.
- The ledger must not be stored only inside `scan-cache.json`, because scan cache expiry must not erase flip history.
- UI work must remain compact ImGui consistent with `DailyRouteWindow` and existing window patterns.
- Local macOS verification is source validation only; GitHub Actions remains the authoritative compile/package gate.
- The existing daily route workflow must remain usable after this phase; ledger work cannot break route scanning, bought/listed progress, or list-price display.

## Acceptance Criteria

- [ ] `scan-cache.json` scan saves and session saves share one serialized write path and atomic replacement behavior.
- [ ] Changing `MinSalesPerDay` invalidates the scan cache.
- [ ] Mutating route/session/ledger UI actions during an in-flight scan are disabled, queued, or merged by an explicit validated rule.
- [ ] Release code no longer emits periodic draw-heartbeat logs during normal use and does not globally suppress unrelated task/application exceptions.
- [ ] A versioned ledger JSON file persists bought lots independently of scan-cache expiry or invalidation.
- [ ] Ledger saves create or maintain a backup before overwriting durable position data.
- [ ] Mark-bought from a route row confirms quantity and actual unit buy price before creating one durable lot.
- [ ] Multiple open lots for the same item id can exist at the same time, each with its own buy timestamp and quantity fields.
- [ ] The positions/debug view lists open lots and supports basic edit/delete for mistakes.
- [ ] Scan warnings for partial/stale/failure cases are visible inline or in-row and have structured diagnostic details available.
- [ ] Phase 6 validation covers the hardening and ledger foundation behaviors, and existing source-validation scripts are updated when their assumptions are stale.

## Ambiguity Report

| Dimension          | Score | Min   | Status | Notes |
|--------------------|-------|-------|--------|-------|
| Goal Clarity       | 0.94  | 0.75  | met    | Phase goal is grounded in runtime hardening plus bought-lot ledger foundation. |
| Boundary Clarity   | 0.95  | 0.70  | met    | Sold-state, history, retainer detection, and shortage expansion are explicitly deferred. |
| Constraint Clarity | 0.84  | 0.65  | met    | Local JSON, Dalamud config directory, ImGui, source validation, and CI compile gate are known. |
| Acceptance Criteria| 0.88  | 0.70  | met    | Requirements have concrete pass/fail checks. |
| **Ambiguity**      | 0.09  | <=0.20| met    | Gate passed after Phase 6 context discussion. |

Status: met = dimension meets or exceeds minimum.

## Interview Log

| Round | Perspective | Question summary | Decision locked |
|-------|-------------|------------------|-----------------|
| 1 | Researcher | What exists now and what is missing? | Current state has cache-bound session dictionaries and no durable ledger. |
| 2 | Simplifier | What is the minimum useful Phase 6 ledger? | Mark bought creates purchase-side lots only; sold-state waits for Phase 7. |
| 3 | Boundary Keeper | What is out of scope for this phase? | Profit history, retainer/gil detection, sale matching, and shortage predictor are deferred. |
| 4 | Failure Analyst | What failures would invalidate the foundation? | Cache/session write races, in-flight UI state loss, bad diagnostics, and cache fingerprint gaps must be addressed. |
| 5 | Seed Closer | Is the spec clear enough to lock? | User chose to write SPEC.md after ambiguity scored 0.09. |

---

*Phase: 06-runtime-hardening-ledger-foundation*
*Spec created: 2026-06-13*
*Next step: $gsd-discuss-phase 6 - implementation decisions (how to build what's specified above)*
