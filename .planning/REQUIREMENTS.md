# Requirements: Saddlebag Arbitrage

**Defined:** 2026-05-04
**Core Value:** A single button gives you today's best arbitrage route. Follow it, buy, list, done in under 20 minutes. Every day.

## v1 Requirements

### Plugin Shell

- [x] **PLUG-01**: Plugin loads in XIV Launcher with a valid Dalamud manifest (SaddlebagArbitrage.json)
- [x] **PLUG-02**: Plugin exposes a main command (`/saddlebag` or `/pbag`) to toggle the UI
- [x] **PLUG-03**: Plugin follows Dalamud lifecycle (constructor, Dispose) with proper cleanup

### Configuration

- [x] **CONF-01**: User can set home world (first-run prompt, persisted)
- [~] **CONF-02**: User can set profit thresholds (model ready; ConfigWindow UI in Phase 4)
- [~] **CONF-03**: User can set velocity floor (model ready; ConfigWindow UI in Phase 4)
- [~] **CONF-04**: User can toggle region-wide search (model ready; ConfigWindow UI in Phase 4)
- [~] **CONF-05**: User can toggle category filters (model ready; ConfigWindow UI in Phase 4)
- [~] **CONF-06**: User can toggle vendor/OOS items (model ready; ConfigWindow UI in Phase 4)
- [~] **CONF-07**: User can set session caps (model ready; ConfigWindow UI in Phase 4)
- [~] **CONF-08**: User can set cache duration (model ready; ConfigWindow UI in Phase 4)
- [x] **CONF-09**: All settings persist across sessions

### API Integration

- [ ] **API-01**: HTTP client calls `POST /api/scan` with configurable parameters and parses the response
- [ ] **API-02**: Rate limiter respects Saddlebag API limits (polite delays between calls)
- [ ] **API-03**: Request/response models are typed for all used endpoints

### Scan & Route

- [ ] **SCAN-01**: ScanEngine calls `/api/scan`, extracts ranked arbitrage opportunities, and returns top N items
- [ ] **SCAN-02**: RouteOptimizer groups items by cheapest server and sorts stops to minimize server hops
- [ ] **SCAN-03**: Scan results are cached locally with configurable expiry; stale cache skips API call
- [ ] **SCAN-04**: Rescan button invalidates cache and re-queries the API

### Core UI

- [ ] **UI-01**: DailyRouteWindow displays today's route: server stops in order, items to buy per stop with prices
- [ ] **UI-02**: Each item has a checkbox to mark "bought" at the purchase server
- [ ] **UI-03**: Home stop section shows items to list with "listed" checkboxes
- [ ] **UI-04**: Running profit tally updates as items are marked listed
- [ ] **UI-05**: Progress bar shows completion (bought/total and listed/total)
- [ ] **UI-06**: OOS (out-of-stock) items are visually highlighted with a priority indicator
- [ ] **UI-07**: Server stops auto-collapse after all items at that stop are bought
- [ ] **UI-08**: ConfigWindow provides settings UI matching CONF-01 through CONF-09

### Session Persistence

- [ ] **SESS-01**: Session state (items, bought/listed status, route, current stop) persists as JSON locally
- [ ] **SESS-02**: Session resumes on next login if still valid (scan not expired)
- [ ] **SESS-03**: "Mark All Bought" and "Mark All Listed" bulk actions available

## v2 Requirements

### Shortage Predictor

- **OPT-01**: Toggleable shortage-predictor supplement via `POST /api/ffxiv/shortagefutures`
- **OPT-02**: Shortage-predicted items merged into route (deduplicated against scan results)
- **OPT-03**: Shortage predictor has its own configurable thresholds (price-vs-median %, quantity-vs-avg %)

### Game Integration

- **INTG-01**: Market board hook detects when player is at market board, highlights relevant items
- **INTG-02**: Server travel hook auto-advances route to current server after travel

## Out of Scope

| Feature | Reason |
| ------- | ------ |
| Undercut monitoring / re-listing alerts | Items sell fast by design; no relisting needed |
| Price history charts (7/30/90 day) | Not needed for daily-flip items |
| Multi-signal trend analysis (weekly delta) | `/api/scan` handles signal fusion internally |
| Background scanner (every 5–15 min) | One scan per session; background polling overkill |
| Right-click item detail window | Decisions made by scanner, not player |
| Allagan Tools bridge | Overkill for daily session scope |
| Discord bot alerts | Out of scope for a Dalamud plugin |
| Multi-character / multi-home-world support | Plugin serves one player, one home world |
| Crafting profit calculator integration | Different workflow; this is buy-and-flip only |
| GC seal / scrip exchange integration | Different workflow; this is gil-to-gil arbitrage |

## Traceability

| Requirement | Phase | Status |
| ----------- | ----- | ------ |
| PLUG-01 | Phase 1 | Complete |
| PLUG-02 | Phase 1 | Complete |
| PLUG-03 | Phase 1 | Complete |
| CONF-01 | Phase 1 | Complete |
| CONF-02 | Phase 1 | Model Ready |
| CONF-03 | Phase 1 | Model Ready |
| CONF-04 | Phase 1 | Model Ready |
| CONF-05 | Phase 1 | Model Ready |
| CONF-06 | Phase 1 | Model Ready |
| CONF-07 | Phase 1 | Model Ready |
| CONF-08 | Phase 1 | Model Ready |
| CONF-09 | Phase 1 | Complete |
| API-01 | Phase 2 | Pending |
| API-02 | Phase 2 | Pending |
| API-03 | Phase 2 | Pending |
| SCAN-01 | Phase 3 | Pending |
| SCAN-02 | Phase 3 | Pending |
| SCAN-03 | Phase 3 | Pending |
| SCAN-04 | Phase 3 | Pending |
| UI-01 | Phase 4 | Pending |
| UI-02 | Phase 4 | Pending |
| UI-03 | Phase 4 | Pending |
| UI-04 | Phase 4 | Pending |
| UI-05 | Phase 4 | Pending |
| UI-06 | Phase 4 | Pending |
| UI-07 | Phase 4 | Pending |
| UI-08 | Phase 4 | Pending |
| SESS-01 | Phase 5 | Pending |
| SESS-02 | Phase 5 | Pending |
| SESS-03 | Phase 5 | Pending |
| OPT-01 | Phase 6 | Pending |
| OPT-02 | Phase 6 | Pending |
| OPT-03 | Phase 6 | Pending |
| INTG-01 | Phase 7 | Pending |
| INTG-02 | Phase 7 | Pending |

**Coverage:**
- v1 requirements: 30 total
- Mapped to phases: 30
- Unmapped: 0 ✓
- v2 requirements: 5 total
- Total: 35

---
*Requirements defined: 2026-05-04*
*Last updated: 2026-05-04 after initialization*
