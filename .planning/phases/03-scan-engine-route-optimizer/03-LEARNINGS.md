---
phase: 3
phase_name: "scan-engine-route-optimizer"
project: "Namazu Flippers"
generated: "2026-05-07"
counts:
  decisions: 6
  lessons: 7
  patterns: 6
  surprises: 5
missing_artifacts:
  - "03-UAT.md"
---

# Phase 3 Learnings: Scan Engine & Route Optimizer

## Decisions

### Local deterministic ranking instead of trusting API order
Rank scan results locally by `ExpectedDailyProfit` desc, then `SalesPerDay` desc, then `CheapestPrice` asc. Do not rely on Saddlebag's response order or any score field.

**Rationale:** The OpenAPI spec documents the request body fully but does not specify response ordering or a score contract. Treating returned order as a ranking would couple plugin behavior to undocumented API internals.
**Source:** 03-CONTEXT.md (D-11..D-14), 03-DISCUSSION-LOG.md, 03-RESEARCH.md §1

### Value-first route ordering with friction as tie-break only
Order route stops by total expected value descending. Apply travel-friction (same-DC) preference only when stop values are within ~20%.

**Rationale:** A user who runs only the first few stops should still hit the highest-value ones. Full TSP is overkill for FFXIV's coarse travel friction.
**Source:** 03-CONTEXT.md (D-21..D-24)

### File-backed scan cache in Phase 3, not deferred to Phase 5
Persist raw scan + derived route under `IDalamudPluginInterface.ConfigDirectory`, even though general session persistence is Phase 5.

**Rationale:** Lets the user log in, let the scan run, and do shopping later in the session — a core workflow that doesn't need full session state.
**Source:** 03-CONTEXT.md (D-25..D-30), 03-DISCUSSION-LOG.md

### Cache invalidation via schema + age + config fingerprint
Cache validity is checked across three independent axes. Any one mismatch invalidates.

**Rationale:** Schema bumps protect against deserialization failures; age handles staleness; config fingerprint prevents serving cached results that no longer match the user's filter intent.
**Source:** 03-02-SUMMARY.md, 03-VERIFICATION.md

### `/nflip` stays the UI toggle; `/nflip scan` for forced refresh
Bare `/nflip` is reserved for opening the future Phase 4 window. `/nflip scan` is the manual refresh that bypasses cache.

**Rationale:** Avoids overloading the primary command. Phase 3 needed a way to exercise the scan engine before Phase 4 UI exists.
**Source:** 03-CONTEXT.md (D-01..D-02, D-10), 03-02-SUMMARY.md

### Realistic profit uses `min(home_listing, avg_ppu) × 0.95 − ppu`
Use the lower of current home listing and historical average sale price as the expected sell price. Apply 5% market tax. Compute per-unit profit from there. (Added in-session — commits 0ba88eb / e046e29)

**Rationale:** API's `profit_amount` field is computed against `home_server_price` (current listing ceiling). When home is listed unrealistically high, that "profit" is fictional — you'd get undercut and clear at `avg_ppu`. The min preserves realistic expectations.
**Source:** Commit 0ba88eb, in-session validation against live API data

---

## Lessons

### Local `dotnet build` cannot verify Dalamud plugin builds on Mac
The Dalamud SDK's `Dalamud.NET.Sdk` resolves dependencies from `DALAMUD_HOME`, which doesn't exist on macOS without a manually staged Dalamud distribution. All Phase 3 plans flagged this as the same environmental gap.

**Context:** Both 03-01 and 03-02 self-checks passed deterministic source checks (grep) but could not reach a clean compiler diagnostic. Phase 3 ultimately moved to a CI-backed workflow (commit e53b827) so builds run on Ubuntu where Dalamud assemblies stage cleanly.
**Source:** 03-01-SUMMARY.md "Deviations", 03-02-SUMMARY.md "Deviations", 03-VERIFICATION.md "Manual Verification Debt"

### Phase 2's `ScanResponse` wrapper-key was a guess that turned out wrong
Phase 2 modeled the response with a single guessed wrapper. Phase 3 hardened it with a normalizer that accepts root arrays + `items`/`results`/`data` keys. The live API actually wraps in `data` — and once that was confirmed, the inner shape (snake_case fields, string item_id, string sale_rates) was completely different from what `ScanItem` expected.

**Context:** Even the hardened normalizer failed end-to-end because the inner row shape diverged. Real fix required a separate wire-shape DTO (`RawScanItem`) plus a translation layer.
**Source:** 03-01-SUMMARY.md "Deviations", commit e046e29

### Saddlebag's `/api/scan` rejects requests with no User-Agent
Default `HttpClient` sends no `User-Agent`. The API (or Cloudflare WAF in front of it) returns `401 {"exception":"Unauthorized"}` — looks exactly like an auth failure but is a bot/UA gate.

**Context:** Reproduced via `curl -H "User-Agent: "` returning 401, vs any non-empty UA returning 200. Fix: set `DefaultRequestHeaders.UserAgent` to `NamazuFlippers/<version> (+repo url)`.
**Source:** Commit b1dc49d, in-session diagnosis

### `sale_rates` is per-day, not per-hour
The OpenAPI doc notes "stats max out at 40 sales due to db limitations" which suggested a per-hour cap (40/168h ≈ 0.238/hour). But high-volume items return values like 13.49 — clearly per-day. Verified by cross-checking against `regionWeeklyQuantitySold*` counts.

**Context:** Initial mapping assumed per-hour and multiplied by 24, inflating velocity 24× for high-volume items. Correct interpretation: the field is sales/day on the home server averaged over `hours_ago`.
**Source:** Commit 0ba88eb, in-session validation

### API's `home_server_price` uses sentinel 999_999_999 for out-of-stock
Out-of-stock items don't return null or zero — they return a max-int-like sentinel. Direct use as a sell price gave fictional 949M-gil profits.

**Context:** Detection threshold of `>= 900_000_000` in `MapItem` is conservative. OOS items get `HomePrice = 0` and `OutOfStock = true` flag.
**Source:** Commit e046e29

### EDP already encodes the "speed vs profit" trade-off correctly
A user request for "weight speed higher than profit" was actually a request for a **velocity floor**, not a re-weighted score. EDP = profit-per-unit × sales-per-day already does the apples-to-apples comparison. The remaining issue was statistical reliability of low-velocity rate estimates.

**Context:** Resolved by adding `MinSalesPerDay = 0.33` as a reliability threshold (not a preference setting). Below that, the rate is computed from too few observations to trust.
**Source:** Commit 2db776d, in-session conversation

### CI must rebase against auto-bumps before pushing
The build workflow auto-commits version bumps to main on every push. Local rebases must `git fetch && git rebase origin/main` between successive pushes within a session, otherwise `git push` is rejected.

**Context:** Hit this 3 times in one session — each fix push triggered a CI version bump that arrived at origin before the next local push.
**Source:** Push rejection during 4 sequential fix commits this session

---

## Patterns

### Constructor-injected dependencies with `/nflip:` log prefix
Every Phase 3 service (`ScanEngine`, `RouteOptimizer`, `ScanCacheStore`) takes its dependencies as constructor params with explicit null checks. User-visible operational logs are prefixed `/nflip:`.

**When to use:** Any new service in this codebase. Keeps testability high and gives users a consistent log filter for plugin activity.
**Source:** 03-PATTERNS.md, `SaddlebagClient.cs`, `ScanEngine.cs`

### Wire-shape DTO decoupled from domain model via boundary translation
Define a separate DTO (`RawScanItem`) matching the live API's exact field names and types, then translate to the domain model (`ScanItem`) in `NormalizeScanResponse`. Keeps `ScanEngine`, `ScanCacheStore`, and downstream code unaware of API quirks.

**When to use:** Any time a wire format diverges from a domain model — type mismatches (string vs int), naming convention drift (snake_case vs camelCase), sentinel values, computed fields.
**Source:** Commit e046e29, `NamazuFlippers/API/Models/RawScanResponse.cs`

### Structured result type instead of exceptions or null
`ScanEngineResult` exposes `Status` (Success/Empty/Error/UsingCache/UsingStaleCache), `UserMessage` (friendly), and `TechnicalDetails` (logs). Callers never see raw exceptions or null routes.

**When to use:** Any user-facing operation with multiple distinct outcome types. Especially when both UI-friendly and operator-friendly information must travel together.
**Source:** 03-PATTERNS.md, `Core/ScanEngineResult.cs`

### Source-generated JSON registration with snake_case naming policy
`ApiJsonContext` registers all serializable types with `JsonKnownNamingPolicy.SnakeCaseLower`. New types must be added to the context — runtime reflection-based JSON is not acceptable.

**When to use:** Any new serializable type in this AOT/trim-sensitive codebase. Add to `ApiJsonContext` and use the source-gen `Default.<Type>` accessor.
**Source:** 03-PATTERNS.md, `API/Models/ApiJsonContext.cs`

### `Interlocked.Exchange` guard for at-most-one-concurrent operations
Duplicate scan requests use `Interlocked.Exchange` to atomically claim a "scan in flight" slot. Second invocation logs and exits cleanly.

**When to use:** Any user-triggered async operation that should not overlap with itself — scan, login auto-run, refresh button.
**Source:** 03-02-SUMMARY.md (Task 5), `NamazuFlippers/NamazuFlippers.cs`

### Centralized static lookup tables (WorldData) for game constants
World names, data-center mappings, and travel friction live in a single static `WorldData` class. `FirstRunWindow` reads from the same source.

**When to use:** Any in-game data with no runtime API source — world lists, data centers, item categories. Avoids drift between callers.
**Source:** 03-PATTERNS.md, `Data/WorldData.cs`

---

## Surprises

### OpenAPI documents the request body but leaves the 200 response schema as `{}`
The Saddlebag spec at `docs.saddlebagexchange.com/openapi.json` has full `FFXIVResellingParams` for the request — every field, type, description. The 200 response schema for `/api/scan` is literally an empty object.

**Impact:** All response-shape knowledge had to be reverse-engineered from live calls. This was the single biggest source of in-session bugs (3 of 4 commits).
**Source:** In-session inspection of `/tmp/saddlebag-openapi.json`, commit e046e29

### API's `profit_amount` is computed against the listing ceiling, not the clearing price
For items where someone listed unrealistically high (`home_server_price = 60M`, `avg_ppu = 41M`), the API surfaces ~20M/day "profit" — but the realistic clearing price means it's a 0.5M/day **loss**. Top-ranked items by API's profit metric were money-pits.

**Impact:** First successful end-to-end scan returned a 102M expected daily profit total. After fix using `min(home_listing, avg_ppu) × 0.95 − ppu`, realistic total dropped to ~225K. User caught it immediately ("seems excessive").
**Source:** Commit 0ba88eb, in-session validation

### Empty User-Agent triggers Cloudflare/WAF 401 with API-style JSON body
Body is `{"exception":"Unauthorized"}` — looks exactly like a real auth-required API. Took 30 seconds of curl reproduction with `-H "User-Agent: "` to confirm; default `HttpClient` sends nothing.

**Impact:** First user-facing failure after CI ship was "API error 401: exception: unauthorized." Could have been mistaken for needing API credentials. Real fix was one line of HttpClient setup.
**Source:** Commit b1dc49d

### Phase 2 and Phase 3 verification both passed without any live API call
Phase 2 verification was deterministic source checks. Phase 3 verification was deterministic source checks. Both summaries flagged "build blocked by Dalamud SDK absence on Mac." Neither phase exercised the API end-to-end. The first real call happened post-ship via Dalamud in-game.

**Impact:** All 4 in-session bug fixes were issues that no source-grep verification could have caught. Goal-backward verification missed the goal-forward question "does the request actually parse the response?" Future API phases should mandate at least one curl/script reproduction against the live endpoint as a verification step.
**Source:** 03-01-SUMMARY.md, 03-02-SUMMARY.md, 03-VERIFICATION.md, in-session bug discovery

### 1752 items in the response, only ~30 were realistic flips after filters
Naive top-by-EDP showed inflated 100M/day routes. Layering in realistic profit math + 1M budget cap + 0.33/day velocity floor brought the count from 1751 candidates to 51 reliable ones — and the top route from 102M to 225K gil/day. Each filter individually changed top-K rankings; together they revealed the real opportunity surface.

**Impact:** Validated that ranking quality matters more than candidate count for this product. The Phase 3 cap settings (`MaxItemsPerSession`, `MaxServersToVisit`) are necessary but not sufficient — domain filters (budget, velocity, realistic profit) belong in this layer, not deferred to UI.
**Source:** In-session iteration on live API data, commits 0ba88eb and 2db776d
