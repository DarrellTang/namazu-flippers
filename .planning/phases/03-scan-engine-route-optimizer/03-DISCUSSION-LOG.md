# Phase 3: Scan Engine & Route Optimizer - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-05-07
**Phase:** 03-scan-engine-route-optimizer
**Areas discussed:** Scan Trigger Behavior, Ranking and Caps, Route Ordering, Scan Cache Boundary, Error and Empty-Result Behavior

---

## Scan Trigger Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Run scan immediately | `/nflip` toggles the plugin and kicks off scan engine | |
| Manual scan command | Keep `/nflip` as open/close, add `/nflip scan` | yes |
| Engine only for now | Build engine, wait for Phase 4 UI to trigger scans | |

**User's choice:** Manual scan command.
**Notes:** `/nflip` remains the UI toggle. Add `/nflip scan` so Phase 3 can be tested before the full UI exists. Normal success output should be concise; richer diagnostics appear on failure. Duplicate scans are ignored while one is running. Latest successful route is kept in memory for Phase 4.

---

## Ranking and Caps

| Option | Description | Selected |
|--------|-------------|----------|
| Trust API order plus caps | Use returned order and apply local caps | |
| Local deterministic ranking | Rank by local fields because API ordering is undocumented | yes |
| OOS boost/ranking | Give out-of-stock items ranking priority | |

**User's choice:** Local deterministic ranking.
**Notes:** The user questioned whether OOS means profit and whether Saddlebag documents a ranking system. Official OpenAPI docs do not document response order or score, so the decision is to rank locally by `ExpectedDailyProfit`, then `SalesPerDay`, then `CheapestPrice`. OOS remains UI metadata, not a profit override. Caps should optimize for route willingness. Profitable one-item stops are allowed. Vendor opportunities are included but preserved distinctly. If too few opportunities meet thresholds, return the smaller route rather than relaxing thresholds.

---

## Route Ordering

| Option | Description | Selected |
|--------|-------------|----------|
| Same data center first | Minimize travel friction first | |
| Highest-value stops first | Visit highest expected value stops first | yes |
| Fewest-hop stable order | Stable route order with lower optimization complexity | |

**User's choice:** Highest-value stops first.
**Notes:** Route ordering should be profit-first. Data center boundaries are ignored when value difference is meaningful. Hop/friction minimization is a tie-breaker only: if stop totals are within about 20%, prefer the lower-friction or same-DC stop. Phase 3 should add a hardcoded world-to-DC map.

---

## Scan Cache Boundary

| Option | Description | Selected |
|--------|-------------|----------|
| In-memory only | Latest route lasts only while plugin is loaded | |
| File-backed scan cache now | Persist latest scan/route with expiry | yes |
| Cache contract only | Define models now, storage later | |

**User's choice:** File-backed cache now.
**Notes:** Cache both raw scan response and derived route. Invalidate by age, scan-affecting config fingerprint, and schema/version changes. Store in the plugin-local/config data directory through Dalamud APIs. `/nflip scan` always refreshes and writes cache on success. The cache exists so the UI can show the latest route later, including after relog, without forcing a new API call.

### Auto-Scan Clarification

| Option | Description | Selected |
|--------|-------------|----------|
| Immediately on login/plugin-ready | Auto-scan as soon as character is available | |
| Small delay after login | Wait briefly after character login/plugin-ready before scanning | yes |
| Only when `/nflip` first opens | Avoid background API calls until explicit UI open | |

**User's choice:** Small delay after character login/plugin-ready.
**Notes:** "Login" means Dalamud character login, not game launch or title screen. Use `IClientState.Login` and also check `IsLoggedIn` during plugin startup. If valid cache exists, load it. If missing/expired/config-incompatible and required config exists, auto-start scan shortly after login.

---

## Error and Empty-Result Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Keep previous cache on refresh failure | Preserve last usable route when refresh fails | yes |
| Clear route on failure | Avoid stale route but leave user with nothing | |
| Keep cache only if fresh | Use fallback only within expiry | |

**User's choice:** Keep previous cache on refresh failure.
**Notes:** Stale cache is better than nothing when refresh fails, but it must be marked/logged as stale. If the API succeeds but returns no usable opportunities, return a structured empty route with a simple friendly message and do not automatically relax thresholds. If failure leaves no usable cache, return a structured error result. UI gets friendly messages; logs keep technical detail.

---

## the agent's Discretion

- Exact command parsing shape for `/nflip scan`.
- Exact successful scan summary log fields.
- Exact cache schema and config fingerprint implementation.
- Exact route/result model naming and folder placement.
- Exact delay mechanism after character login/plugin-ready.

## Deferred Ideas

- Future two-tier scan mode: strict priority settings first, then a relaxed fallback scan if no opportunities are found.
- Full UI, buy/list state, broader session persistence, shortage predictor, and game integration hooks remain future phases.
