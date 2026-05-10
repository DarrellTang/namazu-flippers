# Phase 5: Session Persistence - Pattern Map

**Mapped:** 2026-05-09
**Files analyzed:** 7 (5 modified, 2 created)
**Analogs found:** 7 / 7

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `NamazuFlippers/Data/ScanCacheEnvelope.cs` (modified) | model (POCO envelope) | file-I/O serialized state | self (extend in place) | self-extension |
| `NamazuFlippers/Data/SessionState.cs` (created) | model (POCO) | file-I/O serialized state | `NamazuFlippers/Core/RouteStop.cs`, `NamazuFlippers/Data/ScanCacheEnvelope.cs` | exact |
| `NamazuFlippers/Data/ScanCacheStore.cs` (modified) | service (persistence) | file-I/O atomic write/read | self (extend `SaveAsync` signature) | self-extension |
| `NamazuFlippers/UI/DailyRouteWindow.cs` (modified) | UI window (ImGui) | event-driven (ImGui frame callback) + fire-and-forget I/O dispatch | self (extend `DrawItems` checkbox handlers + `DrawProgressSection` button row) | self-extension |
| `NamazuFlippers/NamazuFlippers.cs` (modified) | controller (plugin lifecycle) | constructor wiring + reference handoff | self (constructor lines 55-99) | self-extension |
| `NamazuFlippers/API/Models/ApiJsonContext.cs` (modified) | config (JSON source-gen registration) | declarative attribute list | self (existing `[JsonSerializable]` block) | self-extension |
| `tests/phase05_nyquist.sh` (created) | test (source-validation script) | shell `grep -E` over source files | `tests/phase04_nyquist.sh`, `tests/phase03_nyquist.sh` | exact |

## Pattern Assignments

### `NamazuFlippers/Data/SessionState.cs` (model POCO, file-I/O serialized state)

**Analog (POCO shape):** `NamazuFlippers/Core/RouteStop.cs` — minimal POCO with public auto-properties, default-initialized collections, file-scoped namespace.

**Analog (envelope-companion placement):** `NamazuFlippers/Data/ScanCacheEnvelope.cs` — sibling Data class that is part of the same persisted envelope.

**File-scoped namespace + sealed POCO pattern** (`Core/RouteStop.cs` lines 1-17):
```csharp
namespace NamazuFlippers.Core;

public sealed class RouteStop
{
    public string PurchaseSource { get; set; } = "";

    public string? DataCenter { get; set; }

    public bool IsVendorStop { get; set; }

    public int TravelFriction { get; set; }

    public int TotalExpectedDailyProfit { get; set; }

    public List<RankedOpportunity> Items { get; set; } = [];
}
```

**Apply to `SessionState.cs`:**
- `namespace NamazuFlippers.Data;` (sibling to `ScanCacheEnvelope`)
- `public sealed class SessionState`
- `public Dictionary<int, bool> Bought { get; set; } = new();`
- `public Dictionary<int, bool> Listed { get; set; } = new();`
- No constructor needed; default-initialize the dictionaries inline (matches the `Items = []` pattern above and `Bought = new()` pattern in the existing `boughtState = new()` field in `DailyRouteWindow.cs` line 34).
- Per CONTEXT.md D-03: NO `AutoCollapsed`, NO `LastModifiedUtc` fields.
- Per CONTEXT.md "Claude's Discretion": placement under `Data/` is consistent with `ScanCacheEnvelope`. Separate file (`SessionState.cs`) preferred over nesting inside `ScanCacheEnvelope.cs` for grep/discoverability — the per-file POCO is the established pattern (see `RankedOpportunity.cs`, `RouteStop.cs`, `ScanEngineResult.cs` each in their own file).

---

### `NamazuFlippers/Data/ScanCacheEnvelope.cs` (modified — schema bump 1→2, add `SessionState` field)

**Analog:** Self. The envelope already follows the schema-versioned pattern; Phase 5 extends it.

**Existing structure** (`ScanCacheEnvelope.cs` lines 1-21):
```csharp
using NamazuFlippers.API.Models;
using NamazuFlippers.Core;

namespace NamazuFlippers.Data;

public sealed class ScanCacheEnvelope
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string ConfigFingerprint { get; set; } = "";

    public ScanResponse RawResponse { get; set; } = new();

    public ScanEngineResult DerivedResult { get; set; } = new();
}
```

**Modifications (Phase 5):**
- Bump `CurrentSchemaVersion` from `1` to `2` (CONTEXT D-01).
- Add `public SessionState SessionState { get; set; } = new();` as the next property after `DerivedResult`.
- No `using NamazuFlippers.Data;` needed — `SessionState` lives in the same `NamazuFlippers.Data` namespace.
- The default-initialized `new()` matches the `RawResponse = new()` and `DerivedResult = new()` style above so older code paths that build envelopes without a session block still produce an empty-but-non-null `SessionState`.

---

### `NamazuFlippers/Data/ScanCacheStore.cs` (modified — extend save signature, optionally add `SaveSessionAsync`)

**Analog:** Self. The atomic write pattern already exists; Phase 5 reuses it.

**Imports** (lines 1-9):
```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NamazuFlippers.API.Models;
using NamazuFlippers.Core;

namespace NamazuFlippers.Data;
```

**Atomic temp-file-then-rename write pattern** (`SaveAsync`, lines 61-85):
```csharp
public async Task SaveAsync(ScanResponse rawResponse, ScanEngineResult result, CancellationToken ct = default)
{
    var now = DateTimeOffset.UtcNow;
    var envelope = new ScanCacheEnvelope
    {
        SchemaVersion = ScanCacheEnvelope.CurrentSchemaVersion,
        CreatedAtUtc = now,
        ExpiresAtUtc = now.AddHours(Math.Max(1, configuration.CacheDurationHours)),
        ConfigFingerprint = CreateConfigFingerprint(),
        RawResponse = rawResponse,
        DerivedResult = result,
    };

    var tempPath = cachePath + ".tmp";
    await using (var stream = File.Create(tempPath))
    {
        await JsonSerializer.SerializeAsync(
            stream,
            envelope,
            ApiJsonContext.Default.ScanCacheEnvelope,
            ct);
    }

    File.Move(tempPath, cachePath, overwrite: true);
}
```

**Load pattern with silent log on failure** (`LoadAnyAsync`, lines 41-59):
```csharp
public async Task<ScanCacheEnvelope?> LoadAnyAsync(CancellationToken ct = default)
{
    if (!File.Exists(cachePath))
        return null;

    try
    {
        await using var stream = File.OpenRead(cachePath);
        return await JsonSerializer.DeserializeAsync(
            stream,
            ApiJsonContext.Default.ScanCacheEnvelope,
            ct);
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
    {
        log.Warning("/nflip: could not load scan cache: {Message}", ex.Message);
        return null;
    }
}
```

**Apply to Phase 5 modifications:**
- Add a new `SaveSessionAsync(SessionState sessionState, CancellationToken ct = default)` method that loads the current envelope (`LoadAnyAsync`), swaps in the new `SessionState`, and rewrites the envelope using the **exact same atomic temp-file-then-rename pattern** above. CONTEXT.md "Claude's Discretion" allows either a new sibling `SessionStore` class or a method-pair on `ScanCacheStore` — the method-pair is cheaper and reuses the `cachePath`/`configuration`/`log` fields already in place.
- Wrap the `SaveSessionAsync` body in the same `try { ... } catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { log.Warning("/nflip: could not save session state: {Message}", ex.Message); }` shape (CONTEXT D-06: silent log on save failure, mirroring the `LoadAnyAsync` exception filter).
- For overlapping fire-and-forget save serialization (CONTEXT D-05): a `private readonly SemaphoreSlim sessionSaveLock = new(1, 1);` field with `await sessionSaveLock.WaitAsync(ct);` / `try { ... } finally { sessionSaveLock.Release(); }` is the simplest correct pattern. Last-write-wins is acceptable per D-05 — the lock just prevents partial-write races on the temp file.
- `IsValid` (lines 106-109) needs no change — it already gates on `SchemaVersion == ScanCacheEnvelope.CurrentSchemaVersion`, so v1 envelopes auto-discard once `CurrentSchemaVersion = 2` (CONTEXT D-02: no migration code).

---

### `NamazuFlippers/UI/DailyRouteWindow.cs` (modified — hydrate-on-load, save-on-toggle, Mark All row)

**Analog:** Self. Three insertion points; the surrounding code is the analog.

**Reference-change wipe pattern** (lines 70-76) — the hook point for hydrate-on-load (D-08):
```csharp
// Detect result-change to wipe state (D-09) — wave 2 acts on this; wave 1 just tracks last seen.
if (!ReferenceEquals(result, lastSeenResult))
{
    boughtState.Clear();
    listedState.Clear();
    autoCollapsedStops.Clear();
    lastSeenResult = result;
}
```

**Apply to Phase 5 hydrate (D-08):**
- After the three `.Clear()` calls (which still fire — Rescan returns a new envelope with empty `SessionState`, so re-hydrating from it is a no-op clean slate), add:
  ```csharp
  // Hydrate from persisted session state (Phase 5 D-08). Plugin owns the
  // current envelope; on first sight of a new result we copy its SessionState
  // dictionaries into our in-memory view. Rescan envelopes have empty
  // SessionState by construction, so this also implements the wipe path.
  var session = plugin.CurrentSessionState;
  if (session != null)
  {
      foreach (var kv in session.Bought) boughtState[kv.Key] = kv.Value;
      foreach (var kv in session.Listed) listedState[kv.Key] = kv.Value;
  }
  ```
- This requires a new `public SessionState? CurrentSessionState` property on `NamazuFlippers` (see plugin entry point section below) so the window can reach into the loaded envelope without taking a hard reference to `ScanCacheStore`.

**Bought checkbox handler** (lines 227-229) — the save-on-toggle hook point (D-04):
```csharp
var bought = boughtState.GetValueOrDefault(item.ItemId);
if (ImGui.Checkbox($"##bought-{item.ItemId}", ref bought))
    boughtState[item.ItemId] = bought;
```

**Apply to Phase 5 save-on-toggle (D-04, D-05):**
```csharp
var bought = boughtState.GetValueOrDefault(item.ItemId);
if (ImGui.Checkbox($"##bought-{item.ItemId}", ref bought))
{
    boughtState[item.ItemId] = bought;
    plugin.QueueSessionSave(boughtState, listedState);
}
```
Mirror the same change on the listed checkbox (lines 296-298):
```csharp
var listed = listedState.GetValueOrDefault(item.ItemId);
if (ImGui.Checkbox($"##listed-{item.ItemId}", ref listed))
{
    listedState[item.ItemId] = listed;
    plugin.QueueSessionSave(boughtState, listedState);
}
```

**Fire-and-forget dispatch precedent** (`NamazuFlippers.OnCommand` line 118):
```csharp
_ = RunScanAsync(forceRefresh: true, scanCts.Token);
```

**Existing plugin-owned button precedent** (`DrawProgressSection` lines 154-155):
```csharp
if (ImGui.Button("Rescan Route", new Vector2(rescanWidth, 0)))
    _ = plugin.RescanAsync(CancellationToken.None);
```

**Apply to `QueueSessionSave` on the plugin** (D-05): `public void QueueSessionSave(Dictionary<int,bool> bought, Dictionary<int,bool> listed) => _ = Task.Run(() => cacheStore.SaveSessionAsync(new SessionState { Bought = new(bought), Listed = new(listed) }, scanCts.Token));`. The `new(...)` copy snapshots the dictionaries off the UI thread so the background save sees a stable view.

**Mark All button row insertion point** (D-11) — between line 125 (the bought/listed Text) and line 126 (the GAP-E1 button-row arithmetic block):

**Existing button rendering pattern** (lines 148-157) — reuse this pixel-budget + GlobalScale style:
```csharp
if (ImGui.Button("Settings", new Vector2(settingsWidth, 0)))
    plugin.OpenConfigWindow();

ImGui.SameLine();
if (plugin.ScanInProgress)
    ImGui.BeginDisabled();
if (ImGui.Button("Rescan Route", new Vector2(rescanWidth, 0)))
    _ = plugin.RescanAsync(CancellationToken.None);
if (plugin.ScanInProgress)
    ImGui.EndDisabled();
```

**Iteration pattern for "every item in the route"** (line 116):
```csharp
var routeItems = result?.RouteStops.SelectMany(stop => stop.Items).ToList() ?? [];
```

**Apply to Mark All row (D-10, D-11, D-13):**
- Insert a new row immediately AFTER `ImGui.Text($"Bought: {boughtCount}/{totalItems}   Listed: {listedCount}/{totalItems}");` (line 125) and BEFORE the existing GAP-E1 button arithmetic block (line 133+).
- Render two buttons in left-to-right order (Bought first, Listed second — matches the counter order, per CONTEXT specifics):
  ```csharp
  if (ImGui.Button("Mark All Bought"))
  {
      foreach (var item in routeItems) boughtState[item.ItemId] = true;
      plugin.QueueSessionSave(boughtState, listedState);
  }
  ImGui.SameLine();
  if (ImGui.Button("Mark All Listed"))
  {
      foreach (var item in routeItems) listedState[item.ItemId] = true;
      plugin.QueueSessionSave(boughtState, listedState);
  }
  ```
- Both buttons always enabled (D-13: no gating).
- No confirmation modal (D-12: each checkbox is reversible).
- Per CONTEXT.md "Claude's Discretion" the exact button widths are flexible, but the established pattern is `new Vector2(width * ImGuiHelpers.GlobalScale, 0)` from lines 133-134 if you want pixel-budgeted widths. For a left-aligned row that does not fight the right-edge budget, default-width buttons (`ImGui.Button("...")` with no `Vector2` arg) are also acceptable and avoid re-introducing the GAP-E1 right-edge math.

---

### `NamazuFlippers/NamazuFlippers.cs` (modified — expose `CurrentSessionState` + `QueueSessionSave`)

**Analog:** Self. The constructor already wires `cacheStore`; Phase 5 just exposes two new members.

**Existing constructor wiring** (lines 71-76):
```csharp
var cacheStore = new ScanCacheStore(pluginInterface, Configuration, log);
scanEngine = new ScanEngine(apiClient, Configuration, log, routeOptimizer, cacheStore);

firstRunWindow = new FirstRunWindow(Configuration, pluginInterface, log);
dailyRouteWindow = new DailyRouteWindow(this, log);
configWindow = new ConfigWindow(this, pluginInterface, log);
```

**Existing public helper precedent** (lines 49-53):
```csharp
/// <summary>Public wrapper around RunScanAsync(forceRefresh: true). Called by DailyRouteWindow's Rescan button (wired in 04-02).</summary>
public Task RescanAsync(CancellationToken ct) => RunScanAsync(true, ct);

/// <summary>Opens the ConfigWindow. Called by DailyRouteWindow's in-window Settings button (D-07).</summary>
public void OpenConfigWindow() => configWindow.IsOpen = true;
```

**Existing scan-result handoff** (`RunScanAsync` lines 176-178):
```csharp
var result = await scanEngine.GetRouteAsync(forceRefresh, ct);
LatestScanResult = result;
LastApiError = result.Status == ScanEngineStatus.Error ? result.UserMessage : null;
```

**Apply to Phase 5 plugin-side wiring:**
- Promote the local `cacheStore` (line 71) to a `private readonly ScanCacheStore cacheStore;` field so `QueueSessionSave` and the session-load path can both reach it.
- Add `public SessionState? CurrentSessionState { get; private set; }` (mirrors the `LatestScanResult` property style on line 42).
- Inside `RunScanAsync` (after line 177's `LatestScanResult = result;`), populate `CurrentSessionState` from the envelope returned by the engine. This requires `ScanEngine.GetRouteAsync` to surface the envelope's `SessionState` alongside the result — simplest path: `ScanCacheStore.LoadValidAsync` already returns the full envelope, so `ScanEngine` can stash the loaded envelope's `SessionState` on the result, OR the plugin can call `cacheStore.LoadAnyAsync(ct)` after `RunScanAsync` to read the just-written envelope back. Pick the path that touches the fewest files; the latter is simpler and stays inside `NamazuFlippers.cs`.
- Add `public void QueueSessionSave(Dictionary<int, bool> bought, Dictionary<int, bool> listed) => _ = Task.Run(() => cacheStore.SaveSessionAsync(new SessionState { Bought = new(bought), Listed = new(listed) }, scanCts.Token));`. The `_ =` discard, the `Task.Run`, and the `scanCts.Token` all match the existing `OnCommand` line 118 (`_ = RunScanAsync(...)`) and `QueueAutoScan` lines 146-157 (`_ = Task.Run(...)`) precedents.

---

### `NamazuFlippers/API/Models/ApiJsonContext.cs` (modified — register `SessionState` and `Dictionary<int, bool>`)

**Analog:** Self. The existing attribute block is the registration pattern.

**Existing registration block** (lines 13-32):
```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScanRequest))]
[JsonSerializable(typeof(ScanResponse))]
[JsonSerializable(typeof(ScanItem))]
[JsonSerializable(typeof(List<ScanItem>))]
[JsonSerializable(typeof(RawScanResponse))]
[JsonSerializable(typeof(RawScanItem))]
[JsonSerializable(typeof(List<RawScanItem>))]
[JsonSerializable(typeof(RankedOpportunity))]
[JsonSerializable(typeof(RouteStop))]
[JsonSerializable(typeof(ScanEngineResult))]
[JsonSerializable(typeof(ScanCacheEnvelope))]
[JsonSerializable(typeof(List<RankedOpportunity>))]
[JsonSerializable(typeof(List<RouteStop>))]
internal sealed partial class ApiJsonContext : JsonSerializerContext
{
}
```

**Apply to Phase 5:**
- Add `[JsonSerializable(typeof(SessionState))]` (the new POCO).
- Add `[JsonSerializable(typeof(Dictionary<int, bool>))]` (so the source generator emits a converter for the inner dictionaries).
- The existing `using NamazuFlippers.Data;` (line 3) already covers `SessionState`'s namespace — no new `using` needed.
- The existing `SnakeCaseLower` naming policy harmlessly applies; `Bought` becomes `bought` and `Listed` becomes `listed` in the JSON, which matches `SPEC.md`'s reference shape (`items[].bought`, `items[].listed`) at the field-name level.
- Per CONTEXT specifics: `Dictionary<int, bool>` serializes as `{"12345": true}` with string-stringified int keys under STJ source-gen. That is the canonical wire format for the `ItemId` keys.

---

### `tests/phase05_nyquist.sh` (created)

**Analog:** `tests/phase04_nyquist.sh` (363 lines) and `tests/phase03_nyquist.sh` (250 lines). Phase 04 is the most recent and structurally complete precedent.

**Bash header + helpers preamble** (`phase04_nyquist.sh` lines 1-87) — copy verbatim:
```bash
#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

failures=0

pass() {
  printf 'ok - %s\n' "$1"
}

fail() {
  printf 'not ok - %s\n' "$1" >&2
  failures=$((failures + 1))
}

require_file() { ... }
require_pattern() { ... }
require_absent_pattern() { ... }
require_order() { ... }
require_all_patterns() { ... }
```
(Copy lines 1-87 of `tests/phase04_nyquist.sh` byte-for-byte; these helpers are stable across phases.)

**Per-area assertion block pattern** (`phase04_nyquist.sh` lines 89-118):
```bash
echo "Phase 04 Nyquist validation"

# === File existence ===
require_file "NamazuFlippers/UI/DailyRouteWindow.cs"
require_file "NamazuFlippers/UI/ConfigWindow.cs"
...

echo
echo "UI-01: WindowSystem wiring and DailyRouteWindow scaffolding"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "WindowSystem registered and all windows added" \
  "WindowSystem" \
  "AddWindow" \
  "windowSystem\.Draw"
```

**Final pass/fail summary footer** (`phase04_nyquist.sh` lines 357-362):
```bash
if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 04 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 04 Nyquist validation passed.\n'
```

**Apply to Phase 5 — assertion areas (per CONTEXT.md "Claude's Discretion"):**
- File existence: `NamazuFlippers/Data/SessionState.cs`, `NamazuFlippers/Data/ScanCacheEnvelope.cs`, `NamazuFlippers/Data/ScanCacheStore.cs`, `NamazuFlippers/UI/DailyRouteWindow.cs`, `NamazuFlippers/NamazuFlippers.cs`, `NamazuFlippers/API/Models/ApiJsonContext.cs`.
- **SESS-01 (envelope schema bump + SessionState type):**
  - `require_pattern "NamazuFlippers/Data/ScanCacheEnvelope.cs" "CurrentSchemaVersion = 2" "schema bumped to 2 (D-01)"`
  - `require_pattern "NamazuFlippers/Data/ScanCacheEnvelope.cs" "SessionState SessionState" "envelope holds SessionState field (D-01)"`
  - `require_pattern "NamazuFlippers/Data/SessionState.cs" "Dictionary<int, bool> Bought" "SessionState.Bought present (D-03)"`
  - `require_pattern "NamazuFlippers/Data/SessionState.cs" "Dictionary<int, bool> Listed" "SessionState.Listed present (D-03)"`
  - `require_absent_pattern "NamazuFlippers/Data/SessionState.cs" "AutoCollapsed|LastModifiedUtc" "SessionState has no AutoCollapsed/LastModifiedUtc (D-03)"`
- **SESS-01 (JSON registration):**
  - `require_pattern "NamazuFlippers/API/Models/ApiJsonContext.cs" "JsonSerializable\(typeof\(SessionState\)\)" "SessionState registered in source-gen context"`
  - `require_pattern "NamazuFlippers/API/Models/ApiJsonContext.cs" "JsonSerializable\(typeof\(Dictionary<int, ?bool>\)\)" "Dictionary<int,bool> registered in source-gen context"`
- **SESS-01 (persistence wiring):**
  - `require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "SaveSessionAsync" "ScanCacheStore exposes SaveSessionAsync"`
  - `require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "cachePath \+ \"\.tmp\"" "atomic temp-file pattern reused (D-04)"` (the existing line, reasserted to lock the pattern)
  - `require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "log\.Warning.*could not save session state" "silent log on save failure (D-06)"`
- **SESS-02 (hydrate-on-load + save-on-toggle):**
  - `require_pattern "NamazuFlippers/NamazuFlippers.cs" "public SessionState\?? CurrentSessionState" "plugin exposes CurrentSessionState"`
  - `require_pattern "NamazuFlippers/NamazuFlippers.cs" "QueueSessionSave" "plugin exposes QueueSessionSave (D-05 fire-and-forget)"`
  - `require_pattern "NamazuFlippers/NamazuFlippers.cs" "_ = Task\.Run.*SaveSessionAsync" "save dispatched as fire-and-forget Task.Run (D-05)"`
  - `require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "plugin\.QueueSessionSave" "DailyRouteWindow calls QueueSessionSave on toggle (D-04)"`
  - `require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "plugin\.CurrentSessionState" "DailyRouteWindow hydrates from CurrentSessionState (D-08)"`
- **SESS-03 (Mark All buttons):**
  - `require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "Mark All Bought" "Mark All Bought button label (D-10)"`
  - `require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "Mark All Listed" "Mark All Listed button label (D-10)"`
  - `require_order "NamazuFlippers/UI/DailyRouteWindow.cs" "Mark All Bought" "Mark All Listed" "Bought button rendered before Listed button (CONTEXT specifics)"`
  - `require_order "NamazuFlippers/UI/DailyRouteWindow.cs" "ImGui\.Text\(\\\$\"Bought:" "Mark All Bought" "Mark All row sits AFTER bought/listed counter Text (D-11)"`
  - `require_order "NamazuFlippers/UI/DailyRouteWindow.cs" "Mark All Bought" "ImGui\.ProgressBar" "Mark All row sits BEFORE the progress bars (D-11)"`
  - `require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "RouteStops\.SelectMany\(stop => stop\.Items\)" "Mark All iterates the same routeItems source as the counters (CONTEXT specifics)"` (already present from Phase 4 GAP-F2 — reasserts the shared iterator)
- **Footer:** swap "Phase 04" → "Phase 05" in the two `printf` calls.

## Shared Patterns

### Atomic File Persistence
**Source:** `NamazuFlippers/Data/ScanCacheStore.cs` lines 74-84
**Apply to:** Any new method that writes the envelope (`SaveSessionAsync`)
```csharp
var tempPath = cachePath + ".tmp";
await using (var stream = File.Create(tempPath))
{
    await JsonSerializer.SerializeAsync(
        stream,
        envelope,
        ApiJsonContext.Default.ScanCacheEnvelope,
        ct);
}
File.Move(tempPath, cachePath, overwrite: true);
```

### Silent-Log on I/O Failure
**Source:** `NamazuFlippers/Data/ScanCacheStore.cs` lines 54-58 (load path)
**Apply to:** `SaveSessionAsync` (CONTEXT D-06)
```csharp
catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
{
    log.Warning("/nflip: could not save session state: {Message}", ex.Message);
}
```
Reuse the same exception filter (`IOException or JsonException or UnauthorizedAccessException`); these three together cover disk-full, AV-locked-file, and permission-denied scenarios that triggered the original guard in `LoadAnyAsync`.

### Fire-and-Forget Task Dispatch
**Source:** `NamazuFlippers/NamazuFlippers.cs` line 118 (`OnCommand` scan trigger), lines 146-157 (`QueueAutoScan`)
**Apply to:** `QueueSessionSave` on the plugin (CONTEXT D-05)
```csharp
_ = Task.Run(async () =>
{
    try
    {
        await someAsync(ct);
    }
    catch (OperationCanceledException) { }
}, scanCts.Token);
```
The `_ =` discard, `Task.Run`, and `scanCts.Token` make the save survive a clean plugin teardown (the CTS cancels in `Dispose` line 104).

### Schema-Versioned Discard-Over-Migrate
**Source:** `NamazuFlippers/Data/ScanCacheStore.cs` lines 106-109 (`IsValid`)
**Apply to:** Phase 5's v1→v2 jump (CONTEXT D-02)
```csharp
public static bool IsValid(ScanCacheEnvelope envelope, string expectedFingerprint, DateTimeOffset nowUtc) =>
    envelope.SchemaVersion == ScanCacheEnvelope.CurrentSchemaVersion &&
    envelope.ExpiresAtUtc > nowUtc &&
    envelope.ConfigFingerprint == expectedFingerprint;
```
No code change required. Bumping `CurrentSchemaVersion` to `2` causes any persisted v1 envelope to fail the first conjunct here, so `LoadValidAsync` returns `null` and the next scan starts clean — zero migration code.

### POCO with File-Scoped Namespace + Default-Initialized Collections
**Source:** `NamazuFlippers/Core/RouteStop.cs`, `NamazuFlippers/Core/RankedOpportunity.cs`, `NamazuFlippers/Data/ScanCacheEnvelope.cs`
**Apply to:** `SessionState.cs` (and any future POCO)
- `namespace X;` (file-scoped, single semicolon)
- `public sealed class Foo`
- `public T Prop { get; set; } = "";` for strings, `= new()` for collections, `= []` for `List<>`.
- No constructor, no methods, no validation logic. The class is a transport bag.

### ImGui Button Row with GlobalScale-Aware Pixel Budget
**Source:** `NamazuFlippers/UI/DailyRouteWindow.cs` lines 133-157 (Settings + Rescan row)
**Apply to:** Mark All row only IF the chosen button widths need to fit a right-edge budget. CONTEXT.md D-11 explicitly places Mark All on its **own** row above the progress bars to avoid the GAP-E1 right-edge fight, so default-width left-aligned buttons are acceptable. If pixel widths are still desired, copy the `* ImGuiHelpers.GlobalScale` multiplier — never hardcode raw pixels (this regressed in 04-07 / 04-08).

### Source-Validation Test Helpers
**Source:** `tests/phase04_nyquist.sh` lines 1-87 (header + 5 helper functions)
**Apply to:** `tests/phase05_nyquist.sh` — copy the entire preamble unchanged. The helpers (`require_file`, `require_pattern`, `require_absent_pattern`, `require_order`, `require_all_patterns`) cover every assertion shape Phase 5 needs. No new helper functions required.

## No Analog Found

None. Every Phase 5 file has either an exact match (POCO, source-validation script) or extends an existing file in place (envelope, store, window, plugin entry, JSON context). The phase is squarely inside the established pattern envelope, which is consistent with the CONTEXT.md framing ("stays inside this pattern by extending the same envelope").

## Metadata

**Analog search scope:**
- `NamazuFlippers/Data/` (POCO + persistence analogs)
- `NamazuFlippers/Core/` (POCO shape analogs)
- `NamazuFlippers/UI/DailyRouteWindow.cs` (in-place modification analogs)
- `NamazuFlippers/NamazuFlippers.cs` (plugin lifecycle analogs)
- `NamazuFlippers/API/Models/ApiJsonContext.cs` (source-gen registration)
- `tests/phase03_nyquist.sh`, `tests/phase04_nyquist.sh` (shell test analogs)

**Files scanned:** 11
**Pattern extraction date:** 2026-05-09
