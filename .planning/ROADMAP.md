# Roadmap: Namazu Flippers

## Overview

Build a Dalamud plugin from scratch that connects to the Saddlebag Exchange API, discovers daily cross-server arbitrage opportunities, presents them in a route-optimized in-game UI, and tracks whether those flips actually produced profit. The project follows a dependency-driven build order: plugin shell -> API layer -> business logic -> UI -> persistence -> runtime hardening -> profit ledger -> reconciliation.

## Phases

- [x] **Phase 1: Plugin Shell & Configuration** - Dalamud project scaffold, manifest, config persistence
- [x] **Phase 2: API Integration** - HTTP client, endpoint models, rate limiter (completed 2026-05-06)
- [x] **Phase 3: Scan Engine & Route Optimizer** - API call orchestration, result ranking, server routing (completed 2026-05-07)
- [x] **Phase 4: Core UI** - DailyRouteWindow with route display, buy/list checkboxes, profit tally
- [x] **Phase 5: Session Persistence** - JSON session store, scan caching, resume support
- [x] **Phase 6: Runtime Hardening & Ledger Foundation** - stabilize persistence/runtime behavior and introduce durable flip positions
- [ ] **Phase 7: Manual Realized Profit Tracking** - mark items sold, capture sale price, compute item-level realized profit by buy date
- [ ] **Phase 8: Profit History UI** - daily/weekly/monthly profit history, open positions, sold-item review
- [ ] **Phase 9: Retainer/Gil Detection Spike** - determine what Dalamud can reliably read from retainers, gil totals, chat, or sale history
- [ ] **Phase 10: Assisted Reconciliation & Polish** - automate safe sale matching where possible and prepare release-quality UX

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
**Plans**: 9 plans (5 gap-closure plans added across three UAT rounds 2026-05-07/2026-05-08; UI-01 GAP-E1 closure pending UAT round 3 on next post-merge CI build)

Plans:
- [x] 04-00-PLAN.md — Create tests/phase04_nyquist.sh source-validation script (Wave 0)
- [x] 04-01-PLAN.md — Build WindowSystem foundation, DailyRouteWindow read-only layout, FirstRunWindow migration (UI-01)
- [x] 04-02-PLAN.md — Wire buy/list checkboxes, profit tally, progress, OOS, auto-collapse (UI-02..UI-07)
- [x] 04-03-PLAN.md — Build ConfigWindow with snapshot/dirty/discard and all CONF-01..09 controls (UI-08)
- [x] 04-04-PLAN.md — [gap-closure] Render listed-checkbox inline on every row; remove unreachable isHomeStop gate (UI-03, UI-04)
- [x] 04-05-PLAN.md — [gap-closure] Fix DrawProgressSection layout so Settings + Rescan both fit at 420px window (UI-01, UI-08)
- [x] 04-06-PLAN.md — [gap-closure] Guard ConfigWindow.OnOpen snapshot with !isDirty so Discard reverts correctly (UI-08)
- [x] 04-07-PLAN.md — [gap-closure] Scale-aware buttonSpacing + listed-checkbox column anchor (UI-01, UI-03, UI-04, UI-08)
- [x] 04-08-PLAN.md — [gap-closure] Rescan/Settings own-row + GlobalScale-scaled widths (UI-01, UI-08; closes GAP-E1, supersedes GAP-D1's user-visible mechanism)

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
- [x] 05-01-PLAN.md — Bundle SessionState POCO, schema v2 envelope, SaveSessionAsync, hydrate-on-load, save-on-toggle, Mark All row, phase05_nyquist.sh (SESS-01, SESS-02, SESS-03)

### Phase 6: Runtime Hardening & Ledger Foundation
**Goal**: Make the current route workflow reliable under in-game usage and create durable flip-position records for future realized-profit tracking
**Depends on**: Phase 5
**Requirements**: HARD-01, HARD-02, HARD-03, LEDGER-01, LEDGER-02, LEDGER-03
**Success Criteria** (what must be TRUE):
  1. Cache/session writes cannot race or corrupt `scan-cache.json`
  2. UI actions during scans have deterministic behavior and cannot silently lose user state
  3. Runtime diagnostics are intentional for release builds, not broad temporary hooks
  4. Each routed item can be represented as a durable flip position tied to the buy date
  5. Existing source-validation scripts match the runtime-discovered API and workflow semantics
**Plans**: 1 plan

Plans:
- [x] 06-01: Implement runtime hardening plus ledger foundation

### Phase 7: Manual Realized Profit Tracking
**Goal**: Let the player close positions manually when items sell and compute item-level realized profit
**Depends on**: Phase 6
**Requirements**: PROFIT-01, PROFIT-02, PROFIT-03, PROFIT-04
**Success Criteria** (what must be TRUE):
  1. Bought/listed positions can be marked sold with an actual sale price
  2. Realized profit is computed as sale price after market tax minus actual or planned buy price
  3. Sold outcomes remain tied to the original buy date/session
  4. Manual entry is fast enough to use after checking retainers
**Plans**: TBD

Plans:
- [ ] 07-01: Add sold-state workflow and realized-profit calculation

### Phase 8: Profit History UI
**Goal**: Show historical profit in a compact view that answers what sold, when it was bought, and how much it made
**Depends on**: Phase 7
**Requirements**: HIST-01, HIST-02, HIST-03, HIST-04
**Success Criteria** (what must be TRUE):
  1. Player can see today, 7-day, and 30-day realized profit
  2. Player can review open positions that are bought/listed but not sold
  3. Sold items are grouped or filterable by buy date
  4. Projected vs realized profit is clearly separated
**Plans**: TBD

Plans:
- [ ] 08-01: Build profit history and open-position views

### Phase 9: Retainer/Gil Detection Spike
**Goal**: Determine what profit-related data can be safely and reliably observed from the game runtime
**Depends on**: Phase 8
**Requirements**: AUTO-01, AUTO-02, AUTO-03
**Success Criteria** (what must be TRUE):
  1. Document whether Dalamud can read character gil and retainer gil totals reliably
  2. Document whether retainer sale events/history can be observed and matched to open positions
  3. Document blind spots such as teleport, repair, purchases, taxes, and ambiguous item matches
  4. Produce a go/no-go recommendation for assisted reconciliation
**Plans**: TBD

Plans:
- [ ] 09-01: Spike retainer/gil observability in a live Dalamud runtime

### Phase 10: Assisted Reconciliation & Polish
**Goal**: Use safe game-observed signals to reduce manual sale entry and prepare the plugin for release-quality use
**Depends on**: Phase 9
**Requirements**: AUTO-04, SHIP-01, SHIP-02, SHIP-03
**Success Criteria** (what must be TRUE):
  1. If reliable sale signals exist, sold positions can be suggested or auto-matched with confirmation
  2. If only gil totals exist, daily net-worth snapshots are clearly labeled as approximate
  3. Error states and edge cases are handled cleanly
  4. Plugin manifest and release artifacts are ready for Dalamud repository submission
**Plans**: TBD

Plans:
- [ ] 10-01: Assisted reconciliation and release polish

### Backlog: Opportunity Expansion
**Goal**: Add more opportunity sources only after the core route and profit-history loop is trustworthy
**Requirements**: OPT-01, OPT-02, OPT-03
**Deferred Ideas**:
- Toggleable shortage-predictor supplement via `POST /api/ffxiv/shortagefutures`
- Deduplicate shortage-predicted items against scan results
- Add configurable shortage thresholds if the supplement proves useful

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 7 -> 8 -> 9 -> 10

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Plugin Shell &amp; Configuration | 2/2 | ✓ Complete | 2026-05-06 |
| 2. API Integration | 2/2 | ✓ Complete | 2026-05-06 |
| 3. Scan Engine & Route Optimizer | 2/2 | ✓ Complete | 2026-05-07 |
| 4. Core UI | 9/9 | ✓ Complete | 2026-05-08 |
| 5. Session Persistence | 1/1 | ✓ Complete | 2026-05-11 |
| 6. Runtime Hardening & Ledger Foundation | 1/1 | ✓ Complete | 2026-06-13 |
| 7. Manual Realized Profit Tracking | 0/TBD | Not started | - |
| 8. Profit History UI | 0/TBD | Not started | - |
| 9. Retainer/Gil Detection Spike | 0/TBD | Not started | - |
| 10. Assisted Reconciliation & Polish | 0/TBD | Not started | - |

## Build Verification Policy

- GitHub Actions is the authoritative compile/package gate. The workflow downloads Dalamud into `DALAMUD_HOME`, builds on Ubuntu, packages `NamazuFlippers.zip`, creates a release, and updates `pluginmaster.json`.
- macOS local builds are source-validation only. `dotnet build NamazuFlippers/NamazuFlippers.csproj` fails locally when Dalamud assemblies are absent, which is expected for this developer environment.
- Use `bash tests/phase03_nyquist.sh` for local Phase 3 source validation, then rely on CI for compile/package verification.
