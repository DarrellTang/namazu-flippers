---
phase: 01-plugin-shell
plan: 01-01
subsystem: plugin-core
tags: [dalamud, csharp, imGui, plugin-shell]

requires: []
provides:
  - Compileable Dalamud plugin solution with manifest and entry point
  - /nflip chat command registration and cleanup
  - First-run home world ImGui popup with config persistence
affects:
  - 01-02 (Configuration system — wires into this entry point)
  - 02 (API integration — built on this scaffold)
  - 04 (Core UI — DailyRouteWindow, ConfigWindow)

tech-stack:
  added:
    - .NET 8.0 (net8.0-windows)
    - Dalamud SDK (API level 10)
    - ImGui.NET (via Dalamud)
  patterns:
    - Standard Dalamud IDalamudPlugin lifecycle (constructor/dispose)
    - DalamudPluginInterface for config persistence (GetPluginConfig/SavePluginConfig)
    - CommandManager for chat command registration
    - UiBuilder.Draw for ImGui rendering hook

key-files:
  created:
    - NamazuFlippers/NamazuFlippers.slnx
    - NamazuFlippers/NamazuFlippers.csproj
    - NamazuFlippers/NamazuFlippers.json
    - NamazuFlippers/NamazuFlippers.cs
  modified: []

key-decisions:
  - "Used .slnx (new XML solution format from .NET 10) instead of traditional .sln — equivalent functionality, modern format"
  - "Set DalamudApiLevel to 10 in manifest (latest stable for current XIV Launcher)"
  - "Placeholder Configuration stub with only HomeWorld — full config class in 01-02"

requirements-completed:
  - PLUG-01
  - PLUG-02
  - PLUG-03

duration: 13min
completed: 2026-05-05
---

# Phase 1 Plan 01-01: Scaffold Dalamud Plugin Project

**Dalamud plugin solution with manifest, entry point, /nflip command, and first-run home world ImGui popup**

## Performance

- **Duration:** 13 min
- **Started:** 2026-05-05T05:05:05Z
- **Completed:** 2026-05-05T05:18:16Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments
- .NET solution and project file targeting net8.0-windows with Dalamud SDK references and DalamudPackager build target
- Plugin manifest (`NamazuFlippers.json`) with `/nflip` command, API level 10, and full metadata
- Entry point implementing `IDalamudPlugin` with constructor/dispose lifecycle and command registration
- First-run home world ImGui popup: auto-appears on first `/nflip`, InputText + Confirm, saves via `SavePluginConfig`

## Task Commits

Each task was committed atomically:

1. **Task 1: Create DALAMUD project scaffold** — `ea18dad` (feat)
2. **Task 2: Create plugin manifest and entry point** — `e52cd9f` (feat)
3. **Task 3: Add first-run home world ImGui popup** — `918ad97` (feat)

## Files Created/Modified
- `NamazuFlippers/NamazuFlippers.slnx` — Solution file, references NamazuFlippers.csproj
- `NamazuFlippers/NamazuFlippers.csproj` — Project targeting net8.0-windows, Dalamud references, Packager target
- `NamazuFlippers/NamazuFlippers.json` — Plugin manifest for XIV Launcher (/nflip command, metadata, tags)
- `NamazuFlippers/NamazuFlippers.cs` — Entry point implementing IDalamudPlugin with first-run popup and Configuration stub

## Decisions Made
- **.slnx format:** .NET 10 generates `.slnx` (XML-based solution) by default instead of `.sln`. Functionally equivalent.
- **API level 10:** Selected as stable Dalamud API level. No `[DalamudPlugin]` attribute needed — `IDalamudPlugin` interface suffices.
- **Build verification constraint:** `dotnet build` fails on macOS because Dalamud assemblies are Windows-only and require XIVLauncher dev environment. C# syntax is verified correct (zero syntax errors; all 13 errors are missing assembly references). Full build verification requires a Windows machine with XIVLauncher installed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] .NET SDK not installed on macOS**
- **Found during:** Task 1 (dotnet new sln)
- **Issue:** `dotnet` CLI not available on arm64 macOS
- **Fix:** User installed .NET SDK 10.0 via Homebrew (`brew install dotnet-sdk`)
- **Files modified:** None (tooling only)
- **Verification:** `dotnet --version` returns 10.0.203

**2. [Rule 1 - Bug] .slnx format instead of .sln**
- **Found during:** Task 1 (solution creation)
- **Issue:** .NET 10 creates `.slnx` (XML-based) by default, not traditional `.sln`. Plan assumes `.sln`.
- **Fix:** Used `.slnx` format — functionally equivalent, modern standard. `dotnet sln add` works identically.
- **Files modified:** NamazuFlippers/NamazuFlippers.slnx (created as .slnx instead of .sln)
- **Verification:** `dotnet sln list` shows project referenced correctly

**3. [Rule 2 - Missing Critical] Added UiBuilder.Draw wiring for first-run popup**
- **Found during:** Task 3 (first-run popup implementation)
- **Issue:** Plan describes ImGui popup rendering but doesn't specify the Dalamud draw hook. Without `PluginInterface.UiBuilder.Draw += OnDraw`, the popup would never render.
- **Fix:** Added `OnDraw()` callback registered in constructor via `UiBuilder.Draw += OnDraw`, unregistered in `Dispose()`. Added `using Dalamud.Logging` for `PluginLog`.
- **Files modified:** NamazuFlippers/NamazuFlippers.cs
- **Verification:** Control flow reviewed — draw callback fires each frame, popup renders when `isVisible && isFirstRun && string.IsNullOrEmpty(HomeWorld)`

---

**Total deviations:** 3 auto-fixed (1 blocking, 1 bug, 1 missing critical)
**Impact on plan:** All auto-fixes necessary for functionality. No scope creep. Build verification can't pass on macOS — expected for Dalamud plugin development.

## Issues Encountered
- **Build verification blocked on macOS:** Dalamud is Windows-only. The 4 `.dll` references (Dalamud, ImGui.NET, Lumina, Lumina.Excel) and `DalamudPackager.dll` are only available on a machine with XIVLauncher installed. C# syntax is verified clean — all 13 build errors are missing assembly references, not code errors. Full `dotnet build` verification requires a Windows machine with the XIVLauncher dev environment.

## Next Phase Readiness
- Plugin scaffold is structurally complete — entry point, manifest, command, and first-run popup are in place
- Placeholder `Configuration` stub ready to be replaced by full class in Plan 01-02
- No blocking issues for 01-02 (Configuration replaces the stub without conflict)

---
*Phase: 01-plugin-shell*
*Completed: 2026-05-05*
