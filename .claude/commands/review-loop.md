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
        threads, done-gate checklist).
     h. Flip label: remove `drl:changes-requested`, add `drl:needs-review`.

   - **`drl:needs-review` or `drl:building` → not your turn.** The Reviewer is up
     (or initial build is external). Do nothing this tick.

   - **`drl:converged` → done.** Post a final summary comment, `@`-mention the
     repo owner that it's ready to merge, and STOP (do not reschedule).

   - **`drl:blocked` → stop.** Summarize why it's blocked and what decision the
     human must make. STOP (do not reschedule).

3. **Guards (check every tick):**
   - Read the round counter from the status comment. If it would exceed
     **MAX_ROUNDS=6**, set `drl:blocked` + ping human + stop.
   - If open-thread count rose two rounds running, set `drl:blocked` + stop.

4. **Reschedule** (only if not converged/blocked):
   `ScheduleWakeup(delaySeconds≈600, prompt="/review-loop $1")`. Pick the delay by
   how fast Pi reviews — 600s is a sane default; tighten to ~270s if rounds are
   fast (stays inside the prompt-cache window).

## Notes
- Never set `drl:converged` yourself — that is the Reviewer's call.
- Merge is the human's; never `gh pr merge`.
- All coordination is via the PR. Do not assume anything about Pi's state beyond
  what the labels, threads, and status comment say.
