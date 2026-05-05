# Saddlebag Arbitrage

## What This Is

A Dalamud plugin (XIV Launcher) for Final Fantasy XIV that automates daily cross-world market board arbitrage. The player opens the plugin, gets a ranked list of 5–10 items to flip across servers, follows a route-optimized shopping list, and completes a 15–20 minute session for consistent daily gil profit. Designed for minimal effort with maximal consistent return — not max-ROI hunting.

## Core Value

A single button gives you today's best arbitrage route. Follow it, buy, list, done in under 20 minutes. Every day.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] PLUGIN-01: User can configure home world, profit thresholds, category filters, and API settings via a config window
- [ ] SCAN-01: Plugin calls `/api/scan` on demand (or uses cached results) and returns ranked arbitrage opportunities
- [ ] ROUTE-01: Plugin groups purchased items by cheapest server and optimizes visit order to minimize server hops
- [ ] UI-01: DailyRouteWindow shows today's route: which servers to visit, what to buy at each, prices, and expected profit
- [ ] UI-02: One-click checkboxes mark items as bought/listed with running profit tally
- [ ] UI-03: OOS (out-of-stock) items are visually highlighted as priority opportunities
- [ ] UI-04: Rescan button re-runs the API query to refresh opportunities
- [ ] SESSION-01: Session state (route, bought/listed status, profit tally) persists locally in JSON
- [ ] SESSION-02: Scan results are cached with configurable expiry (default 4 hours)
- [ ] OPT-01: Optional shortage-predictor supplement via `/api/ffxiv/shortagefutures`
- [ ] INTEG-01: Optional market board hook detects when player is at market board
- [ ] INTEG-02: Optional server travel hook auto-advances route to current server

### Out of Scope

- Undercut monitoring / re-listing alerts — items sell fast by design
- Price history charts and trend analysis — not needed for daily-flip items
- Multi-signal aggregation engine — `/api/scan` handles all signal fusion internally
- Background scanning / polling every 5–15 minutes — one scan per session
- Allagan Tools bridge integration — overkill for this scope
- Right-click item detail window — decisions are made by the scanner
- Constant monitoring dashboard — fire-and-forget session model

## Context

**Technical environment:**

- **Framework**: Dalamud plugin for XIV Launcher (C#, .NET)
- **UI**: ImGui (ImGui.NET) via Dalamud's windowing system
- **API**: Saddlebag Exchange REST API (`/api/scan` primary, `/api/ffxiv/shortagefutures` optional)
- **Data source**: Universalis (crowdsourced market board data)
- **Persistence**: Local JSON file for session state and scan cache

**Key technical decisions already made:**

- Single `/api/scan` endpoint replaces a 3-step API pipeline (marketshare → bestdeals → rank)
- `include_vendor=true` and `show_out_stock=true` flags maximize opportunity discovery
- Scoring model: `expected_daily_profit = margin × sales_per_day` with OOS priority boost
- No database needed — JSON file persistence is sufficient for session state + cache

**What "success" looks like for the player:**
Login → open plugin → see today's route → travel to server → buy items → return home → list items → done. 15–20 minutes. Consistent daily profit measured in hundreds of thousands of gil, not theoretical millions that sit for weeks.

## Constraints

- **Dalamud API**: Must target current stable Dalamud API version; plugin loads within XIV Launcher sandbox
- **Saddlebag Rate Limits**: `/api/scan` has no Universalis warning (safe); shortage-predictor also safe. Still implement polite rate limiting.
- **.NET Version**: Dalamud plugins target .NET 8+ via Dalamud SDK
- **UI Framework**: ImGui via Dalamud's `PluginUI` / `WindowSystem` — no HTML/CSS, no WPF
- **Distribution**: Plugin distributed via Dalamud plugin repository (requires manifest + approval)
- **Single Player**: Plugin serves one player on one home world; no multi-character or multi-account support needed

## Key Decisions

| Decision | Rationale | Outcome |
| -------- | --------- | ------- |
| Use `/api/scan` as sole discovery endpoint | Combines velocity filtering, cross-server price comparison, OOS detection, vendor items, and ranking in one call | - Pending |
| Build for daily session workflow (not constant monitoring) | User wants consistent daily profit, not max-ROI hunting | - Pending |
| JSON file persistence over SQLite | Session state is simple (5–10 items, checkboxes); no query complexity needed | - Pending |
| OOS priority built into scan params | `show_out_stock=true` surfaces zero-listing items without custom logic | - Pending |
| Vendor items included by default | `include_vendor=true` catches NPC-purchased flips players overlook | - Pending |

---
*Last updated: 2026-05-04 after initialization*
