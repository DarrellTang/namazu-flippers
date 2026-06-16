# Requirements: Saddlebag Arbitrage

**Defined:** 2026-05-04
**Core Value:** A single button gives you today's best arbitrage route. Follow it, buy, list, done in under 20 minutes. Then track which flips actually sold and how much profit they made.

## v1 Requirements

### Plugin Shell

- [x] **PLUG-01**: Plugin loads in XIV Launcher with a valid Dalamud manifest (SaddlebagArbitrage.json)
- [x] **PLUG-02**: Plugin exposes a main command (`/saddlebag` or `/pbag`) to toggle the UI
- [x] **PLUG-03**: Plugin follows Dalamud lifecycle (constructor, Dispose) with proper cleanup

### Configuration

- [x] **CONF-01**: User can set home world (first-run prompt, persisted)
- [x] **CONF-02**: User can set profit thresholds
- [x] **CONF-03**: User can set velocity floor
- [x] **CONF-04**: User can toggle region-wide search
- [x] **CONF-05**: User can toggle category filters
- [x] **CONF-06**: User can toggle vendor/OOS items
- [x] **CONF-07**: User can set session caps
- [x] **CONF-08**: User can set cache duration
- [x] **CONF-09**: All settings persist across sessions

### API Integration

- [x] **API-01**: HTTP client calls `POST /api/scan` with configurable parameters and parses the response
- [x] **API-02**: Rate limiter respects Saddlebag API limits (polite delays between calls)
- [x] **API-03**: Request/response models are typed for all used endpoints

### Scan & Route

- [x] **SCAN-01**: ScanEngine calls `/api/scan`, extracts ranked arbitrage opportunities, and returns top N items
- [x] **SCAN-02**: RouteOptimizer groups items by cheapest server and sorts stops to minimize server hops
- [x] **SCAN-03**: Scan results are cached locally with configurable expiry; stale cache skips API call
- [x] **SCAN-04**: Rescan button invalidates cache and re-queries the API

### Core UI

- [x] **UI-01**: DailyRouteWindow displays today's route: server stops in order, items to buy per stop with prices
- [x] **UI-02**: Each item has a checkbox to mark "bought" at the purchase server
- [x] **UI-03**: Home stop section shows items to list with "listed" checkboxes
- [x] **UI-04**: Running profit tally updates as items are marked listed
- [x] **UI-05**: Progress bar shows completion (bought/total and listed/total)
- [x] **UI-06**: OOS (out-of-stock) items are visually highlighted with a priority indicator
- [x] **UI-07**: Server stops auto-collapse after all items at that stop are listed
- [x] **UI-08**: ConfigWindow provides settings UI matching CONF-01 through CONF-09

### Session Persistence

- [x] **SESS-01**: Session state (route, bought/listed status, progress) persists as JSON locally inside the scan cache envelope
- [x] **SESS-02**: Session resumes on next login if still valid (scan not expired)
- [x] **SESS-03**: "Mark All Bought" and "Mark All Listed" bulk actions available

## v2 Requirements

### Runtime Hardening

- [x] **HARD-01**: Scan cache writes and session-state writes are serialized so they cannot corrupt or roll back `scan-cache.json`
- [x] **HARD-02**: UI actions during an in-flight scan cannot silently lose bought/listed state
- [x] **HARD-03**: Runtime diagnostics are release-appropriate and do not globally suppress unrelated plugin/application failures

### Flip Ledger

- [x] **LEDGER-01**: Each routed item can be stored as a durable flip position with item id/name, buy date, source server, planned buy/list prices, and planned profit
- [x] **LEDGER-02**: Position records survive plugin reloads independently of whether the current scan cache is fresh
- [x] **LEDGER-03**: Position records preserve the original buy date/session so future realized profit can be reported against when the item was bought

### Manual Realized Profit

- [ ] **PROFIT-01**: User can mark a bought/listed position as sold
- [ ] **PROFIT-02**: User can enter or confirm the actual sale price for a sold position
- [ ] **PROFIT-03**: Plugin computes realized item profit as `floor(sale_price * 0.95) - actual_or_planned_buy_price`
- [ ] **PROFIT-04**: Sold positions remain tied to their original buy date and can be reviewed later

### Profit History

- [ ] **HIST-01**: User can view realized profit for today, 7 days, and 30 days
- [ ] **HIST-02**: User can review open positions that are bought/listed but not sold
- [ ] **HIST-03**: User can review sold items by buy date/session
- [ ] **HIST-04**: UI clearly separates projected profit from realized profit

### Game Integration

- [ ] **AUTO-01**: Spike whether Dalamud can reliably read current character gil and retainer gil totals
- [ ] **AUTO-02**: Spike whether retainer sale events/history can be observed and matched to open positions
- [ ] **AUTO-03**: Document blind spots such as teleport, repair, purchases, taxes, and ambiguous item matches
- [ ] **AUTO-04**: If reliable sale signals exist, suggest or auto-match sold positions with confirmation

### Release Polish

- [ ] **SHIP-01**: Error states and edge cases are handled cleanly across scan, persistence, ledger, and profit-history flows
- [ ] **SHIP-02**: Plugin manifest and release artifacts are ready for Dalamud repository submission
- [ ] **SHIP-03**: Final source-validation and CI/package checks reflect the current runtime-discovered behavior

### Opportunity Expansion Backlog

- [ ] **OPT-01**: Toggleable shortage-predictor supplement via `POST /api/ffxiv/shortagefutures`
- [ ] **OPT-02**: Shortage-predicted items merged into route (deduplicated against scan results)
- [ ] **OPT-03**: Shortage predictor has its own configurable thresholds (price-vs-median %, quantity-vs-avg %)

## Out of Scope

| Feature | Reason |
| ------- | ------ |
| Undercut monitoring / re-listing alerts | Items sell fast by design; no relisting needed |
| Full accounting ledger for all gil income/expenses | The goal is flip outcome tracking, not complete personal finance |
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
| CONF-02 | Phase 4 | Complete |
| CONF-03 | Phase 4 | Complete |
| CONF-04 | Phase 4 | Complete |
| CONF-05 | Phase 4 | Complete |
| CONF-06 | Phase 4 | Complete |
| CONF-07 | Phase 4 | Complete |
| CONF-08 | Phase 4 | Complete |
| CONF-09 | Phase 1 | Complete |
| API-01 | Phase 2 | Complete |
| API-02 | Phase 2 | Complete |
| API-03 | Phase 2 | Complete |
| SCAN-01 | Phase 3 | Complete |
| SCAN-02 | Phase 3 | Complete |
| SCAN-03 | Phase 3 | Complete |
| SCAN-04 | Phase 3 | Complete |
| UI-01 | Phase 4 | Complete |
| UI-02 | Phase 4 | Complete |
| UI-03 | Phase 4 | Complete |
| UI-04 | Phase 4 | Complete |
| UI-05 | Phase 4 | Complete |
| UI-06 | Phase 4 | Complete |
| UI-07 | Phase 4 | Complete |
| UI-08 | Phase 4 | Complete |
| SESS-01 | Phase 5 | Complete |
| SESS-02 | Phase 5 | Complete |
| SESS-03 | Phase 5 | Complete |
| HARD-01 | Phase 6 | Complete |
| HARD-02 | Phase 6 | Complete |
| HARD-03 | Phase 6 | Complete |
| LEDGER-01 | Phase 6 | Complete |
| LEDGER-02 | Phase 6 | Complete |
| LEDGER-03 | Phase 6 | Complete |
| PROFIT-01 | Phase 7 | Pending |
| PROFIT-02 | Phase 7 | Pending |
| PROFIT-03 | Phase 7 | Pending |
| PROFIT-04 | Phase 7 | Pending |
| HIST-01 | Phase 8 | Pending |
| HIST-02 | Phase 8 | Pending |
| HIST-03 | Phase 8 | Pending |
| HIST-04 | Phase 8 | Pending |
| AUTO-01 | Phase 9 | Pending |
| AUTO-02 | Phase 9 | Pending |
| AUTO-03 | Phase 9 | Pending |
| AUTO-04 | Phase 10 | Pending |
| SHIP-01 | Phase 10 | Pending |
| SHIP-02 | Phase 10 | Pending |
| SHIP-03 | Phase 10 | Pending |
| OPT-01 | Backlog | Deferred |
| OPT-02 | Backlog | Deferred |
| OPT-03 | Backlog | Deferred |

**Coverage:**
- v1 requirements: 30 total
- Mapped to phases: 30
- Unmapped: 0 ✓
- v2 requirements: 28 total
- Total: 58

---
*Requirements defined: 2026-05-04*
*Last updated: 2026-06-13 — Roadmap reshaped after post-Phase-5 in-game usage. Shortage predictor moved to backlog; next milestone work prioritizes runtime hardening, durable flip positions, manual realized-profit tracking, profit history, and retainer/gil observability.*
