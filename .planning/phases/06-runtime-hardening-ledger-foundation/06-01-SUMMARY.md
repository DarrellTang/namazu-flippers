# 06-01 Summary: Runtime Hardening & Ledger Foundation

**Status:** Complete
**Branch:** `codex/tracking`
**Draft PR:** #1 — `Implement Phase 6 runtime hardening and ledger foundation`
**Completed:** 2026-06-13

## What Shipped

- Serialized `scan-cache.json` writes through one shared `writeGate` so scan-cache saves and session-state saves cannot interleave through separate write paths.
- Added `MinSalesPerDay` to the scan cache fingerprint so changing the actual velocity filter invalidates stale cache results.
- Removed temporary broad runtime diagnostics from Phase 5 troubleshooting: no global unobserved-task suppression and no periodic draw-heartbeat logging.
- Retained the upstream scoped ImGui alpha self-heal from `main` while resolving conflicts, because it addresses the observed invisible-UI failure without suppressing unrelated exceptions.
- Added structured scan warnings (`ScanWarning`) so stale/failure fallback can surface retry count, failure type, timestamp, user message, and technical details.
- Added independent durable ledger storage in `flip-ledger.json`, separate from `scan-cache.json`, with schema versioning and `.bak` backup-on-write.
- Added bought-lot model fields for item identity, buy timestamp, source world, actual unit buy price, expected sell price, planned unit profit, bought/listed/sold/remaining quantities, status, route trace, and home world.
- Changed bought route actions from session-only checkboxes into confirmation flows that capture quantity and actual unit buy price before creating a durable lot.
- Added a bulk `Mark All Bought` confirmation that creates quantity-1 lots at routed buy prices, with correction available afterward.
- Added `PositionsWindow` for minimal open-position review, quantity/unit-buy correction, and delete-for-mistake behavior.
- Disabled route mutation controls during in-flight scans with stable scan-state snapshots so `BeginDisabled`/`EndDisabled` remain balanced.
- Updated source validation for runtime-discovered API behavior, Phase 5 session semantics after ledger adjustment, and new Phase 6 hardening/ledger behavior.
- Removed tracked TTS audio artifacts from the draft PR and ignored future `.claude/audio/` output.

## Files Added

- `NamazuFlippers/Core/ScanWarning.cs`
- `NamazuFlippers/Data/FlipLedgerEnvelope.cs`
- `NamazuFlippers/Data/FlipLedgerStore.cs`
- `NamazuFlippers/Data/FlipPosition.cs`
- `NamazuFlippers/Data/FlipPositionStatus.cs`
- `NamazuFlippers/UI/PositionsWindow.cs`
- `tests/phase06_nyquist.sh`

## Validation

- `bash tests/phase03_nyquist.sh` — pass
- `bash tests/phase04_nyquist.sh` — pass
- `bash tests/phase05_nyquist.sh` — pass
- `bash tests/phase06_nyquist.sh` — pass
- GitHub Actions `build` on draft PR #1 — pass

Local `dotnet build NamazuFlippers/NamazuFlippers.csproj --no-restore` still fails in this macOS workspace because local Dalamud assemblies are unavailable. This matches the documented build policy; GitHub Actions remains the compile/package gate.

## Deferred To Phase 7

- Manual sold-state workflow.
- Actual sale price entry.
- Partial close/remaining quantity behavior.
- Tax-adjusted realized profit calculation.
- Sold records tied to original buy date/session.

