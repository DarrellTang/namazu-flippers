# Saddlebag Exchange FFXIV Cross-Server Arbitrage — Dalamud Plugin Handoff

## Project Goal

Build a Dalamud plugin (XIV Launcher) for a **daily 15–20 minute arbitrage session**: jump to 5–10 servers, buy high-velocity items cheap, list them on your home server. Minimal effort, consistent daily profit — not max-ROI hunting.

## Core Philosophy

> **`expected_daily_profit` beats `max_ROI%`**
>
> A 50% margin that sells 5× per day crushes a 500% margin that sells once a month.
> We optimize for *consistent, steady gil per session*, not theoretical max profit.

---

## The Big Optimization: `/api/scan` Does Everything in One Call

After scraping the full API docs, the single most important finding: **`POST /api/scan`** (FFXIV Scan Reselling Search) is purpose-built for exactly our workflow. It powers the `/queries/recommended` page on the Saddlebag website.

> *"This finds the best items to buy on other servers or from vendors and sell on your home server."*

It combines velocity filtering, cross-server price comparison, OOS detection, vendor items, and category filters into **one API call** with ranked results. No need for a multi-step pipeline.

### Scan Endpoint Parameters

| Parameter | Type | What It Does | Suggested Default |
|-----------|------|-------------|-------------------|
| `home_server` | string | Your home world | Configurable |
| `preferred_roi` | int | ROI% threshold (25 = 25% profit) | 25 |
| `min_profit_amount` | int | Minimum gil profit per item | 10,000 |
| `min_desired_avg_ppu` | int | Minimum avg price per unit on home server | 10,000 |
| `min_stack_size` | int | Minimum stack size (ignore single-item bait) | 1 |
| `hours_ago` | int | Sales data window in hours (168 = 7 days) | 168 |
| `min_sales` | int | Minimum sales in that window — **this is our velocity floor** | 2 |
| `hq` | bool | HQ only | false (NQ + HQ) |
| `filters` | int[] | Category/subcategory IDs | Furniture, glamour, collectibles, etc. |
| `region_wide` | bool | Search all DCs (true) or just your DC (false) | false |
| `include_vendor` | bool | Treat vendor NPCs as a "server" — **catches overlooked flips** | true |
| `show_out_stock` | bool | Show OOS items on home server — **built-in priority boost** | true |

### What This Replaces

Our original plan called for 3 API calls (marketshare → bestdeals → manual ranking). The scan endpoint collapses that into one:

```
// Single API call. That's it.
POST /api/scan
{
  "home_server": "Adamantoise",
  "preferred_roi": 25,
  "min_profit_amount": 10000,
  "min_desired_avg_ppu": 10000,
  "min_stack_size": 1,
  "hours_ago": 168,
  "min_sales": 2,
  "hq": false,
  "filters": [56,65,66,67,68,69,70,71,72,81,82, 75,80,90, 1,2,3,4,-5],
  "region_wide": false,
  "include_vendor": true,
  "show_out_stock": true
}

// Response: ranked list of items with:
//   - home server price
//   - cheapest server + price
//   - sales velocity
//   - profit per item
//   - ROI%
//   - OOS flag
// Just take the top 5-10.
```

---

## Optional Supplement: Shortage Predictor

`POST /api/ffxiv/shortagefutures` predicts items likely to go out-of-stock based on price-vs-median and quantity-vs-average trends. Useful as an optional second query to catch items the main scan might miss because they haven't gone OOS yet but are trending that way.

| Parameter | What It Does |
|-----------|-------------|
| `desired_price_vs_median_percent` | 140 = show items where current price is 40% above median |
| `desired_quantity_vs_avg_percent` | 50 = show items where current quantity is 50% below average |
| `desired_sales_per_week` | Minimum sales velocity |
| `desired_median_price` | Minimum median price threshold |

If enabled, run after the main scan and merge any non-duplicate items into the route.

---

## 2-Stage Daily Pipeline (Simplified)

| Stage | What It Does | API Calls |
|-------|-------------|-----------|
| **1. Scan & Route** | One API call to `/api/scan`. Take top 5-10 results. Group by cheapest server. Sort by fewest hops. Optionally supplement with shortage-predictor results. | 1–2 |
| **2. In-Game UI** | Today's route: where to go, what to buy, running profit tally. One-click "bought" / "listed". Session persists locally. | 0 |

---

## Plugin Architecture (Dalamud / C#)

```
Plugin/
├── SaddlebagArbitrage.json          // Plugin manifest
├── Core/
│   ├── ScanEngine.cs                // Calls /api/scan, parses response, optionally supplements with shortage-predictor
│   ├── RouteOptimizer.cs            // Groups items by cheapest server, sorts by fewest hops
│   └── SessionTracker.cs            // Tracks current session: items bought, items listed, running profit, completion
├── API/
│   ├── SaddlebagClient.cs           // HTTP client (single POST to /api/scan, optional POST to /api/ffxiv/shortagefutures)
│   ├── Endpoints.cs                 // Typed request/response models for scan + shortage-predictor
│   └── RateLimiter.cs               // Respects API limits (especially Universalis warning on some endpoints)
├── Data/
│   ├── SessionStore.cs              // Persists today's scan results + session state (JSON)
│   └── ServerData.cs                // World/DC name → ID mappings (e.g., Adamantoise → Aether)
├── UI/
│   ├── DailyRouteWindow.cs          // Main window: route, shopping list per server, profit tally, buy/list buttons
│   └── ConfigWindow.cs              // Settings: home world, ROI%, min profit, velocity floor, categories, region toggle
└── Integration/
    ├── MarketBoardHooks.cs          // Optional: detect when user is at market board
    └── ServerTravelHooks.cs         // Optional: detect server travel, advance route to current server
```

**Net reduction**: 9 core/API files → 7. `DailyScanner.cs` became `ScanEngine.cs` (simpler — one call). `SignalAggregator.cs`, `ArbitrageRanker.cs`, `WatchlistManager.cs` remain deleted.

---

## API Strategy (Final)

### Primary Endpoint

| Endpoint | Purpose | Frequency | Universalis Warning |
|----------|---------|-----------|---------------------|
| `POST /api/scan` | Single call: finds best cross-server flips ranked by profit/velocity | Once per session (cached 4h) | No direct Universalis call |

### Optional Endpoint

| Endpoint | Purpose | Frequency | Universalis Warning |
|----------|---------|-----------|---------------------|
| `POST /api/ffxiv/shortagefutures` | Predicts items going OOS soon | Once per session (cached 4h) | No direct Universalis call |

### Endpoints NOT Needed

| Endpoint | Why We Skip It |
|----------|---------------|
| `POST /api/ffxiv/marketshare` | Scan does this internally |
| `POST /api/ffxiv/bestdeals` | Scan does this internally |
| `POST /api/ffxiv/listings` | Not needed for fast-moving items |
| `POST /api/ffxiv/export-search` | We sell on home server only |
| `POST /api/ffxiv/item-history` | Not needed for daily-flip items |
| `POST /api/ffxiv/extended-history` | Not needed for daily-flip items |
| `POST /api/ffxiv/rawstats` | Scan provides what we need |
| `POST /api/ffxiv/weekly-price-group-delta` | Irrelevant at this velocity |

### Caching Strategy

- Scan results cached locally as JSON with timestamp
- Cache expires after 4 hours (configurable) or on manual rescan
- If cache exists and is fresh, skip API calls entirely on login
- Session state (bought/listed checkmarks) persists in the same cache file

---

## UI: DailyRouteWindow

```
┌─────────────────────────────────────────────────────────┐
│  Saddlebag Arbitrage — Today's Route                    │
│                                                         │
│  Expected Profit: 847,200 gil  │  5 items  │  4 stops   │
│                                                         │
│  ▸ Server 1: Lamia (Primal)                             │
│    Buy: □ Expanse Barding      12,000 (home: 98,000)   │
│    Buy: □ Sky Pirate's Mask    45,000 (home: 130,000)  │
│                                                         │
│  ▸ Server 2: Exodus (Primal)                            │
│    Buy: □ Dravanian Down Tree  8,500  (home: 52,000)   │
│                                                         │
│  ▸ Server 3: Famfrit (Primal)                           │
│    Buy: □ Carbuncle Lamp       22,000 (home: 75,000)   │
│    Buy: □ Oriental Partition   18,500 (home: 65,000)   │
│                                                         │
│  ▸ HOME: Adamantoise (Aether)                           │
│    List: all 5 items above                              │
│                                                         │
│  Progress: ████████░░░░░░░░  2/5 bought, 0/5 listed    │
│  Running Profit: 0 gil listed so far                    │
│                                                         │
│  [Rescan] [Mark All Bought] [Mark All Listed]           │
└─────────────────────────────────────────────────────────┘
```

### Features

- **One click per item** — checkbox to mark "bought" at each server
- **Running profit tally** — updates as items are listed
- **Route persists** — session saved locally, pick up where you left off
- **Rescan button** — re-run scan if market conditions changed
- **Collapsed by default** — only shows servers you haven't visited yet
- **OOS items highlighted** — visual indicator for items with zero home server listings

---

## ConfigWindow Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Home World | (first run prompt) | Your home server where retainers live |
| Preferred ROI% | 25 | Minimum ROI percentage for scan |
| Min Profit Per Item | 10,000 gil | Minimum gil profit per item |
| Min Avg Price (Home) | 10,000 gil | Ignore items that sell for less on home server |
| Velocity Floor | 2 sales/week | Minimum sales in 7-day window |
| Region Wide | Off | Search all DCs (on) or just your DC (off) |
| Include Vendors | On | Show vendor items as purchase sources |
| Show Out of Stock | On | Include items with zero home listings |
| Max Items Per Session | 10 | Cap on route size |
| Max Servers to Visit | 10 | Server hop budget |
| Preferred Categories | Furniture + Collectibles + Glamour | Toggle category filters |
| Enable Shortage Predictor | Off | Run separate shortage-predictor query |
| Cache Duration | 4 hours | How long scan results stay fresh |

---

## Session Persistence

```json
{
  "scan_date": "2026-05-04T18:30:00Z",
  "home_world": "Adamantoise",
  "home_world_id": 57,
  "scan_params": {
    "preferred_roi": 25,
    "min_profit_amount": 10000,
    "region_wide": false,
    "include_vendor": true,
    "show_out_stock": true
  },
  "items": [
    {
      "item_id": 24567,
      "name": "Expanse Barding",
      "home_price": 98000,
      "cheapest_server": "Lamia",
      "cheapest_price": 12000,
      "sales_per_day": 2.3,
      "expected_daily_profit": 197800,
      "out_of_stock": false,
      "bought": true,
      "listed": false
    }
  ],
  "route": [
    { "server": "Lamia", "dc": "Primal", "item_ids": [24567, 24568] },
    { "server": "Exodus", "dc": "Primal", "item_ids": [24569] }
  ],
  "current_stop": 1
}
```

---

## What Got Stripped Out (vs. Original Design)

| Removed | Why |
|---------|-----|
| ❌ 200–500 item watchlist | Replaced by top 5–10 from scan |
| ❌ Background scanner (every 5–15 min) | One scan on login, cached 4h |
| ❌ SignalAggregator | Scan endpoint handles all signals internally |
| ❌ ArbitrageRanker with multi-metric | Scan response is pre-ranked |
| ❌ Multi-step API pipeline (marketshare → bestdeals → rank) | Single `/api/scan` call |
| ❌ UndercutMonitor | Items sell fast by design |
| ❌ Trend/Delta tracking | Velocity floor in scan handles this |
| ❌ Price history charts | Not needed |
| ❌ Item Detail window | Not needed |
| ❌ ShoppingListWindow (separate) | Merged into DailyRouteWindow |
| ❌ Allagan Tools bridge | Overkill |

---

## Saddlebag Exchange Ecosystem

### Core Website: https://saddlebagexchange.com

### API Endpoints We Actually Use

| Endpoint | Role |
|----------|------|
| `POST /api/scan` | **Primary** — single call that finds, filters, and ranks cross-server arbitrage opportunities |
| `POST /api/ffxiv/shortagefutures` | **Optional** — predicts items trending toward out-of-stock |

### Web Tools (Reference)

| Tool | URL | Purpose |
|------|-----|---------|
| Reselling Trading Search | `/queries/recommended` | Same logic as `/api/scan`, web UI version |
| Best Deals | `/ffxiv/best-deals/recommended` | Region-wide deal discovery (backup reference) |
| Marketshare Overview | `/ffxiv/marketshare/queries` | What sells fast (backup reference) |
| Shortage Predictor | `/ffxiv/shortage-predictor` | Web UI for shortage futures |

### API Docs

- **ReDoc** (full spec): https://docs.saddlebagexchange.com/redoc
- **OpenAPI JSON**: https://docs.saddlebagexchange.com/openapi.json
- **Swagger**: https://docs.saddlebagexchange.com/docs
- **Postman Collection**: Public workspace "Saddlebag Exchange Public API"

### Data Source

All pricing data from **Universalis**, crowdsourced by players running Dalamud plugins. You contribute data by playing with XIV Launcher plugins active.

---

## Full API Endpoint Reference (from ReDoc Scrape)

### FFXIV Endpoints

| Endpoint | Description | Direct Universalis Call |
|----------|-------------|------------------------|
| `POST /api/scan` | Cross-server reselling search — ranked flips | No |
| `POST /api/bestdeals` | Items discounted vs regional average | No |
| `POST /api/export` | Compare prices across worlds (best world to sell) | No |
| `POST /api/ffxivmarketshare` | Server market economy heatmap | ⚠️ Yes |
| `POST /api/ffxivrawstats` | Detailed per-item market statistics (updated daily) | No |
| `POST /api/history` | 7-day sale history for one item | ⚠️ Yes |
| `POST /api/ffxiv/v2/history` | 7-day sale history v2 | ⚠️ Yes |
| `POST /api/listing` | Current listings across servers | ⚠️ Yes |
| `POST /api/ffxiv/v2/listing` | Listings v2 (enhanced) | ⚠️ Yes |
| `POST /api/parseallagan` | Allagan Tools inventory analysis | ⚠️ Yes |
| `POST /api/pricecheck` | Price sniper alerts | ⚠️ Yes |
| `POST /api/quantitycheck` | Quantity sniper alerts | ⚠️ Yes |
| `POST /api/salealert` | Sale alerts (retainer tracking) | No |
| `POST /api/selfpurchase` | Your purchase history | No |
| `POST /api/undercut` | Undercut alerts | ⚠️ Yes |
| `POST /api/v2/craftsim` | Crafting profit calculator | No |
| `POST /api/v2/shoppinglist` | Shopping list (up to 10 items) | ⚠️ Yes |
| `POST /api/ffxiv/blog` | Item description lookup | No |
| `POST /api/ffxiv/scripexchange` | Scrip exchange calculator | No |
| `POST /api/ffxiv/gcsealsexchange` | GC seal exchange (vendor turn-ins) | No |
| `POST /api/ffxiv/gcsealcrafting` | GC seal crafting | No |
| `POST /api/ffxiv/shortagefutures` | Shortage predictions | No |
| `POST /api/ffxiv/weekly-price-group-delta` | Weekly price trends per group | No |

### Key Takeaway

Of 23 FFXIV endpoints, we need **1** (plus optionally 1 more). The `/api/scan` endpoint was purpose-built for our exact use case.

---

## Proven Item Categories (from Saddlebag Wiki Guides)

| Category | Filters | Notes |
|----------|---------|-------|
| Furniture | 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 | High demand, low competition |
| Collectibles | 75, 80, 90 | Minions, mounts, orchestrion rolls |
| Glamour/Gear | 1, 2, 3, 4, -5 | Exclude crafted raid gear (-5) |
| Vendor Items | -1 | NPC-purchased items people overlook |
| Consumables | (toggleable) | High velocity, lower margins |

Configurable in plugin settings — user can toggle categories on/off. [Full category ID list on wiki](https://github.com/ff14-advanced-market-search/saddlebag-with-pockets/wiki/Item-categories-ids-and-list).

---

## Resources

- **Saddlebag Exchange**: https://saddlebagexchange.com
- **API Docs** (ReDoc): https://docs.saddlebagexchange.com/redoc
- **OpenAPI Spec**: https://docs.saddlebagexchange.com/openapi.json
- **GitHub Wiki** (guides): https://github.com/ff14-advanced-market-search/saddlebag-with-pockets/wiki
- **GitHub Main Repo**: https://github.com/ff14-advanced-market-search/saddlebag-with-pockets
- **Secret Sale Leads**: https://github.com/ff14-advanced-market-search/saddlebag-with-pockets/wiki/FFXIV-Sale-Leads
- **Category IDs**: https://github.com/ff14-advanced-market-search/saddlebag-with-pockets/wiki/Item-categories-ids-and-list
- **Discord**: https://discord.gg/saddlebag-exchange-973380473281724476
- **Universalis**: https://universalis.app

## Key Guides (Scraped)

1. "TLDR: How to earn gil with cross-server trading"
2. "How to Import, Trade and Flip items on the FFXIV Marketboard using Saddlebag Exchange Import Searches"
3. "A general guide on how to Import, Trade and Flip items on the FFXIV Marketboard"

## Skills Installed

### NotebookLM CLI
- **Package**: `teng-lin/notebooklm-py` (agent-agnostic, 12.5K stars)
- **Installed at**: `~/.pi/skills/notebooklm/SKILL.md`
- **CLI binary**: `/Users/darrelltang/.local/share/uv/tools/notebooklm-py/bin/notebooklm` (v0.3.4, managed via `uv`)
- **Auth**: Already logged in via Google OAuth (`notebooklm login`)
- **Useful for this project**: Feed scraped wiki guides into NotebookLM for an audio overview of arbitrage strategies — listen while playing FFXIV.

### Firecrawl
- Multiple skills available: `firecrawl-scrape`, `firecrawl-search`, `firecrawl-map`, `firecrawl-crawl`, `firecrawl-agent`, `firecrawl-download`
- CLI at `/opt/homebrew/bin/firecrawl`
- Used for scraping web pages, API docs, and wiki guides as needed
