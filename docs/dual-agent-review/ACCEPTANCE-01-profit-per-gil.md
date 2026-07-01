# ACCEPTANCE — Profit-per-gil ranking, absorption-capped Kelly sizing, Universalis enrichment (Tiers 1-3)

The contract both agents review against. Glossary terms in `/CONTEXT.md`; rationale in
`docs/adr/0001`–`0003`.

## Goal
Shift the scan from ranking by absolute expected profit to **profit-per-gil**: rank by
capital efficiency × confidence, size each flip by absorption-capped half-Kelly (recommending
a quantity), and enrich opportunities with Universalis competition/price data — all in one PR,
degrading gracefully to today's behavior when Universalis is unavailable.

## Acceptance criteria

1. **Capital-efficiency ranking.** Opportunities are ranked by
   `CapitalEfficiency = (ProfitPerUnit / CheapestPrice) × SalesPerDay`, raw velocity,
   tiebroken by ExpectedDailyProfit then ascending CheapestPrice. Replaces the
   `ExpectedDailyProfit` primary sort in `ScanEngine`.
2. **Floors unchanged.** `MinProfitAmount`, `PreferredRoi`, and `MinSalesPerDay` remain as
   admissibility filters (flat, not velocity-banded). `MaxItemsPerSession` still caps the route.
3. **Sell Confidence.** `c = d_exp / (d_exp + depth)` where `d_exp = SalesPerDay × HoldingWindowDays`
   and `depth` = home-world listing count. `depth = 0 ⇒ c = 1`. Final rank = `CapitalEfficiency × c × PriceConfidence`.
4. **Price Confidence (persistence).** Recent home-world sales (Universalis) corroborate the
   expected sell price: a 0–1 multiplier that discounts rank + size when recent median sale
   < `PriceCorroborationThreshold × expectedSellPrice`. Fewer than the minimum recent sales ⇒
   neutral (1.0). Never a hard filter.
5. **Absorption cap.** Per-opportunity unit ceiling `A = max(0, d_exp − depth)`. No recommended
   quantity exceeds `A` (or `d_exp` when depth is unavailable).
6. **Absorption-capped Kelly sizing.** Each kept opportunity gets a recommended **quantity**.
   Budget pool = `MaxBudgetPerSession`. Allocation ∝ `edge × c × PriceConfidence` scaled by
   `KellyFraction` (half-Kelly), each position bounded by `A` units and remaining budget. Total
   deployment may be **less** than the pool when absorption-limited (under-deploy is correct).
7. **Universalis enrichment.** After scan + filter, one batched Universalis call enriches the
   top survivors (≤100) on the home world for depth + recent sales. Controlled by
   `EnableUniversalis` (default true).
8. **Graceful degradation.** If Universalis is disabled, errors, or times out, the scan still
   completes using `depth = 0`, `PriceConfidence = 1` (velocity-only, today's behavior). A scan
   never fails because Universalis failed.
9. **UI.** The route window shows a recommended **quantity** per item and a one-line session
   deployment summary (gil deployed vs budget vs absorption ceiling). Sell-confidence/depth are
   secondary (tooltip/detail), not inline clutter.
10. **Config.** New persisted settings with defaults: `HoldingWindowDays=7`, `KellyFraction=0.5`,
    `EnableUniversalis=true`, `PriceCorroborationThreshold=0.9`, min-recent-sales-to-judge=3.
    Existing settings unchanged in meaning except `MaxBudgetPerSession` is now the Kelly pool.
11. **Cache.** `ScanCacheEnvelope` schema bumped v2→v3; v2 caches are treated as stale and
    trigger one fresh scan (no crash, no silent misread).
12. **Routing unchanged in spirit.** `RouteOptimizer` no longer applies the budget cap (Kelly owns
    sizing); it groups the sized (item, quantity) set into stops and minimizes hops. World travel
    is treated as free (no travel cost in selection).

## Completion tests

| Test / check | Covers | Runs in CI? |
|---|---|---|
| `NamazuFlippers.Tests` (xUnit) — CapitalEfficiency, SellConfidence, PriceConfidence, absorption cap, Kelly allocation (incl. under-deploy + graceful-degradation paths) on fixtures | 1,3,4,5,6,8 | yes — `dotnet test` wired into `build.yml` |
| `tests/phase09_nyquist.sh` — source-greps pipeline order, config fields, cache bump, Universalis client + degrade path, UI quantity/summary wiring | 2,7,9,10,11,12 | yes (added to CI) |
| `gh pr checks` build job | compiles against Dalamud | yes |

## Verification method
Criteria 1,3,4,5,6,8 → xUnit assertions on pure functions with hand-built fixtures (no Dalamud
needed). Criteria 2,7,9,10,11,12 → nyquist source validation + reviewer reads the diff. The
Reviewer (Pi) maps each criterion to test/code evidence before approving.

## Out of scope (follow-up issues, not blockers)
- Full stat-arb persistence (z-score / OU half-life) — only the lightweight corroboration proxy ships.
- Runtime gil detection / true-wealth Kelly (deliberately deferred; see ADR-0002).
- Cross-DC / purchase-world Universalis enrichment (home world only).
- Travel-cost-aware selection (world travel treated as free).
- Calibrating the volume proxy / holding window against the realized ledger (later, data-driven).

## Definition of done
The 5 objective gates in `PROTOCOL.md`: CI green · acceptance tests (xUnit + nyquist) green in CI ·
every criterion above satisfied · zero unresolved review threads · scope clean.
