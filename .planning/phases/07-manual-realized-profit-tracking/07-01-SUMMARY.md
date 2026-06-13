# Phase 7 Summary 07-01: Manual Realized-Profit Tracking

**Completed:** 2026-06-13
**Status:** Complete

## Delivered

- Added `FlipSale` records for manual sale outcomes.
- Extended `FlipPosition` with sale history, last sold timestamp, and total realized profit.
- Added ledger write path `RecordSaleAsync` with serialized write-gate usage, backup-on-write persistence, partial-close quantity updates, and status transition to `Sold` only when the lot is fully closed.
- Added plugin-level `QueuePositionSold` API for UI-triggered sale recording.
- Added a `Sold` action in the open positions window with confirmation modal, quantity entry, actual unit sale price entry, and after-tax realized profit preview.
- Registered sale models in `ApiJsonContext`.
- Added `tests/phase07_nyquist.sh` to validate Phase 7 source contracts and guard against premature automatic reconciliation.

## Product Boundary

Phase 7 stayed intentionally manual. It does not observe game runtime sale signals, read retainer/gil state, auto-match items, or silently close positions. Those decisions remain gated by Phase 9 live observability and the end-state automation ceiling.

## Validation Results

- Passed: `bash tests/phase03_nyquist.sh`
- Passed: `bash tests/phase04_nyquist.sh`
- Passed: `bash tests/phase05_nyquist.sh`
- Passed: `bash tests/phase06_nyquist.sh`
- Passed: `bash tests/phase07_nyquist.sh`
- Expected local limitation: `dotnet build NamazuFlippers/NamazuFlippers.csproj --no-restore` fails because this workspace has no resolved Dalamud SDK assemblies (`Dalamud`, ImGui bindings, Lumina, etc.). CI remains the authoritative compile/package gate.

## Next

Phase 8 should build the profit history UI on top of the item-level ledger: today, 7-day, and 30-day totals; open position review; and sold history grouped or filterable by original buy date.
