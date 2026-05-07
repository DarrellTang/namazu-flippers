---
phase: 03
slug: scan-engine-route-optimizer
status: verified
threats_open: 0
asvs_level: 1
created: 2026-05-07
---

# Phase 03 - Security

Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Saddlebag API -> API client | Remote `/api/scan` JSON is parsed into typed scan response models. | External market data; untrusted JSON shape and values. |
| API client -> scan engine | Raw scan rows are filtered, ranked, and converted into route opportunities. | Item IDs, names, worlds/vendor sources, prices, sales velocity, expected profit. |
| Scan engine -> route/cache layer | Ranked opportunities become route stops and cached derived results. | Derived route, raw scan response, config fingerprint, cache timestamps. |
| Dalamud runtime -> scan orchestration | Commands and client login events trigger manual or automatic scans. | User commands, login state, cancellation/disposal signals, latest route state. |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-03-01 | Tampering / Data Integrity | `ScanEngine` row filtering | mitigate | Filter rows with non-positive item IDs/prices/profit, blank names, blank purchase source, or non-positive sales velocity before ranking. | closed |
| T-03-02 | Information Disclosure / Error Handling | `SaddlebagClient`, `ScanEngineResult` | mitigate | Normalize supported response shapes, convert API/JSON failures to structured `ScanEngineResult.Error`, and keep technical details separate from friendly user messages. | closed |
| T-03-03 | Tampering / Data Integrity | `ScanEngine` ranking | mitigate | Always sort locally by expected daily profit descending, sales per day descending, then cheapest price ascending before capping. | closed |
| T-03-04 | Business Logic Integrity | `RouteOptimizer` stop ordering | mitigate | Order route stops by total expected value and only apply travel friction inside the 20 percent tie-break window. | closed |
| T-03-05 | Tampering / Cache Integrity | `ScanCacheStore` | mitigate | Cache validity requires schema version match, unexpired timestamp, and matching scan-affecting config fingerprint; cache is stored under the plugin config directory. | closed |
| T-03-06 | Race Condition / State Integrity | `NamazuFlippers` scan orchestration | mitigate | Manual and automatic scans share an `Interlocked` duplicate-scan guard, latest-result assignment, cancellation token, and disposal cleanup. | closed |
| T-03-07 | Availability / Runtime Readiness | `NamazuFlippers` login auto-scan | mitigate | Auto-scan is triggered from `IClientState.Login` or startup `IsLoggedIn`, delayed after login, and skipped when required home-world config is missing. | closed |

*Status: open - closed*
*Disposition: mitigate (implementation required) - accept (documented risk) - transfer (third-party)*

---

## Verification Evidence

| Threat ID | Evidence |
|-----------|----------|
| T-03-01 | `ScanEngine.IsUsable` rejects invalid item IDs, blank names/source, non-positive prices/profit, and non-positive sales velocity before mapping opportunities (`NamazuFlippers/Core/ScanEngine.cs`). |
| T-03-02 | `SaddlebagClient.NormalizeScanResponse` accepts root arrays and common wrappers while throwing `ApiException` for empty, invalid, or unsupported shapes; `ScanEngine` catches `ApiException` and returns structured `Error` with `TechnicalDetails` separated from `UserMessage`. |
| T-03-03 | `ScanEngine.ScanFreshCoreAsync` performs local `OrderByDescending(ExpectedDailyProfit)`, `ThenByDescending(SalesPerDay)`, and `ThenBy(CheapestPrice)` before applying `MaxItemsPerSession`. |
| T-03-04 | `RouteOptimizer.RouteStopComparer` compares total expected profit and only lets lower travel friction decide when values are within `FrictionTieBreakWindow = 0.20`. |
| T-03-05 | `ScanCacheStore.IsValid` requires `CurrentSchemaVersion`, future `ExpiresAtUtc`, and exact `ConfigFingerprint`; `CreateConfigFingerprint` covers home world, scan thresholds, route caps, vendor/out-of-stock flags, region-wide mode, and category filters. |
| T-03-06 | `NamazuFlippers.RunScanAsync` uses `Interlocked.Exchange(ref scanInProgress, 1)`, resets in `finally`, stores `LatestScanResult`, and cancellation is wired through `scanCts` disposal. |
| T-03-07 | `clientState.Login += OnLogin`, startup `clientState.IsLoggedIn`, delayed `QueueAutoScan`, and the home-world guard in `RunScanAsync` prevent title-screen/config-incomplete scans from failing noisily. |

## Summary Threat Flags

No standalone `## Threat Flags` section was present in `03-01-SUMMARY.md` or `03-02-SUMMARY.md`. The security register was sourced from the `threat_model` blocks in `03-01-PLAN.md` and `03-02-PLAN.md`.

---

## Accepted Risks Log

No accepted risks.

---

## Security Audit 2026-05-07

| Metric | Count |
|--------|-------|
| Threats found | 7 |
| Closed | 7 |
| Open | 0 |

Verification note: deterministic source evidence checks passed for the mitigation points above. `dotnet build NamazuFlippers/NamazuFlippers.csproj` is not expected to pass in the local macOS workspace unless Dalamud SDK assemblies are configured; GitHub Actions is the authoritative compiler/package verification path because it downloads Dalamud into `DALAMUD_HOME` before building.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-05-07 | 7 | 7 | 0 | Codex |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-05-07
