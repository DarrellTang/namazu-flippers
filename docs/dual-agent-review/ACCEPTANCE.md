# ACCEPTANCE — Universalis median sell price (outlier-robust pricing)

The contract both agents review against for this PR. Prior contracts archived at
`ACCEPTANCE-01-profit-per-gil.md`, `ACCEPTANCE-02-holding-window-retry.md`, and
`ACCEPTANCE-03-owned-verifier.md`. Verification tiers per `VERIFICATION-POLICY.md`; sizing/enrichment
rationale in `docs/adr/0003`; glossary in `/CONTEXT.md`.

## Goal
Stop presenting outlier-inflated sell prices. Saddlebag reports the **mean** recent price, which a
handful of 1M-gil misclick sales can blow up (observed live: a ~50k item shown as ~650k). Use the
**median** of recent home-world Universalis sales — immune to those outliers — as the expected sell
price for display, profit, and filtering, so fluke "flips" collapse to reality and disappear.

## Acceptance criteria

1. **Median sell price when corroborated.** When Universalis returns at least
   `MinRecentSalesToJudge` recent home-world sales, the expected sell price is the **median** of
   those sales (`OpportunityScoring.ResolveSellPrice`), not Saddlebag's average.
2. **Price-derived numbers recomputed.** On correction, `HomePrice`, `ProfitPerUnit`,
   `ExpectedDailyProfit`, and `CapitalEfficiency` are recomputed from the corrected price net of the
   5% market tax (`OpportunityScoring.NetProfitPerUnit`, `MarketTaxRate = 0.95`).
3. **Re-filter after correction.** The `MinProfitAmount` and `PreferredRoi` floors are re-applied on
   the corrected prices (`ScanEngine.IsStillAdmissible`); opportunities that no longer clear them are
   dropped — so an outlier-inflated average can no longer surface a fake flip.
4. **Unverified fallback + UI flag.** When Universalis is disabled/unavailable or there are too few
   recent sales, Saddlebag's average is retained and the opportunity is marked
   `PriceVerified = false`; the route window shows an unverified-price hint (marker + tooltip). This
   is never a hard filter beyond the existing floors — unverified items still show.
5. **Graceful degradation preserved.** A Universalis failure still completes the scan (velocity-only,
   every price unverified); no scan fails because Universalis failed.
6. **Pure + tested; no regression.** `ResolveSellPrice` and `NetProfitPerUnit` are pure and unit
   tested (median-applied, too-few-sales fallback, no-data fallback, outlier-robust median on the
   real turret fixture, and correction flipping a fluke below the profit floor). The existing suite
   stays green.

## Completion tests

| Test / check | Covers | Tier | Runs in CI? |
|---|---|---|---|
| `NamazuFlippers.Tests/PriceResolutionTests.cs` — median/fallback/outlier/floor-flip on the pure math | 1, 2, 6 | 🟩 Test | yes — `dotnet test` |
| Reviewer diff-read: `ScanEngine.ApplyScoring` correction + `IsStillAdmissible` re-filter, and the `DailyRouteWindow` unverified marker | 3, 4, 5 | 🟨 Diff-read | reviewer |
| `gh pr checks` build job (`dotnet build` + package) | compiles against Dalamud | 🟩 | yes |

Per `VERIFICATION-POLICY.md`, the pure price math is Tier 🟩 (owned unit tests) and the ScanEngine/UI
wiring is Tier 🟨 (reviewer reads the diff) — no nyquist source-greps.

## Verification method
Criteria 1, 2, 6 → xUnit on the pure functions (`dotnet test` in CI). Criteria 3, 4, 5 → the Reviewer
(Pi) reads the diff and maps each to the ScanEngine re-filter and the UI marker. The Reviewer confirms
each pure-math test fails a cheat (two-diff check) before approving.

## Out of scope (follow-up, not blockers)
- Deriving **velocity** from Universalis recent-sale timestamps (separate, larger trust fix).
- The "fat flip" ranking/funding mode (absolute-profit ranking + waterfall allocation).
- Collapsing the two conflicting velocity floors (Min Sales/Day vs /Week) into one control.
- Expanding category presets (Materials, etc.).

## Definition of done
The 5 objective gates in `PROTOCOL.md`: CI green · acceptance tests (`PriceResolutionTests` + full
`dotnet test`) green in CI · every criterion above satisfied · zero unresolved review threads · scope
clean.
