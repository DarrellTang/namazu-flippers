# Phase 03 - Pattern Map

**Phase:** 03 - Scan Engine & Route Optimizer
**Generated:** 2026-05-07

## Existing Patterns To Reuse

| New/Changed File | Role | Closest Existing Analog | Pattern To Preserve |
|------------------|------|-------------------------|---------------------|
| `NamazuFlippers/Core/ScanEngine.cs` | Business orchestration around API calls, ranking, cache-aware result state | `NamazuFlippers/API/SaddlebagClient.cs` | Constructor-injected dependencies, async `CancellationToken`, structured exception handling, `/nflip:` log prefix |
| `NamazuFlippers/Core/RankedOpportunity.cs` | Domain DTO copied from `ScanItem` | `NamazuFlippers/API/Models/ScanItem.cs` | Sealed model class with simple get/set properties and non-null default strings |
| `NamazuFlippers/Core/ScanEngineResult.cs` | Structured success/empty/error output | `NamazuFlippers/API/ApiException.cs` plus `LastApiError` in `NamazuFlippers.cs` | Explicit status/error fields instead of null or raw exception propagation |
| `NamazuFlippers/Core/RouteOptimizer.cs` | Deterministic grouping and ordering | `NamazuFlippers/API/RateLimiter.cs` | Small sealed service with pure-ish methods and simple constructor state |
| `NamazuFlippers/Core/RouteStop.cs` | Derived route stop DTO | `ScanItem.cs` | Plain model, string defaults, derived totals held explicitly |
| `NamazuFlippers/Data/WorldData.cs` | Central hardcoded world/DC lookup | `FirstRunWindow.cs` `KnownWorlds` | Static in-memory list, alphabetical world coverage, no runtime network dependency |
| `NamazuFlippers/Data/ScanCacheStore.cs` | Plugin-local JSON cache persistence | `FirstRunWindow.cs` config persistence call site and `ApiJsonContext.cs` source-gen pattern | Use `IDalamudPluginInterface` paths, `System.Text.Json`, cancellation-aware IO where possible |
| `NamazuFlippers/NamazuFlippers.cs` | Runtime wiring for command/login/manual scan | Current `OnCommand`, constructor, and `Dispose` | Keep entry point thin; parse command, delegate to services, unsubscribe/dispose cleanly |
| `NamazuFlippers/API/Models/ApiJsonContext.cs` | Serializer registrations | Existing source-gen context | Add cache/route/domain serializable types explicitly; avoid reflection-heavy JSON at runtime |

## Concrete Code Excerpts

### Constructor injection and logging pattern

Source: `NamazuFlippers/API/SaddlebagClient.cs`

```csharp
public SaddlebagClient(Configuration config, IPluginLog log, RateLimiter? rateLimiter = null)
{
    _config = config ?? throw new ArgumentNullException(nameof(config));
    _log = log ?? throw new ArgumentNullException(nameof(log));
    _rateLimiter = rateLimiter;
}
```

Apply to `ScanEngine`, `RouteOptimizer`, and `ScanCacheStore`: pass required dependencies explicitly, keep null checks, and keep `/nflip:` log prefix for user-visible operational logs.

### Result/error surface pattern

Source: `NamazuFlippers/NamazuFlippers.cs`

```csharp
public string? LastApiError { get; private set; }
```

Apply to Phase 3 by expanding from API-only error to scan result state while keeping Phase 4 consumption simple. Do not expose raw exceptions or null as the normal UI contract.

### Static world list pattern

Source: `NamazuFlippers/FirstRunWindow.cs`

```csharp
private static readonly string[] KnownWorlds =
[
    "Adamantoise", "Aegis", "Alexander", "Alpha", "Anima", "Asura", "Atomos",
    ...
];
```

Apply by moving or duplicating this list into `WorldData.KnownWorlds` plus a world-to-DC map. Prefer centralizing and updating `FirstRunWindow` to read `WorldData.KnownWorlds`.

### JSON source generation pattern

Source: `NamazuFlippers/API/Models/ApiJsonContext.cs`

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScanRequest))]
[JsonSerializable(typeof(ScanResponse))]
[JsonSerializable(typeof(ScanItem))]
internal sealed partial class ApiJsonContext : JsonSerializerContext
{
}
```

Apply by registering any cache envelope/route models used with `System.Text.Json`.

## Data Flow

1. `NamazuFlippers.cs` receives `/nflip scan` or `IClientState.Login`.
2. `ScanEngine` checks duplicate-scan state and cache policy.
3. `ScanCacheStore` loads a valid cache when allowed.
4. `SaddlebagClient.ScanAsync` fetches raw scan data when required.
5. `ScanEngine` filters and ranks `ScanItem` rows into `RankedOpportunity`.
6. `RouteOptimizer` groups opportunities into `RouteStop` objects and orders stops.
7. `ScanCacheStore` writes raw response plus derived route on successful fresh scan.
8. `NamazuFlippers.cs` stores latest result and logs concise status for Phase 3 testing.

## Planning Implications

- `03-01` should not touch login events or cache yet unless needed for compile safety.
- `03-02` should own integration in `NamazuFlippers.cs`, because route/cache behavior needs both scan and optimizer outputs.
- Avoid adding UI windows in Phase 3. Logs and latest in-memory state are enough for Phase 4 to consume.
