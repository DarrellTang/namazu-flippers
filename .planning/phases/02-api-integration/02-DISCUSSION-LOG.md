# Phase 2: API Integration - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-05-05
**Phase:** 02-api-integration
**Areas discussed:** Error handling UX, Model fidelity, HTTP client resilience, Rate limiter design

---

## Error handling UX

| Option                  | Description                                                                                          | Selected |
| ----------------------- | ---------------------------------------------------------------------------------------------------- | -------- |
| Dalamud chat message    | Print error to Dalamud chat log — familiar, non-intrusive, visible in-game                           |          |
| In-window error banner  | Show red error banner inside the plugin window — more visible but requires window to be open         |          |
| Both chat + window      | Chat message for awareness AND error state in plugin window                                          | ✓        |

**User's choice:** Both chat + window
**Notes:** Most thorough approach — ensures the player sees the error whether the window is open or not.

---

## Model fidelity

| Option                          | Description                                                                     | Selected |
| ------------------------------- | ------------------------------------------------------------------------------- | -------- |
| Phase 3 needs only              | Model only fields the Scan Engine will actually use — lean, faster to build     | ✓        |
| Full API schema                 | Model every field the API returns — future-proof but more maintenance           |          |
| Full schema with JSON ignore    | Model everything but mark unused fields — compromise, full shape + explicit use |          |

**User's choice:** Phase 3 needs only
**Notes:** Fields to model: item name, item ID, home price, cheapest server, cheapest price, sales velocity, expected daily profit, OOS flag, plus any fields the Scan Engine ranking/grouping logic requires.

---

## HTTP client resilience

| Option                       | Description                                                                     | Selected |
| ---------------------------- | ------------------------------------------------------------------------------- | -------- |
| Retry with backoff           | Auto-retry 2-3 times with exponential backoff before surfacing error            | ✓        |
| Single retry, then fail      | One immediate retry, then surface error — simpler, faster failure feedback       |          |
| Fail immediately             | No retries — surface error on first failure, let user decide when to retry       |          |

**User's choice:** Retry with backoff
**Notes:** Standard pattern for API clients. Handles most transient issues silently. Non-transient failures (4xx, 5xx) surface immediately.

---

## Rate limiter design

| Option                    | Description                                                                           | Selected |
| ------------------------- | ------------------------------------------------------------------------------------- | -------- |
| Simple minimum delay      | Enforce a minimum time between API calls — simple, predictable, sufficient for 1-2 calls/session | ✓ |
| Token bucket              | Token bucket with configurable rate and burst — more sophisticated, flexible          |          |
| Just log warnings         | No enforcement — rely on infrequent manual scans being naturally polite               |          |

**User's choice:** Simple minimum delay
**Notes:** Both endpoints are Universalis-safe. Rate limiter is a politeness safeguard, not a hard constraint. Exact delay duration left to agent discretion.

---

## the agent's Discretion

- Exact retry count, backoff multiplier, and timeout duration
- Minimum delay value for rate limiter
- Model class naming and namespace structure
- HTTP client construction approach (raw `HttpClient`, `IHttpClientFactory`, etc.)
- Whether to make the client injectable as a Dalamud service or use singleton pattern
- Deserialization library choice (System.Text.Json vs Newtonsoft.Json)

## Deferred Ideas

- Shortage predictor endpoint (`POST /api/ffxiv/shortagefutures`) — Phase 6
- Scan engine that consumes this client — Phase 3
- Scan result caching — Phase 3
- Session persistence — Phase 5
