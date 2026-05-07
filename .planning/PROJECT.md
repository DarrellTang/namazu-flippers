# Namazu Flippers

## What This Is

A Dalamud plugin (XIV Launcher) for Final Fantasy XIV that automates daily cross-world market board arbitrage — named after the commerce-obsessed Namazu beast tribe. The player opens the plugin, gets a ranked list of 5–10 items to flip across servers, follows a route-optimized shopping list, and completes a 15–20 minute session for consistent daily gil profit. Designed for minimal effort with maximal consistent return — not max-ROI hunting.

## Core Value

A single button gives you today's best arbitrage route. Follow it, buy, list, done in under 20 minutes. Every day.

## Requirements

### Validated

- PLUG-01, PLUG-02, PLUG-03: Plugin shell requirements validated in Phase 1
- CONF-01, CONF-09: Home world prompt and config persistence validated in-game
- CONF-02 through CONF-08: Configuration model ready; ConfigWindow UI in Phase 4
- SCAN-01, SCAN-02, SCAN-03, SCAN-04: Scan engine, route optimizer, cache, and manual refresh wiring validated in Phase 3

### Active

- [ ] PLUGIN-01: User can configure home world, profit thresholds, category filters, and API settings via a config window
- [ ] UI-01: DailyRouteWindow shows today's route: which servers to visit, what to buy at each, prices, and expected profit
- [ ] UI-02: One-click checkboxes mark items as bought/listed with running profit tally
- [ ] UI-03: OOS (out-of-stock) items are visually highlighted as priority opportunities
- [ ] SESSION-01: Session state (route, bought/listed status, profit tally) persists locally in JSON
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
- **Build verification**: GitHub Actions is the authoritative compiler/package gate. macOS local builds are source-validation only because `net10.0-windows` + `Dalamud.NET.Sdk` require a configured Dalamud SDK path.

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
- **.NET Version**: Current project targets `net10.0-windows` via `Dalamud.NET.Sdk/15.0.0`
- **UI Framework**: ImGui via Dalamud's `PluginUI` / `WindowSystem` — no HTML/CSS, no WPF
- **Distribution**: Plugin distributed via Dalamud plugin repository (requires manifest + approval)
- **Single Player**: Plugin serves one player on one home world; no multi-character or multi-account support needed
- **Local macOS limitation**: Do not treat macOS `dotnet build` failures caused by missing Dalamud assemblies as implementation failures. Use `bash tests/phase03_nyquist.sh` locally and rely on CI for compile/package verification.

## Key Decisions

| Decision | Rationale | Outcome |
| -------- | --------- | ------- |
| Plugin named "Namazu Flippers" with `/nflip` command | FFXIV-themed, memorable, short command for fast typing | Phase 1 |
| Use Dalamud built-in config serialization | Standard Dalamud plugin pattern, less boilerplate than custom JSON | Phase 1 |
| Minimal project scaffold in Phase 1 | Add folders (Core/, API/, etc.) as each phase needs them | Phase 1 |
| Simple ImGui popup for first-run home world | Lightweight, appears once, consistent with eventual ConfigWindow | Phase 1 |
| Use `/api/scan` as sole discovery endpoint | Combines velocity filtering, cross-server price comparison, OOS detection, vendor items, and ranking in one call | Phase 3 |
| Build for daily session workflow (not constant monitoring) | User wants consistent daily profit, not max-ROI hunting | - Pending |
| JSON file persistence for scan cache | Cache stores raw scan response and derived route under Dalamud plugin config data | Phase 3 |
| OOS priority built into scan params | `show_out_stock=true` surfaces zero-listing items without custom logic | Phase 3 |
| Vendor items included by default | `include_vendor=true` catches NPC-purchased flips players overlook | Phase 3 |
| CI is the authoritative build gate | Developer workspace is macOS; local compile lacks Dalamud SDK assemblies, while CI downloads Dalamud and packages releases | Phase 3 |

---
*Last updated: 2026-05-07 after Phase 3 validation/build-doc update*
