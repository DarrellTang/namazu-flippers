# Namazu Flippers

## What This Is

A Dalamud plugin (XIV Launcher) for Final Fantasy XIV that supports daily cross-world market board arbitrage — named after the commerce-obsessed Namazu beast tribe. The player opens the plugin, gets a ranked list of items to flip across servers, follows a route-optimized shopping list, lists those items through retainers, and later tracks which flips sold and how much realized profit they produced. Designed for minimal effort with measurable consistent return — not max-ROI hunting or full accounting.

## Core Value

A single button gives you today's best arbitrage route. Follow it, buy, list, done in under 20 minutes. Then use the plugin as a lightweight flip journal so bought items, sold items, and realized profit are not tracked from memory.

## Requirements

### Validated

- PLUG-01, PLUG-02, PLUG-03: Plugin shell requirements validated in Phase 1
- CONF-01 through CONF-09: Full ConfigWindow UI for all 14 settings + home world prompt validated in Phase 4 (Discard flow fixed in 04-06 via `!isDirty` snapshot guard)
- SCAN-01, SCAN-02, SCAN-03, SCAN-04: Scan engine, route optimizer, cache, and manual refresh wiring validated in Phase 3
- UI-01 through UI-08: Core UI validated in Phase 4 — 8/8 must-haves verified after gap-closure (04-04 listed-checkbox + profit tally, 04-05 Settings/Rescan layout at 420px, 04-06 ConfigWindow Discard); 4 debug sessions resolved; UAT closed
- SESS-01 through SESS-03: Session persistence delivered in Phase 5 with bought/listed state in the scan-cache envelope and Mark All actions
- HARD-01 through HARD-03: Runtime hardening delivered in Phase 6 with shared cache write serialization, deterministic scan-time UI mutation behavior, and release-appropriate diagnostics
- LEDGER-01 through LEDGER-03: Durable bought-lot ledger foundation delivered in Phase 6 with independent `flip-ledger.json` persistence and original buy-date/session trace
- PROFIT-01 through PROFIT-04: Manual realized-profit tracking delivered in Phase 7 with sold entry, actual sale price capture, tax-adjusted profit math, partial closes, and preserved buy-date/session trace

### Active

- [ ] HIST-01: Historical realized-profit view
- [ ] AUTO-01: Retainer/gil observability spike for assisted reconciliation

### Out of Scope

- Undercut monitoring / re-listing alerts — items sell fast by design
- Full gil accounting across all income/expenses — teleport, repairs, incidental purchases, and unrelated rewards are accepted blind spots
- Price history charts and broad trend analysis — not needed for daily-flip items
- Multi-signal aggregation engine — `/api/scan` handles all signal fusion internally
- Background scanning / polling every 5–15 minutes — one scan per session
- Allagan Tools bridge integration — overkill for this scope
- Right-click item detail window — decisions are made by the scanner
- Constant monitoring dashboard — fire-and-forget session model

## Context

**Technical environment:**

- **Framework**: Dalamud plugin for XIV Launcher (C#, .NET)
- **UI**: ImGui (ImGui.NET) via Dalamud's windowing system
- **API**: Saddlebag Exchange REST API (`/api/scan` primary; `/api/ffxiv/shortagefutures` deferred to backlog)
- **Data source**: Universalis (crowdsourced market board data)
- **Persistence**: Local JSON files for scan cache, session state, and durable flip positions
- **Build verification**: GitHub Actions is the authoritative compiler/package gate. macOS local builds are source-validation only because `net10.0-windows` + `Dalamud.NET.Sdk` require a configured Dalamud SDK path.

**Key technical decisions already made:**

- Single `/api/scan` endpoint replaces a 3-step API pipeline (marketshare → bestdeals → rank)
- `include_vendor=true` and `show_out_stock=true` flags maximize opportunity discovery
- Scoring model now distinguishes projected route value from realized profit. UI uses per-sale profit for the actual buy-one/list-one workflow while scan ranking can still consider velocity.
- No database needed — JSON file persistence is sufficient for scan cache, session state, and a lightweight flip ledger

**What "success" looks like for the player:**
Login -> open plugin -> see today's route -> travel to server -> buy items -> return home -> list items through retainers -> later mark sold items and see realized profit by buy date. 15-20 minutes for the route, with historical profit visible without relying on memory.

## Constraints

- **Dalamud API**: Must target current stable Dalamud API version; plugin loads within XIV Launcher sandbox
- **Saddlebag Rate Limits**: `/api/scan` has no Universalis warning (safe). Still implement polite rate limiting.
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
| Profit tracking is position-based, not full accounting | User wants to know which bought items sold and what each made, while accepting incidental gil deltas as blind spots | Phase 6+ |
| Shortage predictor moved to backlog | In-game use shows the current route works; the missing value is outcome tracking, not more opportunity sources | Phase 6+ |
| Manual sale recording stays user-confirmed | Avoids incorrect ledger mutation before Phase 9 proves live-game observability is reliable | Phase 7 |

---
*Last updated: 2026-06-13 after Phase 7 added manual sold-entry and realized-profit tracking.*
