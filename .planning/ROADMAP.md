# Roadmap: Namazu Flippers

## Overview

Build a Dalamud plugin from scratch that connects to the Saddlebag Exchange API, discovers daily cross-server arbitrage opportunities, and presents them in a route-optimized in-game UI. The project follows a dependency-driven build order: plugin shell → API layer → business logic → UI → persistence → polish.

## Phases

- [x] **Phase 1: Plugin Shell & Configuration** - Dalamud project scaffold, manifest, config persistence
- [x] **Phase 2: API Integration** - HTTP client, endpoint models, rate limiter (completed 2026-05-06)
- [x] **Phase 3: Scan Engine & Route Optimizer** - API call orchestration, result ranking, server routing (completed 2026-05-07)
- [ ] **Phase 4: Core UI** - DailyRouteWindow with route display, buy/list checkboxes, profit tally
- [ ] **Phase 5: Session Persistence** - JSON session store, scan caching, resume support
- [ ] **Phase 6: Optional Features** - Shortage predictor supplement, game integration hooks
- [ ] **Phase 7: Polish & Ship** - Error handling, edge cases, testing, manifest submission prep

## Phase Details

### Phase 1: Plugin Shell & Configuration
**Goal**: Plugin loads in XIV Launcher with configuration persistence
**Depends on**: Nothing (first phase)
**Requirements**: PLUG-01, PLUG-02, PLUG-03, CONF-01, CONF-02, CONF-03, CONF-04, CONF-05, CONF-06, CONF-07, CONF-08, CONF-09
**Success Criteria** (what must be TRUE):
  1. Plugin appears in XIV Launcher plugin list and loads without errors
  2. `/nflip` command opens and closes the plugin UI
  3. Home world prompt appears on first run and setting persists
  4. All configuration values persist across game sessions
  5. Plugin disposes cleanly (no crashes on logout/reload)
**Plans**: 2 plans (2 planned)

Plans:
- [x] 01-01: Scaffold Dalamud plugin project (manifest, entry point, build system)
- [x] 01-02: Implement configuration system with persistence

### Phase 2: API Integration
**Goal**: Plugin can call Saddlebag Exchange API and receive typed responses
**Depends on**: Phase 1
**Requirements**: API-01, API-02, API-03
**Success Criteria** (what must be TRUE):
  1. `POST /api/scan` returns parsed, typed response objects
  2. Rate limiter prevents excessive calls
  3. Network errors are handled gracefully with user feedback
**Plans**: 2 plans

Plans:
- [x] 02-01: Build HTTP client with endpoint models (request/response types)
- [x] 02-02: Implement rate limiter and error handling

### Phase 3: Scan Engine & Route Optimizer
**Goal**: Plugin discovers ranked arbitrage opportunities and builds an optimized server route
**Depends on**: Phase 2
**Requirements**: SCAN-01, SCAN-02, SCAN-03, SCAN-04
**Success Criteria** (what must be TRUE):
  1. Scan produces a ranked list of 5–10 arbitrage items
  2. Items are grouped by cheapest purchase server
  3. Server stops are ordered to minimize total hops
  4. OOS items receive priority placement in the route
  5. Scan results are cached and reused within the expiry window
**Plans**: 2 plans

Plans:
- [x] 03-01: Implement ScanEngine (API call → parse → rank → top N)
- [x] 03-02: Implement RouteOptimizer (group by server, minimize hops, world/DC data)

### Phase 4: Core UI
**Goal**: Player sees today's route in an ImGui window, clicks through items, and tracks profit
**Depends on**: Phase 3
**Requirements**: UI-01, UI-02, UI-03, UI-04, UI-05, UI-06, UI-07, UI-08
**Success Criteria** (what must be TRUE):
  1. DailyRouteWindow shows route with server stops, items, prices, and expected profit
  2. Each item has a clickable checkbox to mark "bought"
  3. Home stop shows items to list with "listed" checkboxes
  4. Running profit tally updates in real time
  5. Progress bar shows bought/total and listed/total completion
  6. OOS items are visually distinct (color/icon)
  7. Completed server stops auto-collapse
  8. ConfigWindow exposes all settings from CONF-01 through CONF-09
**Plans**: 4 plans

Plans:
- [ ] 04-00-PLAN.md — Create tests/phase04_nyquist.sh source-validation script (Wave 0)
- [ ] 04-01-PLAN.md — Build WindowSystem foundation, DailyRouteWindow read-only layout, FirstRunWindow migration (UI-01)
- [ ] 04-02-PLAN.md — Wire buy/list checkboxes, profit tally, progress, OOS, auto-collapse (UI-02..UI-07)
- [ ] 04-03-PLAN.md — Build ConfigWindow with snapshot/dirty/discard and all CONF-01..09 controls (UI-08)

### Phase 5: Session Persistence
**Goal**: Session state survives game restarts; scan cache avoids redundant API calls
**Depends on**: Phase 4
**Requirements**: SESS-01, SESS-02, SESS-03
**Success Criteria** (what must be TRUE):
  1. Route, bought/listed status, and profit tally persist to JSON on changes
  2. Reloading the plugin restores in-progress session if scan not expired
  3. "Mark All Bought" and "Mark All Listed" buttons work correctly
  4. Scan cache respects expiry duration; rescan invalidates cache
**Plans**: 1 plan

Plans:
- [ ] 05-01: Implement JSON-based SessionStore with cache expiry and bulk actions

### Phase 6: Optional Features
**Goal**: Shortage predictor supplement and basic game integration hooks
**Depends on**: Phase 5
**Requirements**: OPT-01, OPT-02, OPT-03
**Success Criteria** (what must be TRUE):
  1. Shortage predictor toggle in config enables supplementary `/api/ffxiv/shortagefutures` query
  2. Shortage items merge into route without duplicating scan results
  3. Shortage thresholds are configurable
**Plans**: 1 plan

Plans:
- [ ] 06-01: Add shortage predictor supplement with configurable thresholds

### Phase 7: Polish & Ship
**Goal**: Production-ready quality: error handling, edge cases, cleanup, ship-ready manifest
**Depends on**: Phase 6
**Requirements**: INTG-01, INTG-02 (optional hooks)
**Success Criteria** (what must be TRUE):
  1. All error states handled (API down, network failure, empty results, bad data)
  2. Edge cases covered (no opportunities found, all items already bought, expired session)
  3. Plugin manifest is complete and accurate for Dalamud repo submission
  4. Code is clean, documented, and follows Dalamud plugin conventions
  5. Market board hook detects market board presence (optional)
  6. Server travel hook detects travel and advances route (optional)
**Plans**: 2 plans

Plans:
- [ ] 07-01: Error handling, edge cases, and cleanup pass
- [ ] 07-02: Game integration hooks and manifest finalization

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Plugin Shell &amp; Configuration | 2/2 | ✓ Complete | 2026-05-06 |
| 2. API Integration | 2/2 | ✓ Complete | 2026-05-06 |
| 3. Scan Engine & Route Optimizer | 2/2 | ✓ Complete | 2026-05-07 |
| 4. Core UI | 0/4 | Not started | - |
| 5. Session Persistence | 0/1 | Not started | - |
| 6. Optional Features | 0/1 | Not started | - |
| 7. Polish & Ship | 0/2 | Not started | - |

## Build Verification Policy

- GitHub Actions is the authoritative compile/package gate. The workflow downloads Dalamud into `DALAMUD_HOME`, builds on Ubuntu, packages `NamazuFlippers.zip`, creates a release, and updates `pluginmaster.json`.
- macOS local builds are source-validation only. `dotnet build NamazuFlippers/NamazuFlippers.csproj` fails locally when Dalamud assemblies are absent, which is expected for this developer environment.
- Use `bash tests/phase03_nyquist.sh` for local Phase 3 source validation, then rely on CI for compile/package verification.
