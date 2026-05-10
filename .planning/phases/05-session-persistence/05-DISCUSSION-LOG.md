# Phase 5: Session Persistence - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-09
**Phase:** 05-session-persistence
**Areas discussed:** Storage layout, Save trigger / I/O cadence, Session validity / wipe rules, Bulk actions UX (Mark All)

---

## Storage layout

### Q1: Where should bought/listed state live on disk?

| Option | Description | Selected |
|--------|-------------|----------|
| Extend ScanCacheEnvelope | Add SessionState to existing scan-cache.json. One atomic write, one schema version, session can never reference a missing scan. | ✓ |
| Separate session-state.json | New file alongside scan-cache.json. Session writes don't churn the cached scan response. Need a back-reference. | |
| Append to Configuration | Save into the Dalamud-managed config blob via SavePluginConfig. Mixes ephemeral session state with user settings. | |

**User's choice:** Extend ScanCacheEnvelope.
**Notes:** Bumps SchemaVersion 1→2; existing migrate-or-discard path handles old files.

### Q2: What should happen when loading a v1 (pre-Phase-5) cache?

| Option | Description | Selected |
|--------|-------------|----------|
| Discard old cache | Treat schema mismatch as missing cache; trigger fresh scan. No migration code. | ✓ |
| Migrate v1→v2 in place | Read v1, default SessionState to empty, save as v2. Player keeps cached scan. | |
| Migrate AND seed empty session | Same as #2, named/testable function. | |

**User's choice:** Discard old cache.
**Notes:** Cost is one extra scan on first launch after the v1.0.33 update — accepted.

### Q3: What does SessionState contain?

| Option | Description | Selected |
|--------|-------------|----------|
| Just bought + listed maps | Dictionary<int,bool> Bought / Listed. autoCollapsed is derivable. | ✓ |
| Bought + Listed + AutoCollapsed | Persists collapsed UI state too. Snappier first-frame. | |
| Bought + Listed + LastModifiedUtc | Adds a "last touched" timestamp for future telemetry/UX. | |

**User's choice:** Just bought + listed maps.
**Notes:** Minimal surface; matches what the user actually clicks.

---

## Save trigger / I/O cadence

### Q1: When should bought/listed state get flushed to disk?

| Option | Description | Selected |
|--------|-------------|----------|
| On every checkbox change | Crash-safe. ~10-20 writes/session via existing atomic temp-file pattern. | ✓ |
| Debounced (~500ms) | Coalesces clicks. Adds Timer/Task-cancellation. Brief crash-loss window. | |
| On window close + Dispose only | Lowest IO. Defeats SESS-02 if game crashes mid-session. | |

**User's choice:** On every checkbox change.

### Q2: How should saves be dispatched (saves are async; ImGui frame must not block)?

| Option | Description | Selected |
|--------|-------------|----------|
| Fire-and-forget Task | _ = Task.Run(() => sessionStore.SaveAsync(...)). Last-write-wins, internal lock to serialize. | ✓ |
| Queue to a serial background channel | Channel<SessionState> drained by a single writer. Strong ordering. More plumbing. | |
| Synchronous write inline | File.WriteAllText sync. UI may stutter on slow disk. | |

**User's choice:** Fire-and-forget Task.

### Q3: If a save throws, what should the player see?

| Option | Description | Selected |
|--------|-------------|----------|
| Silent log, no UI signal | log.Warning(...) and continue. Session state is convenience, not gameplay. | ✓ |
| Inline status banner | "Couldn't save session" line under the Status Banner. | |
| Chat-log message + banner | Full Phase 2 D-01 treatment: chat + banner. | |

**User's choice:** Silent log, no UI signal.

---

## Session validity / wipe rules

### Q1: Beyond cache-envelope expiry/fingerprint/schema, are there session-specific wipe triggers?

| Option | Description | Selected |
|--------|-------------|----------|
| No extra triggers — session lifetime = cache lifetime | Session dies with envelope. Simplest mental model. | ✓ |
| Wipe on FFXIV daily reset (8am UTC) | New game day = clean slate, even if cache still valid. | |
| Wipe whenever any item in route changes | Defensive cross-check of route ItemIds. | |

**User's choice:** No extra triggers — session lifetime = cache lifetime.

### Q2: How does wipe-on-change reconcile with restore-on-load?

| Option | Description | Selected |
|--------|-------------|----------|
| Hydrate from envelope on first load; wipe only on subsequent Rescan | Cache load = restore. Rescan = clean slate. Reuses Phase 4 D-09 reference-change. | ✓ |
| Track 'is this the same scan?' via CreatedAtUtc | Compare timestamps to decide hydrate vs wipe. | |
| Always start empty, ignore persisted state | Defeats SESS-02. | |

**User's choice:** Hydrate from envelope on first load; wipe only on subsequent Rescan.

### Q3: Should the UI signal 'restored' state on plugin reload?

| Option | Description | Selected |
|--------|-------------|----------|
| Transparent — just show ticked checkboxes | No banner, no toast. Progress bar reads 3/7, items grayed. | ✓ |
| Subtle log-only signal | log.Information with bought/listed counts. /xllog only. | |
| Status banner: 'Resumed your session from HH:mm' | Visible reassurance via banner. | |

**User's choice:** Transparent — just show ticked checkboxes.
**Notes:** No-friction, fire-and-forget daily-session feel.

---

## Bulk actions UX (Mark All)

### Q1: What scope do the Mark All buttons cover?

| Option | Description | Selected |
|--------|-------------|----------|
| Whole route only | Two top-level buttons. Matches SESS-03 verbatim. | ✓ |
| Per-stop only | "Mark all in this stop" inside each CollapsingHeader. | |
| Both — whole-route AND per-stop | Top buttons + per-stop controls. Most flexible, costs UI real estate. | |

**User's choice:** Whole route only.

### Q2: Where do Mark All Bought / Listed buttons live in the layout?

| Option | Description | Selected |
|--------|-------------|----------|
| New row above the progress bars | After "Bought: X/Y   Listed: X/Y" text, before progress bars. | ✓ |
| Same row as Settings/Rescan | Pack four buttons in the top right-aligned row. Risks GAP-E1 regression. | |
| Inside an action toolbar at the window bottom | Pinned bottom row. New layout pattern. | |

**User's choice:** New row above the progress bars.
**Notes:** Avoids fighting the GAP-E1 / 04-08 right-edge pixel budget.

### Q3: Confirmation modal for Mark All?

| Option | Description | Selected |
|--------|-------------|----------|
| No modal, action is reversible | Per-item un-tick recovers from misclick. Modal reserved for genuinely destructive ops. | ✓ |
| Modal only when partially complete | Skip modal on no-op states (0 or all already marked). | |
| Always show confirmation | Phase 4 D-12/D-13 modal pattern applied here. | |

**User's choice:** No modal, action is reversible.

### Q4: Should "Mark All Listed" be enabled before all items are bought?

| Option | Description | Selected |
|--------|-------------|----------|
| Both buttons always enabled | Mirrors per-item permissiveness. Player owns the workflow. | ✓ |
| Mark All Listed disabled until all bought | Enforces buy-then-list order. Inconsistent with per-item behavior. | |
| Mark All Listed only marks items already bought | Smart subset: flips listed=true only where bought=true. | |

**User's choice:** Both buttons always enabled.
**Notes:** Consistency with per-item checkboxes over enforcement.

---

## Claude's Discretion

- Where the new SessionStore (or extended ScanCacheStore) class lives in code — both equally clean given session lives inside the existing envelope.
- Exact `SessionState` class name, namespace placement, file layout.
- Exact serialization wiring for `Dictionary<int, bool>` under `ApiJsonContext`.
- Lock/serialization strategy for overlapping fire-and-forget saves (SemaphoreSlim, Interlocked flag, single-slot Channel).
- Exact button labels and styling for Mark All; row layout (SameLine pair vs Columns/Table).
- `tests/phase05_nyquist.sh` shape and assertion set.
- Whether to add a release-notes line for the v1→v2 schema bump (UX courtesy, not required).

## Deferred Ideas

- `current_stop` cursor / "where am I in the route" pointer — incoherent under Phase 3 D-21's value-first stop ordering.
- Auto-collapsed stop persistence — derivable from bought state today; revisit if first-frame perceived as slow.
- `LastModifiedUtc` on session state — not needed for any Phase 5 behavior.
- Per-stop Mark All buttons — defer to a future polish phase if player feedback asks for it.
- Save retry / queued retry on failure — saves are idempotent; revisit if telemetry shows real loss.
- Lifetime / daily / weekly earnings tracker — already deferred from Phase 4.
- FFXIV daily-reset (8am UTC) wipe — cache duration already enforces freshness.
- v1→v2 envelope migration code path — discard-and-rescan is the right trade-off for local cache.
