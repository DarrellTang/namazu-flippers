# Dual-Agent PR Review Loop (DRL)

A repeatable protocol for converging a pull request using two independent coding
agents that never talk directly:

- **Builder** — Claude Code. Implements the work, opens the PR, addresses findings.
- **Reviewer** — Pi (GPT-5.5). Reviews the diff against the contract, posts findings, approves.

They coordinate **only** through GitHub primitives on the PR — labels, review
comments/threads, review verdicts, CI checks, and one pinned status comment. The
PR is the message bus; neither agent needs the other's context.

Final merge is **always human**.

---

## The contract: `ACCEPTANCE.md`

Phase 0 produces an `ACCEPTANCE.md` committed to the branch (see
`ACCEPTANCE.template.md`). It is the single source of truth both agents review
against — Builder builds to it, Reviewer reviews against it. No criterion that
isn't in `ACCEPTANCE.md` may block the PR; no work outside its scope may be added.

---

## State machine (PR labels)

Exactly one `drl:*` label is set at all times. An agent acts **only on its turn**,
does its work, then flips the label. This is what prevents two agents writing at once.

| Label | Meaning | Whose turn | Exit |
|---|---|---|---|
| `drl:building` | Builder implementing; PR may be draft | Builder | → `needs-review` when pushed + CI green |
| `drl:needs-review` | Ready for review; CI is green | Reviewer | → `changes-requested` (findings) or → `converged` (approve) |
| `drl:changes-requested` | Findings posted, unresolved threads exist | Builder | → `needs-review` once all addressed + CI green |
| `drl:converged` | All done-gates pass | nobody (human merges) | terminal |
| `drl:blocked` | Guard tripped or disagreement | human | terminal until human acts |

```
building ──push+CI green──► needs-review ──findings──► changes-requested ──┐
                                 │                                         │
                                 └──approve+gates──► converged (human)     │
   ▲──────────────────────────── addressed + CI green ─────────────────────┘
   any state ──guard tripped / unresolved disagreement──► blocked
```

**Turn rule:** on each loop tick an agent reads the label. If it is not its turn,
it sleeps. If it is, it works, flips the label, updates the status comment, sleeps.

**Convergence is the Reviewer's call.** Builder never sets `converged`. When the
Reviewer approves, it verifies the done-gates and sets `converged` itself.

### Reviewer-internal subagents

The Reviewer may use fresh-context, read-only subagents to conserve the parent context
window on broad PRs. These helpers are internal to the Reviewer and are not extra DRL
actors:

- They inspect the PR/diff/files directly and return evidence-backed findings.
- They may not post GitHub comments, submit reviews, update labels, update the status
  comment, approve, request changes, or decide convergence.
- The parent Reviewer synthesizes their output, verifies actionable findings, applies the
  scope lock, and performs all PR mutations.

---

## Definition of done (objective gates — ALL must hold)

The Reviewer verifies these before approving + setting `drl:converged`:

1. **CI green** — `gh pr checks <PR>` shows the `build` job passing.
2. **Acceptance tests pass** — every test named in `ACCEPTANCE.md` exists and is
   green **in CI** (not just locally). If the acceptance tests aren't yet wired
   into `.github/workflows/build.yml`, wiring them in is part of the PR.
3. **Every acceptance criterion is satisfied** — Reviewer maps each numbered
   criterion in `ACCEPTANCE.md` to concrete code/test evidence.
4. **Zero unresolved review threads.**
5. **Scope clean** — diff stays within `ACCEPTANCE.md` scope; no creep.

Merge stays human: on `converged`, both agents stop and `@`-mention the owner.

---

## Anti-thrash guards

- **MAX_ROUNDS = 6.** Round counter lives in the status comment. Exceeding it →
  `drl:blocked` + ping human.
- **Zero-new-findings = approve.** A review round that surfaces no new actionable
  finding, with CI green, is an approval — do not invent nits to keep looping.
- **Monotonic progress.** Open-thread count must not increase two rounds running.
  If it does → `drl:blocked`.
- **Disagreement is bounded.** Builder may decline a finding **once**, with rationale
  posted in-thread. If the Reviewer re-asserts, Builder sets `drl:blocked` rather
  than thrashing. Human breaks the tie.
- **Scope lock.** New ideas discovered mid-loop become a follow-up issue
  (`gh issue create`), never new scope on this PR.
- **Watchdog (the budget brake).** Both agents run on subscriptions, so there is no
  dollar cost to cap — the scarce resources are wall-clock and the owner's attention.
  Timed from the PR's own `drl:*` label events: if the loop's **age > 2h** or it sits
  **idle > 30m** (no label change — usually a human step was missed), the Builder sets
  `drl:blocked` and `@`-mentions the owner with the concrete next action. This bounds a
  self-scheduling loop and nudges the owner when they are the gap.

---

## Pinned status comment

One comment, marked with `<!-- DRL-STATUS -->`, both agents read and update each
round. It is the human's at-a-glance dashboard:

```
<!-- DRL-STATUS -->
## 🔁 DRL status
- Round: 2 / 6
- Last actor: Reviewer (Pi)
- State: drl:changes-requested
- CI: ✅ build passing
- Open threads: 3
- Done-gates: CI ✅ | accept-tests ✅ | criteria 4/5 | threads ❌ | scope ✅
- Watchdog: age 24m / idle 4m (caps 2h / 30m)
- Next: Builder addresses 3 threads
```

---

## Setup (once per repo)

```bash
gh label create drl:building          -c '#cccccc' -d 'DRL: builder implementing'        || true
gh label create drl:needs-review      -c '#0e8a16' -d 'DRL: ready for reviewer'           || true
gh label create drl:changes-requested -c '#d93f0b' -d 'DRL: findings posted, builder turn'|| true
gh label create drl:converged         -c '#5319e7' -d 'DRL: done-gates pass, human merge'  || true
gh label create drl:blocked           -c '#b60205' -d 'DRL: guard tripped / needs human'   || true
```

Pi must also **trust this project** the first time it loads `.pi/` (it prompts;
the decision is saved to `~/.pi/agent/trust.json`). Both agents share the same
`/review-loop` command name — Claude's is `.claude/commands/review-loop.md`
(Builder), Pi's is `.pi/prompts/review-loop.md` (Reviewer).

## Per-PR kickoff

1. **Phase 0 (Q&A):** Builder interviews the owner, writes `ACCEPTANCE.md`, commits it.
2. **Phase 1 (build):** Builder implements + writes the acceptance tests, wires them
   into CI, opens the PR with `ACCEPTANCE.md` linked in the body, sets `drl:building`.
3. **Phase 2 (loop):** Builder runs `/review-loop <PR>` in Claude Code; Reviewer
   runs `/review-loop <PR>` in Pi (the prompt template at `.pi/prompts/review-loop.md`).
   They converge via the state machine above.
4. **Phase 3 (merge):** Human merges on `drl:converged`.

## Useful gh snippets

```bash
gh pr view <PR> --json labels,statusCheckRollup,reviewDecision,reviews
gh pr checks <PR>                                   # CI status
gh pr diff <PR>                                     # the diff under review
gh pr review <PR> --approve | --request-changes -b "…"
gh api repos/{owner}/{repo}/pulls/<PR>/comments     # inline review comments
# resolve a thread (GraphQL):
gh api graphql -f query='mutation($t:ID!){resolveReviewThread(input:{threadId:$t}){thread{isResolved}}}' -f t=<THREAD_ID>
# set the single drl label:
gh pr edit <PR> --remove-label drl:needs-review --add-label drl:changes-requested
```
