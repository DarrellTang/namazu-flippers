# ACCEPTANCE — Holding Window slider + Universalis transient-error retry

The contract both agents review against for PR #7. Follow-up to the Tiers 1-3 work
(previous contract archived at `ACCEPTANCE-01-profit-per-gil.md`). Glossary in `/CONTEXT.md`;
sizing rationale in `docs/adr/0002`–`0003`.

## Goal
Two usability/resilience fixes surfaced by live testing of the merged profit-per-gil build:
make the **holding window** tunable in-app (the primary lever for how many slow-moving
opportunities get a recommended quantity), and stop dropping Universalis enrichment on a
single transient gateway error.

## Acceptance criteria

1. **Holding Window control.** `ConfigWindow` renders an editable **slider** for
   `HoldingWindowDays` (range **1–30**), sets the dirty flag on change, clamps to the range,
   and carries a tooltip explaining the absorption/velocity trade-off (bigger window ⇒ larger
   absorption ceiling ⇒ more/larger recommended positions on slow items, at the cost of gil
   sitting longer).
2. **Setting persists.** `HoldingWindowDays` continues to round-trip through `ConfigWindow`'s
   `Snapshot` / `RestoreFrom` / `RestoreDefaults` (default 7), so the new control's value is not
   lost through discard/reset flows.
3. **Universalis transient-error retry.** `UniversalisClient` retries transient failures — HTTP
   **5xx (incl. 504)** and network/timeout errors — up to a bounded attempt count with exponential
   backoff. A **4xx** is not retried (client-side, won't self-heal).
4. **Graceful degradation preserved.** When retries are exhausted, or on any non-cancellation
   failure, the enrichment call resolves to an empty/partial result and the scan completes
   velocity-only (depth = 0, PriceConfidence = 1). Only genuine cancellation
   (`OperationCanceledException` with the token cancelled) propagates. A scan never fails because
   Universalis failed.
5. **No sizing/behavior regression.** The profit-per-gil ranking and absorption-capped Kelly math
   are unchanged; the existing `NamazuFlippers.Tests` (xUnit) and `tests/phase09_nyquist.sh` still
   pass in CI.

## Completion tests

| Test / check | Covers | Runs in CI? |
|---|---|---|
| `tests/phase10_nyquist.sh` — source-greps the Holding Window slider (label/range/clamp/tooltip/persistence) and the Universalis retry (bounded attempts, backoff, 5xx-only retry, degrade path) | 1,2,3,4 | yes (added to CI) |
| `tests/phase09_nyquist.sh` — unchanged Tier 1-3 pipeline/config/cache/UI validation | 5 (no regression) | yes |
| `NamazuFlippers.Tests` (xUnit, 43) — pure sizing math unchanged | 5 (no regression) | yes — `dotnet test` |
| `gh pr checks` build job | compiles against Dalamud | yes |

## Verification method
Criteria 1–4 → nyquist source validation (`phase10`) + reviewer reads the diff (the retry loop and
the slider are wiring, not pure functions — no new Dalamud-free unit surface). Criterion 5 →
existing xUnit + `phase09` stay green. The Reviewer (Pi) maps each criterion to code/test evidence
before approving.

## Out of scope (owner-declined or follow-up, not blockers)
- Showing quantity-0 ("market saturated") items greyed out — the owner explicitly declined this.
- UI widgets for the other Tier 1-3 settings (KellyFraction, EnableUniversalis toggle,
  PriceCorroborationThreshold, MinRecentSalesToJudge) — separate follow-up.
- Broadening category presets beyond Furniture/Collectibles/Glamour — separate follow-up.
- Tuning the retry counts/backoff against real Universalis rate limits (data-driven, later).

## Definition of done
The 5 objective gates in `PROTOCOL.md`: CI green · acceptance tests (phase10 + phase09 + xUnit)
green in CI · every criterion above satisfied · zero unresolved review threads · scope clean.
