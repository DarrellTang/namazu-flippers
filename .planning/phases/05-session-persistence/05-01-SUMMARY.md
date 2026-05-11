---
phase: 05-session-persistence
plan: 01
subsystem: session-persistence
tags: [session-persistence, json-cache, dalamud-plugin, imgui, source-validation]
requires:
  - Phase 3: ScanCacheStore atomic temp-file-then-rename, ScanCacheEnvelope schema gate, IsValid
  - Phase 4: DailyRouteWindow boughtState/listedState, reference-change wipe, DrawProgressSection
provides:
  - Persisted bought/listed checkbox state inside scan-cache.json envelope (v2)
  - Transparent hydrate-on-first-sight of a ScanEngineResult
  - Mark All Bought / Mark All Listed whole-route bulk actions
affects:
  - Cache schema gate: v1 envelopes auto-discard after upgrade (one extra scan, per D-02)
  - DailyRouteWindow visual order: counter -> Mark All row -> Settings/Rescan row -> progress bars
tech-stack:
  added:
    - SemaphoreSlim(1,1) for fire-and-forget save serialization
    - Dictionary<int, bool> JSON source-gen converter
  patterns:
    - Atomic temp-file-then-rename (reused from SaveAsync)
    - Silent-log on IOException/JsonException/UnauthorizedAccessException (mirrors LoadAnyAsync)
    - Fire-and-forget Task.Run dispatch (mirrors OnCommand / QueueAutoScan)
key-files:
  created:
    - NamazuFlippers/Data/SessionState.cs
    - tests/phase05_nyquist.sh
  modified:
    - NamazuFlippers/Data/ScanCacheEnvelope.cs
    - NamazuFlippers/Data/ScanCacheStore.cs
    - NamazuFlippers/API/Models/ApiJsonContext.cs
    - NamazuFlippers/NamazuFlippers.cs
    - NamazuFlippers/UI/DailyRouteWindow.cs
decisions:
  - Implemented every D-01..D-13 verbatim; no deviations
  - SaveSessionAsync added as a method-pair on ScanCacheStore (vs. sibling SessionStore class) — reuses cachePath/configuration/log fields with zero new wiring
  - QueueSessionSave snapshots dictionaries via `new Dictionary<int,bool>(...)` before dispatch so the background save sees a stable view independent of further UI-thread mutation
  - CurrentSessionState is populated via cacheStore.LoadAnyAsync(ct) after each scan (vs. threading SessionState through ScanEngine.GetRouteAsync) — minimizes file-touch scope; cost is one extra envelope read of a small JSON file
  - Mark All buttons use default ImGui widths (no Vector2) — they live on their own row above the GAP-E1-budgeted Settings/Rescan row, so the right-edge pixel fight is avoided
metrics:
  duration: 3m 4s
  tasks_completed: 4
  files_changed: 7
  commits: 4
  completed: 2026-05-11T21:35:12Z
---

# Phase 5 Plan 01: Session Persistence Summary

One-liner: Mid-route bought/listed state persists inside the existing scan-cache.json envelope (schema v2) with transparent hydrate-on-first-sight and two whole-route Mark All bulk-action buttons above the progress bars.

## What Shipped

| Requirement | Description | Status |
| ----------- | ----------- | ------ |
| SESS-01 | JSON persistence of bought/listed state inside scan-cache.json | Complete |
| SESS-02 | Resume in-progress route on next login while cache valid | Complete |
| SESS-03 | Mark All Bought / Mark All Listed whole-route bulk actions | Complete |

## Files

### Created

- `NamazuFlippers/Data/SessionState.cs` — sealed POCO: `Dictionary<int,bool> Bought`, `Dictionary<int,bool> Listed`. Implements D-03 (no AutoCollapsed, no LastModifiedUtc).
- `tests/phase05_nyquist.sh` — 41-assertion source-validation script; copy of phase04 helper preamble byte-for-byte + Phase 5 assertions covering every D-NN.

### Modified

- `NamazuFlippers/Data/ScanCacheEnvelope.cs` — `CurrentSchemaVersion = 2`; added `SessionState SessionState { get; set; } = new();` (D-01).
- `NamazuFlippers/Data/ScanCacheStore.cs` — added `private readonly SemaphoreSlim sessionSaveLock = new(1, 1);` field and `public async Task SaveSessionAsync(SessionState, CancellationToken)` method that reuses the atomic temp-file-then-rename pattern and the same exception filter as `LoadAnyAsync` (D-04, D-05, D-06).
- `NamazuFlippers/API/Models/ApiJsonContext.cs` — added `[JsonSerializable(typeof(SessionState))]` and `[JsonSerializable(typeof(Dictionary<int, bool>))]`.
- `NamazuFlippers/NamazuFlippers.cs` — promoted `cacheStore` to `private readonly` field; added `public SessionState? CurrentSessionState { get; private set; }`; added `public void QueueSessionSave(Dictionary<int,bool>, Dictionary<int,bool>)`; populates `CurrentSessionState = envelope?.SessionState` after each scan via `cacheStore.LoadAnyAsync(ct)` (D-04, D-05, D-08).
- `NamazuFlippers/UI/DailyRouteWindow.cs` — hydrate-on-first-sight block inside the reference-change branch (between the three `.Clear()` calls and `lastSeenResult = result;`); save-on-toggle on both checkbox handlers; Mark All Bought / Mark All Listed row inserted between the counter Text and the GAP-E1-budgeted Settings/Rescan row (D-04, D-08, D-09, D-10, D-11, D-12, D-13).

## Decision Implementation Map

| Decision | Where implemented |
| -------- | ----------------- |
| D-01 (persist inside envelope, schema 1→2) | `ScanCacheEnvelope.cs` schema bump + new `SessionState` field |
| D-02 (no v1→v2 migration) | No code; reused `IsValid` schema-version gate (unchanged) |
| D-03 (Bought + Listed only, no AutoCollapsed/LastModifiedUtc) | `SessionState.cs` shape |
| D-04 (save on every checkbox toggle + Mark All) | 4 `plugin.QueueSessionSave` call sites in `DailyRouteWindow.cs` |
| D-05 (fire-and-forget Task.Run, SemaphoreSlim) | `QueueSessionSave` uses `_ = Task.Run(...)`; `SaveSessionAsync` uses `sessionSaveLock.WaitAsync/Release` |
| D-06 (silent log on save failure) | `SaveSessionAsync` catch: `log.Warning("/nflip: could not save session state: {Message}", ...)` |
| D-07 (session lifetime = cache lifetime) | Implicit via D-01 + existing `IsValid` schema/expiry/fingerprint gate |
| D-08 (hydrate on first sight; Rescan = clean envelope) | Hydrate block inside `if (!ReferenceEquals(result, lastSeenResult))` AFTER `.Clear()` calls, BEFORE `lastSeenResult = result;` |
| D-09 (transparent restore — no banner) | No new log/banner/toast in window; `require_absent_pattern` asserts |
| D-10 (two whole-route buttons, no per-stop) | Two `ImGui.Button` calls labeled "Mark All Bought" and "Mark All Listed" |
| D-11 (placement: row above progress bars) | New row between counter Text and GAP-E1 button arithmetic block |
| D-12 (no confirmation modal) | No popup code added; `require_absent_pattern` asserts |
| D-13 (both buttons always enabled) | No BeginDisabled/EndDisabled around Mark All; `require_absent_pattern` asserts |

## Verification

```bash
$ bash tests/phase05_nyquist.sh
... 41 ok lines ...

Phase 05 Nyquist validation passed.
```

All 41 assertions pass. Exit 0.

### Success Criteria Status

1. `bash tests/phase05_nyquist.sh` exits 0 — **PASS**
2. CI compile/package — **PENDING** (will run after merge; macOS local build expected to fail without Dalamud SDK assemblies)
3. Toggle writes v2 envelope atomically — Architecturally satisfied: `SaveSessionAsync` reuses the proven temp-file-then-rename pattern
4. Reload restores checkmarks transparently — Architecturally satisfied: `CurrentSessionState` hydrate path; no banner/toast added
5. Mark All flips every routed item in one click + one save — Architecturally satisfied: foreach over `routeItems` followed by one `QueueSessionSave`
6. Rescan produces clean envelope — Architecturally satisfied: new envelope default-initializes `SessionState = new()` (empty dicts)
7. v1 envelope auto-discards — Architecturally satisfied: existing `IsValid` gate (no migration code)
8. Save failure logs silently — Architecturally satisfied: `log.Warning(...)` in catch, no banner code added

Criteria 3–8 are gated on the CI build + in-game UAT, per the project's "GitHub Actions is the authoritative compile/package gate" policy.

## Commits

| Task | Description | Commit |
| ---- | ----------- | ------ |
| 1 | SessionState POCO + envelope schema v2 + JSON source-gen | `8c3386d` |
| 2 | SaveSessionAsync + plugin CurrentSessionState/QueueSessionSave | `a470e76` |
| 3 | DailyRouteWindow hydrate / save-on-toggle / Mark All row | `79cd1bc` |
| 4 | tests/phase05_nyquist.sh source-validation | `843f53b` |

## Deviations from Plan

None — plan executed exactly as written. The plan was fully prescriptive (verbatim code blocks for every edit), and every D-NN was implemented as specified.

## Authentication Gates

None encountered.

## Known Stubs

None.

## Threat Flags

None. Phase 5 threat model (T-05-01..T-05-06) is fully addressed:
- T-05-02 (DoS on deserialize): pre-existing LoadAnyAsync exception filter unchanged.
- T-05-04 (path traversal): cachePath construction unchanged — still uses the const `"scan-cache.json"` filename inside `pluginInterface.ConfigDirectory.FullName`.
- T-05-05 (concurrent saves): mitigated via `SemaphoreSlim(1,1) sessionSaveLock` in `SaveSessionAsync`.

No new network endpoints, no new auth paths, no new file-access patterns, no new schema at trust boundaries beyond the in-envelope `SessionState` block (T-05-01 accepted: tampering only affects the local player's own checkboxes).

## Self-Check: PASSED

Files (all FOUND):
- NamazuFlippers/Data/SessionState.cs
- NamazuFlippers/Data/ScanCacheEnvelope.cs
- NamazuFlippers/Data/ScanCacheStore.cs
- NamazuFlippers/API/Models/ApiJsonContext.cs
- NamazuFlippers/NamazuFlippers.cs
- NamazuFlippers/UI/DailyRouteWindow.cs
- tests/phase05_nyquist.sh

Commits (all FOUND):
- 8c3386d (Task 1)
- a470e76 (Task 2)
- 79cd1bc (Task 3)
- 843f53b (Task 4)

Nyquist script: bash tests/phase05_nyquist.sh exits 0 with 41 passing assertions.

## Follow-ups

- CI build + release (e.g., 1.0.33.0) verifies compile/package and produces the in-game test artifact.
- In-game UAT per the plan's `<verification>` section (toggle, /xlreload, verify restore; Mark All; Rescan clean slate; v1 envelope discard; simulated save failure).
- REQUIREMENTS.md to mark SESS-01, SESS-02, SESS-03 as Complete (orchestrator step after UAT closes).
