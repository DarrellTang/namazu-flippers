---
phase: 03
slug: scan-engine-route-optimizer
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-07
---

# Phase 03 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | .NET test project if added by executor; otherwise `dotnet build` plus grep/file checks |
| **Config file** | `NamazuFlippers/NamazuFlippers.csproj` |
| **Quick run command** | `dotnet build NamazuFlippers/NamazuFlippers.csproj` |
| **Full suite command** | `dotnet build NamazuFlippers/NamazuFlippers.csproj` plus any added test project command |
| **Estimated runtime** | ~30 seconds |

## Sampling Rate

- **After every task commit:** Run `dotnet build NamazuFlippers/NamazuFlippers.csproj`
- **After every plan wave:** Run full build plus any added tests
- **Before `$gsd-verify-work`:** Build and tests must be green
- **Max feedback latency:** 60 seconds

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 03-01 | 1 | SCAN-01 | T-03-01 | Invalid/partial API rows are rejected before ranking | unit/build | `dotnet build NamazuFlippers/NamazuFlippers.csproj` | pending | pending |
| 03-01-02 | 03-01 | 1 | SCAN-01 | T-03-02 | API failures return structured errors, not null/raw exceptions | unit/build | `dotnet build NamazuFlippers/NamazuFlippers.csproj` | pending | pending |
| 03-02-01 | 03-02 | 2 | SCAN-02 | T-03-03 | Route grouping cannot create unknown/empty purchase stops | unit/build | `dotnet build NamazuFlippers/NamazuFlippers.csproj` | pending | pending |
| 03-02-02 | 03-02 | 2 | SCAN-02 | T-03-04 | Same-DC preference only applies inside the 20% value window | unit/build | `dotnet build NamazuFlippers/NamazuFlippers.csproj` | pending | pending |
| 03-02-03 | 03-02 | 2 | SCAN-03 | T-03-05 | Cache is invalidated by age, schema version, and config fingerprint | unit/build | `dotnet build NamazuFlippers/NamazuFlippers.csproj` | pending | pending |
| 03-02-04 | 03-02 | 2 | SCAN-04 | T-03-06 | Manual rescan bypasses cache and ignores duplicate concurrent scans | unit/build | `dotnet build NamazuFlippers/NamazuFlippers.csproj` | pending | pending |

## Wave 0 Requirements

- Existing infrastructure is enough for build verification.
- If adding tests is feasible, create a small domain test project for ranking, routing, and cache validity before or during 03-01.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Character-login auto-scan | SCAN-03 | Requires Dalamud runtime and logged-in character | Install plugin, configure home world, log into a character, verify scan starts after login and skips title/character select |
| Manual `/nflip scan` | SCAN-04 | Requires Dalamud command runtime | Run `/nflip scan`, verify concise status logs and cache bypass behavior |
| Stale-cache fallback on API failure | SCAN-03 | Requires inducing API failure or blocked network | Create valid cache, make refresh fail, verify stale route is retained and marked stale |

## Validation Sign-Off

- [x] All tasks have automated build verification or explicit manual runtime verification
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 does not need new infrastructure before planning
- [x] No watch-mode flags
- [x] Feedback latency target < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
