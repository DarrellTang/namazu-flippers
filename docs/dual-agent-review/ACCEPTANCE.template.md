# ACCEPTANCE — <PR title>

> The contract both agents review against. Filled during Phase 0 Q&A. Keep it
> testable: every criterion must map to a check the Reviewer can verify.

## Goal
<1–2 sentences: what this PR makes true that wasn't before.>

## Acceptance criteria
<Numbered, each independently verifiable. "Ranking key X is computed as Y and the
top-N route reflects it" — not "improve ranking".>

1.
2.
3.

## Completion tests
<How each criterion is proven, using the three tiers in `VERIFICATION-POLICY.md`:
🟩 **test** (a `NamazuFlippers.Tests` unit test — for logic reachable without Dalamud),
🟨 **diff-read** (the reviewer confirms a safeguard in the diff), or
🟦 **smoke** (the owner runs the published plugin — for visual/UI criteria, post-merge).
Name the test file / thread / repro and which criterion each covers.>

| Test / check | Covers criteria | Tier |
|---|---|---|
| `NamazuFlippers.Tests/…Tests.cs` | 1, 2 | 🟩 test — `dotnet test` in CI |
| reviewer confirms in diff | 3 | 🟨 diff-read |
| owner in-game smoke check | 4 | 🟦 smoke — post-merge |
| `gh pr checks` build job | compiles | yes |

## Verification method
<For each criterion: automated test / CI check / manual demonstration. Manual items
need a written repro the Reviewer can follow.>

## Out of scope
<Explicit non-goals. Anything here is a follow-up issue, not a blocker.>

## Definition of done
The 5 objective gates in `PROTOCOL.md` (CI green · acceptance tests green in CI ·
every criterion satisfied · zero unresolved threads · scope clean).
