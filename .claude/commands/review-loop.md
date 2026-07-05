---
description: Run the Builder side of the Dual-Agent PR Review Loop (DRL) — poll a PR, address Pi's findings, push, re-request review, until converged or blocked.
argument-hint: <PR-number> [acceptance-doc-path]
allowed-tools: Bash, Read, Edit, Write, Grep, Glob, Skill, ScheduleWakeup
---

You are the **Builder** in the Dual-Agent PR Review Loop. The full contract is
`docs/dual-agent-review/PROTOCOL.md` — read it now if not already in context, plus
the PR's `ACCEPTANCE.md` (arg 2, default: the `ACCEPTANCE.md` on the PR branch).

PR under loop: **$1**

This command runs **one tick** of the Builder loop, then self-schedules the next
tick via `ScheduleWakeup` (re-firing this same `/review-loop $1` prompt) until a
terminal state. Do not block-wait between ticks.

## One tick

1. **Read state:**
   `gh pr view $1 --json labels,statusCheckRollup,reviewDecision,url`
   Determine the single `drl:*` label.

2. **Branch on the label:**

   - **`drl:changes-requested` → your turn.** Do all of:
     a. List unresolved review threads + comments:
        `gh api repos/{owner}/{repo}/pulls/$1/comments` and the GraphQL review
        threads query (see PROTOCOL.md). Read each finding.
     b. For each finding, EITHER fix it in code (+ adjust/extend the acceptance
        tests named in `ACCEPTANCE.md`) and reply in-thread noting the fix,
        OR decline **once** with a clear rationale in-thread. If a declined
        finding is re-asserted by the Reviewer, set `drl:blocked` and stop (do
        not thrash — see the disagreement guard).
     c. Stay within `ACCEPTANCE.md` scope. New ideas → `gh issue create`, never
        new scope here.
     d. Commit (atomic, one concern per commit) and push.
     e. Confirm CI: invoke the `watch-ci-run` skill for this PR's branch; if the
        `build` job fails, fix and re-push until green.
     f. Resolve the threads you fixed (GraphQL `resolveReviewThread`).
     g. Update the `<!-- DRL-STATUS -->` comment (round, actor=Builder, CI, open
        threads, done-gate checklist, watchdog age/idle vs the 2h/30m caps).
     h. Flip label: remove `drl:changes-requested`, add `drl:needs-review`.

   - **`drl:needs-review` or `drl:building` → not your turn.** The Reviewer is up
     (or initial build is external). Do nothing this tick *except run the step-3
     guards* — this is exactly the state where the loop waits on a human, so the
     watchdog is meant to fire here.

   - **`drl:converged` → done.** Post a final summary comment, `@`-mention the
     repo owner that it's ready to merge, and STOP (do not reschedule).

   - **`drl:blocked` → stop.** Summarize why it's blocked and what decision the
     human must make. STOP (do not reschedule).

3. **Guards (check every tick, in every label state):**
   - **Round cap.** Read the round counter from the status comment. If it would
     exceed **MAX_ROUNDS=6**, set `drl:blocked` + ping human + stop.
   - **Monotonic progress.** If open-thread count rose two rounds running, set
     `drl:blocked` + stop.
   - **Watchdog — the budget brake.** Both agents run on subscriptions, so there is
     no dollar cost to cap; the scarce things are wall-clock and your attention. Time
     the loop from the PR's own `drl:*` label events (no custom state to keep):
     ```bash
     gh api repos/{owner}/{repo}/issues/$1/timeline --paginate \
       --jq '.[] | select(.event=="labeled" or .event=="unlabeled")
                 | select(.label.name|startswith("drl:")) | .created_at'
     ```
     Let `age = now − earliest drl:* event` and `idle = now − most recent drl:*
     change` (`now = date -u +%s`). If **`age > 2h`** or **`idle > 30m`**, the loop
     has stalled on a human step (usually: Pi review never ran, or a converged PR
     wasn't merged). Set `drl:blocked`, post a comment `@`-mentioning the owner that
     names the stuck state and the concrete action — e.g. *"`needs-review` for 32m
     with no review: run `/review-loop <PR>` in Pi, or merge if you've already looked"*
     — update the status comment, and **STOP (do not reschedule).**

4. **Reschedule** (only if not converged/blocked/watchdog-tripped):
   `ScheduleWakeup(delaySeconds≈300, prompt="/review-loop $1")`. Default cadence is
   **5 minutes (300s)**. Drop to ~270s if you want to stay inside the prompt-cache
   window; raise toward 600s only if Pi reviews are consistently slow. At 300s the
   30m idle cap is ~6 ticks and the 2h age cap is ~24 ticks.

## Notes
- Never set `drl:converged` yourself — that is the Reviewer's call.
- Merge is the human's; never `gh pr merge`.
- All coordination is via the PR. Do not assume anything about Pi's state beyond
  what the labels, threads, and status comment say.
