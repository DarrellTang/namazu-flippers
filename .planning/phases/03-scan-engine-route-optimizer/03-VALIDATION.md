---
phase: 03
slug: scan-engine-route-optimizer
status: verified
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-07
updated: 2026-05-07
---

# Phase 03 - Validation Strategy

Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Bash source-level Nyquist validation plus GitHub Actions compile/package build |
| **Config file** | `tests/phase03_nyquist.sh`; `NamazuFlippers/NamazuFlippers.csproj` |
| **Quick run command** | `bash tests/phase03_nyquist.sh` |
| **Full suite command** | `bash tests/phase03_nyquist.sh` locally; GitHub Actions build for compiler/package verification |
| **Estimated runtime** | ~1 second for source validation; CI build/package runtime varies |

---

## Sampling Rate

- **After every task commit:** Run `bash tests/phase03_nyquist.sh`
- **After every plan wave:** Run `bash tests/phase03_nyquist.sh`; check GitHub Actions once pushed
- **Before `$gsd-verify-work`:** Run source validation locally and confirm GitHub Actions build/package result
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 03-01 | 1 | SCAN-01 | T-03-02 | `/api/scan` response shape is normalized into `ScanResponse.Items` without relying on one wrapper key | source | `bash tests/phase03_nyquist.sh` | yes | green |
| 03-01-02 | 03-01 | 1 | SCAN-01 | T-03-02 | Structured `ScanEngineResult` models expose success, empty, error, cache, stale-cache, user message, and technical details | source | `bash tests/phase03_nyquist.sh` | yes | green |
| 03-01-03 | 03-01 | 1 | SCAN-01 | T-03-01 / T-03-03 | Invalid scan rows are filtered before deterministic local ranking and session capping | source | `bash tests/phase03_nyquist.sh` | yes | green |
| 03-02-01 | 03-02 | 2 | SCAN-02 | T-03-04 | World/data-center helpers provide known-world lookup and route friction values | source | `bash tests/phase03_nyquist.sh` | yes | green |
| 03-02-02 | 03-02 | 2 | SCAN-02 | T-03-04 | Route stops group by purchase source, preserve vendor stops, and apply friction only inside the 20 percent value window | source | `bash tests/phase03_nyquist.sh` | yes | green |
| 03-02-03 | 03-02 | 2 | SCAN-03 | T-03-05 | Cache envelope and store validate schema, expiry, and scan-affecting config fingerprint under plugin-local storage | source | `bash tests/phase03_nyquist.sh` | yes | green |
| 03-02-04 | 03-02 | 2 | SCAN-03 | T-03-05 | `ScanEngine.GetRouteAsync(false)` reuses valid cache and marks stale fallback separately after refresh failure | source | `bash tests/phase03_nyquist.sh` | yes | green |
| 03-02-05 | 03-02 | 2 | SCAN-04 | T-03-06 / T-03-07 | `/nflip scan` bypasses cache, duplicate scans are ignored, and login/startup auto-scan uses cached route behavior | source | `bash tests/phase03_nyquist.sh` | yes | green |

---

## Wave 0 Requirements

Existing repo infrastructure now covers Phase 3 source-level validation:

- [x] `tests/phase03_nyquist.sh` - automated checks for SCAN-01 through SCAN-04
- [x] No package install required
- [x] No watch-mode command required

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Build/plugin load in Dalamud environment | SCAN-01 / SCAN-02 / SCAN-03 / SCAN-04 | Local macOS workspace lacks Dalamud SDK assemblies, so `dotnet build` cannot complete here | Use GitHub Actions as the authoritative compile/package check; for in-game local testing, install the CI release artifact or build from a configured Windows/Dalamud dev environment |
| Character-login auto-scan | SCAN-03 | Requires Dalamud runtime and logged-in character state | Install plugin, configure home world, log into a character, and verify scan starts after login/startup while skipping title screen and missing home-world setup |
| Manual `/nflip scan` | SCAN-04 | Requires Dalamud command runtime | Run `/nflip scan`, verify it bypasses cache, logs concise status, and ignores duplicate concurrent scans |
| Stale-cache fallback on API failure | SCAN-03 | Requires saved plugin cache plus induced API/network failure | Create valid cache, force a refresh failure, and verify stale route is retained and marked `UsingStaleCache` |

Manual-only rows are runtime/UAT checks. They do not block Nyquist source compliance because each phase requirement now has automated source-level coverage.

---

## Validation Audit 2026-05-07

| Metric | Count |
|--------|-------|
| Gaps found | 8 |
| Resolved | 8 |
| Escalated | 0 |

Automated command result:

```bash
bash tests/phase03_nyquist.sh
```

Result: passed.

Build status:

```bash
dotnet build NamazuFlippers/NamazuFlippers.csproj
```

Result: blocked in this local macOS environment because Dalamud assemblies are not available (`Dalamud.NET.Sdk: root at /`, followed by missing `Dalamud` references). This is expected for the developer workspace. GitHub Actions is the authoritative compile/package verification path because it downloads the Dalamud SDK before building.

---

## Validation Sign-Off

- [x] All tasks have automated source validation or explicit manual runtime verification
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all missing references
- [x] No watch-mode flags
- [x] Feedback latency target < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-05-07
