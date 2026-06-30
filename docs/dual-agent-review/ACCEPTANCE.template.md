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
<The actual tests/commands that encode each criterion above. In this repo that is
typically a `tests/<phase>_nyquist.sh` source-validation script plus the CI `build`
job. Name the file(s) and which criterion each covers.>

| Test / check | Covers criteria | Runs in CI? |
|---|---|---|
| `tests/…_nyquist.sh` | 1, 2 | must be wired into build.yml |
| `gh pr checks` build job | compiles | yes |

## Verification method
<For each criterion: automated test / CI check / manual demonstration. Manual items
need a written repro the Reviewer can follow.>

## Out of scope
<Explicit non-goals. Anything here is a follow-up issue, not a blocker.>

## Definition of done
The 5 objective gates in `PROTOCOL.md` (CI green · acceptance tests green in CI ·
every criterion satisfied · zero unresolved threads · scope clean).
