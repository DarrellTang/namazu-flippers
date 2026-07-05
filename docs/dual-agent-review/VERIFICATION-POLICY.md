# Verification Policy

How every acceptance criterion is *proven*, so that when the Reviewer sets
`drl:converged` the verdict means something real — not a string-search that a maker
could satisfy without the feature working.

This is the durable companion to `PROTOCOL.md` (the loop state machine) and the
per-PR `ACCEPTANCE.md` (the criteria). PROTOCOL says *how the loop runs*; ACCEPTANCE
says *what must be true*; this file says *how we know each one is true*.

Background: the `tests/phaseNN_nyquist.sh` scripts were GSD tooling that "verified"
criteria by grepping source for literal strings. Grepping for `MaxAttempts = 3`
proves someone typed those characters, not that retry works. GSD is being removed, so
that layer is going away. This policy is what replaces it.

---

## The three tiers

Every criterion is proven one of three ways:

| Tier | How "done" is proven | Runs | Right for |
|------|----------------------|------|-----------|
| 🟩 **Test** | An automated test runs the code and checks its behavior | pre-merge, in CI | logic that matters and can be reached without Dalamud |
| 🟨 **Diff-read** | The Reviewer agent confirms in the diff that a safeguard wasn't deleted | pre-merge | low-stakes "still wired" / "didn't regress" checks |
| 🟦 **Smoke** | A human runs the published plugin in-game and looks | **post-merge** | anything visual — layout, widgets, display |

### The decision rule (how a criterion earns its tier)

Two questions, in order:

1. **How much would it hurt if this silently broke?** High-stakes logic (money math,
   retry, data versioning) earns a 🟩 Test. Low-stakes plumbing earns a 🟨 Diff-read.
   Purely visual output earns a 🟦 Smoke. *This is an intent call — it belongs to the
   owner, not the agents.*
2. **Can the logic be reached without Dalamud?** The test project
   (`NamazuFlippers.Tests`) deliberately compiles only Dalamud-free files, so it runs
   anywhere. A criterion can become a 🟩 Test only if its logic lives in — or can be
   extracted into — a Dalamud-free file. UI rendering (ImGui), disk I/O, and live
   network can't, so they fall back to 🟨 or 🟦. Widening this Dalamud-free surface is
   how more of the plugin becomes machine-verifiable over time.

---

## The publishing constraint (why visuals are post-merge)

There is **no local dev-build path**: macOS can't compile the plugin, and there is no
way to load an unmerged branch into the game. A build reaches the game only by:

```
merge to main → CI bumps the version + publishes → Dalamud pulls the new version → run it
```

**Merging is how a build becomes runnable.** So no in-game check can happen *before*
merge. The loop therefore **converges and merges without any pre-merge "run the
plugin" step** — that gate is unsatisfiable and must never be wired in. Visual
criteria are verified *after* merge, as a smoke test, with fix-forward if wrong.

Consequence to stay honest about: **`drl:converged` means "everything a machine or the
Reviewer can check is green." It does not mean the visuals were seen.** Those ship on
the strength of the tests + the diff-read, and get confirmed post-publish. This is
acceptable here because the blast radius is a personal tool and rollback is cheap
(revert or fix → CI republishes → re-update in-game). Keep that rollback path fast; it
is the entire safety net for visual regressions.

```
maker → PR → [ 🟩 tests green + 🟨 reviewer diff-read ] → owner merges
                                                             │
                                                  CI publishes new version
                                                             │
                                            owner runs it in-game (🟦 smoke test)
                                                             │
                                        looks wrong? → new issue → next loop fixes it
```

---

## Current criterion → tier map

Traceable to the two acceptance contracts. `A0` = `ACCEPTANCE-01-profit-per-gil.md`,
`A1` = `ACCEPTANCE.md` (Holding Window + retry).

| Source | Criterion | Tier | How it's proven | Status |
|--------|-----------|------|-----------------|--------|
| A0-1 | Capital-efficiency ranking | 🟩 Test | `NamazuFlippers.Tests` (pure math) | ✅ built |
| A0-3 | Sell Confidence | 🟩 Test | `NamazuFlippers.Tests` | ✅ built |
| A0-4 | Price Confidence | 🟩 Test | `NamazuFlippers.Tests` | ✅ built |
| A0-5 | Absorption cap | 🟩 Test | `NamazuFlippers.Tests` | ✅ built |
| A0-6 | Kelly sizing | 🟩 Test | `NamazuFlippers.Tests` | ✅ built |
| A0-8 | Graceful degradation (math paths) | 🟩 Test | `NamazuFlippers.Tests` | ✅ built |
| A1-3/4 | Universalis retry + degradation | 🟩 Test | **new** — needs an extract-and-inject refactor | 🔨 to build |
| A1-2 | Config persistence round-trip | 🟩 Test | **new** — extract save/restore to a pure helper | 🔨 to build |
| A0-10 | Config default values | 🟩 Test | **new** | 🔨 to build |
| A0-11 | Cache versioning / stale rejection | 🟩 Test | **new** | 🔨 to build |
| A0-2 | Profit floors still applied | 🟨 Diff-read | Reviewer confirms filters present in diff | ✍️ reviewer |
| A0-7 | Enrichment respects the on/off toggle | 🟨 Diff-read | Reviewer confirms gating present in diff | ✍️ reviewer |
| A0-12 | Route builder groups items correctly | 🟨 Diff-read | Reviewer confirms grouping present in diff | ✍️ reviewer |
| A1-1 | Holding Window slider looks right | 🟦 Smoke | owner, post-merge | 👤 owner |
| A0-9 | Route window display | 🟦 Smoke | owner, post-merge | 👤 owner |
| A1-5 | No sizing regression | 🟩 Test | (covered by the math tests above) | ✅ built |

**What `converged` now rests on:** 10 automated tests green in CI + 3 plumbing items
the Reviewer confirms in the diff + 2 visual items the owner smoke-tests after publish.
No layer left that a maker can pass without doing the real work.

---

## Post-merge smoke checklist (the owner's part)

After CI publishes and the plugin updates in-game, before considering a UI-touching
change truly done:

- [ ] **Holding Window slider** — appears in settings, drags across 1–30, tooltip
      reads correctly.
- [ ] **Route window** — shows a recommended quantity per item and the one-line
      "gil deployed vs budget vs absorption ceiling" summary.

If either is wrong: open a follow-up issue and fix-forward in the next loop.

---

## Build backlog: the 4 new tests

Each is maker (agent) work. Two need a small refactor first to move logic across the
Dalamud boundary so a Dalamud-free test can reach it. The owner's only job on these is
to confirm each test **passes for the right reason** (the two-diff check below).

1. **Retry + degradation** — make the network client's HTTP handler injectable (it is
   currently a `static readonly HttpClient`), then test: `500, 500, 200` ⇒ 3 attempts
   with backoff; a `4xx` ⇒ not retried; retries exhausted ⇒ empty result, scan never
   throws. *Highest value — replaces the most easily-faked check.*
2. **Config persistence** — extract the snapshot / restore-from / restore-defaults
   field-copy out of the ImGui window into a pure helper, then round-trip it.
   *Immunizes every current and future setting against the "forgot to save it" bug.*
3. **Config defaults** — pin the default values (holding window 7, Kelly 0.5, enable
   Universalis true, price threshold 0.9, min recent sales 3) so a maker can't silently
   change one.
4. **Cache versioning** — prove an old-schema cache envelope is rejected as stale and
   re-scanned, not misread as current.

### Prove each test before trusting it (the two-diff check)

A verifier you haven't tested is a verifier you can't trust. For each new test, confirm
it **passes a real fix and fails a cheat**: feed it one diff that fixes the cause and
one that deletes or weakens the check, and confirm it only goes green on the first. If
the cheat slips through, the test is too loose.

---

## Cleanup when GSD is removed

- Delete `tests/phase09_nyquist.sh` and `tests/phase10_nyquist.sh`, and remove the
  "Run nyquist source validation" step from `.github/workflows/build.yml`.
- Rewrite `ACCEPTANCE.template.md` so "Completion tests" points at this policy's three
  tiers (test / diff-read / smoke), not a `tests/<phase>_nyquist.sh` script.
- Drop the `phaseNN_nyquist.sh` rows from the existing `ACCEPTANCE.md` and
  `ACCEPTANCE-01-*.md` completion tables; the criteria they covered are re-homed in the
  map above.

## Known gap (later)

No cost/time ceiling on the loop yet — `MAX_ROUNDS = 6` caps rounds but not dollars or
wall-clock. Add a budget brake to the runner before any long unattended run.
