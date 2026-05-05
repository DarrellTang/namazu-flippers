# Phase 1: Plugin Shell & Configuration - Context

**Gathered:** 2026-05-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver a valid Dalamud plugin that loads in XIV Launcher, responds to the `/nflip` chat command, and persists configuration across game sessions. This is the scaffold — no API calls, no scan engine, no route UI. Just the plugin skeleton, its lifecycle, and the configuration data model with storage.

</domain>

<decisions>
## Implementation Decisions

### Plugin identity
- **D-01:** Display name: "Namazu Flippers"
- **D-02:** Internal name / namespace / .csproj / .sln: `NamazuFlippers`
- **D-03:** Chat command: `/nflip`
- **D-04:** GitHub repo: `DarrellTang/namazu-flippers`

### Configuration storage
- **D-05:** Use Dalamud's built-in config serialization (`DalamudPluginInterface.SavePluginConfig<T>()`), not custom JSON
- **D-06:** Config model includes all CONF-01 through CONF-09 properties from REQUIRMENTS.md, with sensible defaults

### Project structure
- **D-07:** Minimal scaffold — create only the files/folders Phase 1 needs (entry point, config model, manifest)
- **D-08:** Subsequent phases add Core/, API/, Data/, UI/, Integration/ folders as needed

### First-run experience
- **D-09:** Simple ImGui popup window prompts for home world on first run (when home world is unset)
- **D-10:** Popup auto-appears on first `/nflip`; dismisses once a valid world is saved

### the agent's Discretion
- Exact ImGui popup layout (input field placement, confirm button styling)
- Config model class design — property types, default values, validation approach
- Plugin entry point boilerplate structure (class layout, field ordering)
- .csproj and manifest.json template details
- Namespace organization within the minimal scaffold
</decisions>

<specifics>
## Specific Ideas

- Plugin naming follows the humorous FFXIV fish-merchant theme (Namazu beast tribe + "flipping" items)
- Command `/nflip` is short and types fast — important for repeated in-game use
- The first-run popup should feel lightweight, not a full config window — just enough to unblock the player

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements
- `.planning/REQUIREMENTS.md` — PLUG-01 through PLUG-03 (plugin shell requirements), CONF-01 through CONF-09 (configuration requirements)
- `.planning/REQUIREMENTS.md` §Traceability — maps all 12 Phase 1 requirements to their status

### Architecture & conventions
- `SPEC.md` — Full plugin architecture overview, suggested folder layout (Core/, API/, Data/, UI/, Integration/), config settings table
- `SPEC.md` §Plugin Architecture — Entry point, manifest, lifecycle expectations
- `SPEC.md` §ConfigWindow Settings — Default values for all CONF-01 through CONF-09 properties

### Project context
- `.planning/PROJECT.md` — Technical constraints (Dalamud API, .NET 8+, ImGui, single-player), key decisions (JSON session state separate from config)
- `.planning/ROADMAP.md` §Phase 1 — Success criteria (5 items), plan descriptions (01-01 scaffold, 01-02 config)
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — greenfield project, no existing codebase

### Established Patterns
- Dalamud plugin conventions: `DalamudPluginInterface`, `IDalamudPlugin`, `PluginUI` / `WindowSystem` for ImGui windows
- Standard Dalamud config pattern: a `Configuration` class implementing `IPluginConfig`, serialized via `SavePluginConfig<T>()`

### Integration Points
- Plugin manifest (`NamazuFlippers.json`) loaded by XIV Launcher on startup
- Chat command registered via `CommandManager.AddHandler()` in plugin constructor
- Config window will be built on top of this config model in Phase 4

</code_context>

<deferred>
## Deferred Ideas

- Full ConfigWindow with all settings controls — Phase 4 (plan 04-03)
- HTTP client and API integration — Phase 2
- Scan engine and route optimizer — Phase 3
- DailyRouteWindow UI — Phase 4
- Session persistence (JSON for bought/listed state) — Phase 5
- Shortage predictor supplement — Phase 6
- Market board and server travel hooks — Phase 7

</deferred>

---

*Phase: 01-plugin-shell*
*Context gathered: 2026-05-04*
