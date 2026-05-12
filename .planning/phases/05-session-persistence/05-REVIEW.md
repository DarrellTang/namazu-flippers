---
phase: 05-session-persistence
reviewed: 2026-05-12
depth: standard
status: issues_found
files_reviewed: 7
counts:
  blocker: 4
  warning: 11
  info: 7
---

# Phase 5 Code Review: Session Persistence

## Files Reviewed

- `NamazuFlippers/API/Models/ApiJsonContext.cs`
- `NamazuFlippers/Data/ScanCacheEnvelope.cs`
- `NamazuFlippers/Data/ScanCacheStore.cs`
- `NamazuFlippers/Data/SessionState.cs`
- `NamazuFlippers/NamazuFlippers.cs`
- `NamazuFlippers/UI/DailyRouteWindow.cs`
- `tests/phase05_nyquist.sh`

## Summary

Implementation cleanly meets D-01..D-13 at the structural level — schema bumped to v2, `SessionState` POCO is minimal, source-gen registered, atomic write pattern reused, fire-and-forget pattern matches Phase 3/4 idioms, Mark All buttons in the right row. Nyquist script passes (41/41).

However, the implementation introduces three concurrency races that the SemaphoreSlim does not actually cover, plus a logic bug where Mark All clicks during a running scan are silently lost, plus a test-script regex bug that makes one assertion always pass. The biggest concern is that the lock is named `sessionSaveLock` but only protects `SaveSessionAsync` against itself — it does not synchronize against `SaveAsync` (called by `ScanEngine.TrySaveCacheAsync`) which writes to the same temp file path. Under normal "user clicks while no scan is running" usage everything works, but auto-scan-on-login, Rescan-during-clicks, or quick relogins can corrupt the cache or drop user state.

---

## BLOCKER Findings

### BLOCKER-01: `SaveAsync` and `SaveSessionAsync` race on the same temp-file path

**File:** `NamazuFlippers/Data/ScanCacheStore.cs:75-86, 88-121`

Both methods write to `cachePath + ".tmp"` (literal `scan-cache.json.tmp`) and then `File.Move(..., overwrite: true)`. `SaveSessionAsync` is serialized against itself by `sessionSaveLock`, but `SaveAsync` (invoked by `ScanEngine.TrySaveCacheAsync` from inside `RunScanAsync`) does not acquire that lock. A user toggle that fires `QueueSessionSave` while a scan completes will collide with the scan's `SaveAsync` on `scan-cache.json.tmp`. Possible outcomes on Windows:

1. `File.Create` IOException in one path — caught silently in `SaveSessionAsync`, caught and logged in `SaveAsync` but scan result is then not persisted (silent cache loss).
2. Content truncated mid-write by the second writer; subsequent `File.Move` of the loser overwrites the winner's good file with garbage. `LoadAnyAsync` logs `JsonException` and discards the cache.
3. Worst: `SaveSessionAsync` reads envelope, `SaveAsync` writes new envelope (new scan data), `SaveSessionAsync` writes the OLD envelope back with new SessionState — the just-completed scan is silently rolled back.

Reachable: `OnLogin` → `QueueAutoScan` (3s delay) → `RunScanAsync` while the user clicks in the route window; `/nflip scan` while clicking; etc.

**Fix:** Acquire `sessionSaveLock` inside `SaveAsync` too (or rename to a general `fileLock` and wrap both methods). Alternatively, use a unique temp file per write (`$"scan-cache.{Guid.NewGuid():N}.tmp"`) AND lock.

### BLOCKER-02: `SaveSessionAsync` clobbers fresh scan data when racing with `SaveAsync`

**File:** `NamazuFlippers/Data/ScanCacheStore.cs:88-121`

Even with BLOCKER-01 fixed at the file-level, the read-modify-write in `SaveSessionAsync` (load envelope → mutate SessionState → write whole envelope) is not atomic with respect to `SaveAsync`. Sequence:

1. T0: `SaveSessionAsync` reads envelope E1 (yesterday's cache + current session).
2. T1: `ScanEngine.SaveAsync` writes envelope E2 (today's fresh API data, empty SessionState).
3. T2: `SaveSessionAsync` writes E1-with-new-SessionState back, overwriting E2's RawResponse/DerivedResult/ExpiresAtUtc/ConfigFingerprint with yesterday's stale data.

Next `LoadValidAsync` sees E1's stale `ExpiresAtUtc`, discards as expired — losing the fresh scan AND its session state.

**Fix:** With BLOCKER-01's lock covering `SaveAsync`, the read-then-write in `SaveSessionAsync` is serialized against new envelope writes — this dissolves.

### BLOCKER-03: Mark All clicks during an in-flight scan are silently lost

**File:** `NamazuFlippers/UI/DailyRouteWindow.cs:142-152, 72-86`

Mark All buttons are always enabled (D-13). If the user clicks `Mark All Bought` while `plugin.ScanInProgress == true`:

1. UI: `boughtState[item.ItemId] = true` for every routed item.
2. UI: `plugin.QueueSessionSave(...)` schedules fire-and-forget save.
3. BG: `SaveSessionAsync` reads envelope, mutates SessionState, writes (may lose-race per BLOCKER-01/02).
4. BG: scan completes, `LatestScanResult = result` (new reference).
5. Next UI frame: `!ReferenceEquals(result, lastSeenResult)` → clear `boughtState`/`listedState` → hydrate from `plugin.CurrentSessionState`, which was populated by `RunScanAsync` reading the envelope possibly before the Mark All save landed.

Result: user clicks Mark All Bought, sees marks for one frame, then sees them all uncheck themselves once scan finishes — click is gone from disk.

`QueueAutoScan` on login has a 3-second delay; the window can remain open across the delay. A returning player loading the plugin and tapping Mark All Bought during that window will lose all marks.

**Fix:** Either gate Mark All on `!plugin.ScanInProgress` (simplest), or have `RunScanAsync` skip re-hydrating `CurrentSessionState` when state has been mutated since the scan started (complex). Sample:

```csharp
if (plugin.ScanInProgress) ImGui.BeginDisabled();
if (ImGui.Button("Mark All Bought")) { /* ... */ }
ImGui.SameLine();
if (ImGui.Button("Mark All Listed")) { /* ... */ }
if (plugin.ScanInProgress) ImGui.EndDisabled();
```

### BLOCKER-04: `phase05_nyquist.sh` BeginDisabled assertion is dead

**File:** `tests/phase05_nyquist.sh:153`

```bash
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "BeginDisabled\(\);[[:space:]]*\n[[:space:]]*if \(ImGui\.Button\(\"Mark All" \
  "Mark All buttons not wrapped in BeginDisabled (D-13)"
```

`grep -Eq` operates line-by-line; the `\n` in the pattern matches a literal `n` (or nothing). The assertion always passes regardless of whether Mark All buttons are accidentally wrapped in BeginDisabled. False confidence on a D-13 regression.

**Fix:** Use awk to track BeginDisabled..EndDisabled state across lines:

```bash
awk '
  /ImGui\.BeginDisabled/ { in_disabled = 1 }
  /ImGui\.EndDisabled/ { in_disabled = 0 }
  /Mark All (Bought|Listed)/ && in_disabled { found = 1 }
  END { exit found ? 1 : 0 }
' NamazuFlippers/UI/DailyRouteWindow.cs && pass "..." || fail "..."
```

---

## WARNING Findings

### WARNING-01: `OperationCanceledException` on disposed CTS escapes the save handler

**File:** `NamazuFlippers/NamazuFlippers.cs:77-88`, `NamazuFlippers/Data/ScanCacheStore.cs:92`

On `Dispose`, `scanCts.Cancel()` then `scanCts.Dispose()` runs. Any in-flight `Task.Run` lambda from `QueueSessionSave` may access `scanCts.Token` after `Dispose`, throwing `ObjectDisposedException`. The catch only handles `OperationCanceledException`. Surfaces as an unobserved task exception in `/xllog`.

**Fix:** Either don't `Dispose` the CTS (`Cancel()` alone is sufficient), or wrap `scanCts.Token` access defensively in `QueueSessionSave`.

### WARNING-02: `ScanCacheStore` owns a `SemaphoreSlim` but doesn't implement `IDisposable`

**File:** `NamazuFlippers/Data/ScanCacheStore.cs:18`

`SemaphoreSlim` is never disposed. In practice no OS handle leaks (the lazy `ManualResetEvent` is only created when `AvailableWaitHandle` is touched, which this code doesn't), but every plugin reload (`/xlreload`, used heavily in dev) leaks one. Code analyzers flag CA1063/CA2213.

**Fix:** Implement `IDisposable` on `ScanCacheStore`, dispose semaphore, dispose `cacheStore` from `NamazuFlippers.Dispose()`.

### WARNING-03: `RunScanAsync` re-reads disk for `CurrentSessionState`

**File:** `NamazuFlippers/NamazuFlippers.cs:215-220`

After `scanEngine.GetRouteAsync`, an extra `cacheStore.LoadAnyAsync` is performed to extract `SessionState`. Wasteful and creates a second observation window where on-disk file may have just been updated by a fire-and-forget `SaveSessionAsync`. Not strictly a correctness bug but unnecessary I/O.

**Fix:** Have `ScanEngine.GetRouteAsync` return the SessionState (or full envelope) so `RunScanAsync` doesn't re-read.

### WARNING-04: Hydrate path resurrects ItemIds not in the current route

**File:** `NamazuFlippers/UI/DailyRouteWindow.cs:78-83`

Hydrating copies every key from `session.Bought`/`session.Listed` into `boughtState`/`listedState` regardless of presence in `result.RouteStops`. Dicts grow unbounded across sessions if the route shifts; persisted JSON grows; counters and rendering are correct today but a future "show me everything I bought" panel would surface stale items.

**Fix:** Filter by current route's item IDs:

```csharp
var routeIds = result.RouteStops.SelectMany(s => s.Items).Select(i => i.ItemId).ToHashSet();
foreach (var kv in session.Bought)
    if (routeIds.Contains(kv.Key)) boughtState[kv.Key] = kv.Value;
foreach (var kv in session.Listed)
    if (routeIds.Contains(kv.Key)) listedState[kv.Key] = kv.Value;
```

### WARNING-05: `ConfigFingerprint` omits `MinSalesPerDay`

**File:** `NamazuFlippers/Data/ScanCacheStore.cs:123-140`

Pre-existing Phase 3 bug surfaced during this review. `IsUsable` (`ScanEngine.cs:195`) gates opportunities on `MinSalesPerDay`, but the fingerprint is built from `MinSalesPerWeek` only. Changing `MinSalesPerDay` produces different scan results from the same raw response, but the fingerprint doesn't notice, so `IsValid` returns true and the user sees the pre-change cached route.

**Fix:** Add `configuration.MinSalesPerDay` to the fingerprint input. May be deferred to a bugfix phase if avoiding scope creep.

### WARNING-06: `SaveSessionAsync` silently drops save when no envelope exists

**File:** `NamazuFlippers/Data/ScanCacheStore.cs:96-97`

If `LoadAnyAsync` returns null (no envelope yet), method silently returns. Plan accepts this (D-04/D-05 dev note: UI hidden until first scan). But no log line — a future developer debugging "why isn't my Mark All persisting?" gets no signal.

**Fix:** `log.Debug("/nflip: session save dropped — no envelope on disk yet.");`

### WARNING-07: No round-trip test for `Dictionary<int, bool>` source-gen serialization

**File:** `NamazuFlippers/API/Models/ApiJsonContext.cs:13-16`

`PropertyNamingPolicy = SnakeCaseLower` applies to property names; dictionary keys go through int→string converter so it's fine in practice. But both write and read use the same context — a broken serializer would still appear to work in isolation. Add a unit test asserting `Serialize → Deserialize` round-trips a `SessionState { Bought = { [12345] = true } }`.

### WARNING-08: Mark All buttons don't respect `GlobalScale` for explicit sizing

**File:** `NamazuFlippers/UI/DailyRouteWindow.cs:142-152`

Unlike Settings/Rescan (lines 160-184) which multiply button widths by `ImGuiHelpers.GlobalScale`, Mark All buttons use auto-sizing. At extreme scales or narrow window widths the two buttons + `SameLine` could overflow. Window minimum 320px should fit at GlobalScale=1.0; manual UAT recommended at multiple FFXIV UI scales.

### WARNING-09: `QueueSessionSave` copies dictionaries on UI thread on every click

**File:** `NamazuFlippers/NamazuFlippers.cs:71-75`

Each toggle constructs new dicts on the UI thread, schedules a fresh `Task.Run`. Queue depth unbounded under pathological click rates. Fine for normal usage but a `Channel<SessionState>(capacity: 1, droppingOldest: true)` with a single background consumer would be more robust.

### WARNING-10: `clientState.IsLoggedIn` race at plugin construction

**File:** `NamazuFlippers/NamazuFlippers.cs:126-131`

Constructor subscribes `Login` then checks `IsLoggedIn` and calls `QueueAutoScan`. If a `Login` event fires between the subscribe and the check, both fire — but `Interlocked.Exchange(ref scanInProgress, 1)` guard in `RunScanAsync` prevents double-scan. The guard is load-bearing for correctness. Add a comment near line 130 noting this.

### WARNING-11: `SaddlebagClient`/HttpClient never disposed

**File:** `NamazuFlippers/NamazuFlippers.cs:104, 136-146`

Pre-existing — `apiClient` is constructed but never disposed. Out of Phase 5 scope. Noted for visibility.

---

## INFO Findings

- **INFO-01:** `using` statements in `ScanCacheEnvelope.cs` are required (not dead). Disregard.
- **INFO-02:** `SaveSessionAsync` catch filter intentionally excludes `OperationCanceledException` — cancellation propagates to `QueueSessionSave` which handles it. Consistent.
- **INFO-03:** `DailyRouteWindow.lastSeenResult` holds a reference across frames; reassigned on each new scan, no leak.
- **INFO-04:** Mark All iterates `routeItems` (correct), not `boughtState.Keys`, ensuring untouched items get added.
- **INFO-05:** XML doc on `CurrentSessionState` is clear about lifecycle and nullability.
- **INFO-06:** `require_order` in nyquist uses `head -1 + grep -n` — assumes patterns appear at most once meaningfully. Current patterns are distinctive enough.
- **INFO-07:** `OnCommand` `Equals("scan", OrdinalIgnoreCase)` doesn't handle `/nflip Scan extra-args` — pre-existing, out of scope.

---

## Priority Recommendation

Address in this order before in-game UAT:

1. **BLOCKER-01 + BLOCKER-02** (same fix: extend `sessionSaveLock` to cover `SaveAsync`) — cache-corruption hazard is the most user-visible.
2. **BLOCKER-04** — fix the dead nyquist assertion before relying on it as a gate.
3. **BLOCKER-03** — decide between gating Mark All on `!ScanInProgress` (simplest) or accepting the documented race.
4. **WARNING-01** — disposed-CTS exception path; small fix, prevents `/xllog` noise.
5. **WARNING-04** — stale-ItemId hydrate; bounded JSON growth.
6. **WARNING-05** — `MinSalesPerDay` fingerprint gap (pre-existing, may defer to a bugfix phase).
7. Remaining warnings are polish / future-proofing.
