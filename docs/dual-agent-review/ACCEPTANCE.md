# ACCEPTANCE — Owned verifier: unit tests replace nyquist source-greps

The contract both agents review against. Implements the build backlog in
`VERIFICATION-POLICY.md` (issue #9): convert the four machine-checkable criteria that GSD's
`phaseNN_nyquist.sh` scripts "verified" by grepping source into real unit tests the project
owns, extracting the minimal Dalamud-free seams required, and retire the two CI-wired nyquist
scripts. Glossary in `/CONTEXT.md`.

## Goal
Make the checker's "green" mean behavior, not text. A grep for `MaxAttempts = 3` passes on a
dead constant; a unit test does not. Each new test is proven with the two-diff cheat check
(green on a real fix, red on a cheat).

## Acceptance criteria

1. **Retry policy is Dalamud-free and behavior-preserving.** The transient-retry loop is
   extracted to `NamazuFlippers/API/TransientHttpRetry.cs` (no Dalamud dependency).
   `UniversalisClient` delegates to it and keeps its existing behavior: retries transient 5xx and
   network/timeout errors with exponential backoff, never retries 4xx, returns null on exhaustion,
   and rethrows only genuine cancellation.
2. **Retry is unit-tested.** `TransientHttpRetryTests` proves: first-success returns the body;
   `500,500,200` ⇒ 3 attempts then body; a 4xx ⇒ 1 attempt then null; persistent 5xx ⇒ null at the
   attempt ceiling; a network error is retried then succeeds; a cancelled token propagates
   `OperationCanceledException`; backoff is applied before each retry, never before the first.
3. **Config settings + snapshot/restore are Dalamud-free.** All settings, their defaults, and the
   `Snapshot`/`RestoreFrom`/`RestoreDefaults` logic move from the ImGui window into a Dalamud-free
   `Configuration` partial (`ConfigurationSettings.cs`); the IPluginConfiguration marker stays in
   `Configuration.cs`. `ConfigWindow` calls the relocated methods; behavior is unchanged.
4. **Config persistence is unit-tested.** `ConfigurationPersistenceTests` round-trips **every**
   setting by reflection (so a future field missed by Snapshot/RestoreFrom fails automatically),
   proves Snapshot deep-copies array settings, and proves RestoreDefaults resets tunables while
   preserving `HomeWorld`.
5. **Config defaults are unit-tested.** `ConfigurationDefaultsTests` pins the locked Tier 1-3
   defaults (7 / 0.5 / true / 0.9 / 3) and the core defaults.
6. **Cache staleness rule is Dalamud-free and unit-tested.** The schema version + "is current"
   rule move to `NamazuFlippers/Data/CacheSchema.cs`; `ScanCacheStore.IsValid` uses it.
   `CacheSchemaTests` proves v3 is current and v0/v1/v2/v4 are rejected as stale (criterion 11).
7. **Verification is re-homed off nyquist.** The two CI-wired scripts (`tests/phase09_nyquist.sh`,
   `tests/phase10_nyquist.sh`) and the "Run nyquist source validation" CI step are removed. The
   plugin still compiles and packages, and `dotnet test` runs all unit tests in CI.

## Completion tests

| Test / check | Covers | Runs in CI? |
|---|---|---|
| `NamazuFlippers.Tests/TransientHttpRetryTests.cs` | 1, 2 | yes — `dotnet test` |
| `NamazuFlippers.Tests/ConfigurationPersistenceTests.cs` | 3, 4 | yes — `dotnet test` |
| `NamazuFlippers.Tests/ConfigurationDefaultsTests.cs` | 5 | yes — `dotnet test` |
| `NamazuFlippers.Tests/CacheSchemaTests.cs` | 6 | yes — `dotnet test` |
| `gh pr checks` build job (`dotnet build` + package) | 1, 3, 6, 7 | yes |

62 tests pass locally (43 prior + 19 new); each new test was verified to go red under a cheat
diff. Full plugin compilation is confirmed by CI (macOS can't compile against Dalamud).

## Verification method
All criteria are Tier 🟩 **Test** per `VERIFICATION-POLICY.md` and verified by `dotnet test` in CI
plus the build job. The Reviewer confirms each test fails a cheat (the two-diff check) and that the
extractions preserve behavior (no logic changed, only relocated).

## Out of scope (follow-up, not blockers)
- Removing the non-CI-wired nyquist scripts (`tests/phase03`–`phase08_nyquist.sh`) — part of the
  broader GSD removal sweep, not this PR.
- The diff-read criteria (profit floors, enrichment gating, routing) and smoke criteria (UI
  rendering) are unchanged in behavior; they are now verified per `VERIFICATION-POLICY.md`
  (reviewer diff-read / owner smoke test), not by a grep. No new tests for them here.
- A loop cost/time budget brake (tracked as the known gap in `VERIFICATION-POLICY.md`).

## Definition of done
The 5 objective gates in `PROTOCOL.md`: CI green · acceptance tests (the four test files above)
green in CI · every criterion satisfied · zero unresolved review threads · scope clean.
