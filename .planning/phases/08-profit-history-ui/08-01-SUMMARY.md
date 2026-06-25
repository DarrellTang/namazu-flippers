# Phase 8 Summary 08-01: Profit History UI

**Completed:** 2026-06-13
**Status:** Complete

## Delivered

- Added `ProfitHistoryWindow`, a read-only ledger-backed view.
- Added realized profit totals for today, 7 days, and 30 days.
- Added an Open tab showing current open lots and projected remaining profit.
- Added a Sold tab showing sale records grouped by original buy date.
- Added an all-position ledger snapshot in the plugin while preserving the existing open-position snapshot for correction workflows.
- Added a History button in the daily route summary.
- Added `tests/phase08_nyquist.sh`.

## Product Boundary

Phase 8 is display-only. It does not add sale matching, retainer/gil detection, chat parsing, or any new mutation pathway. The item-level ledger remains the authoritative source of realized profit.

## Validation Results

- Passed: `bash tests/phase03_nyquist.sh`
- Passed: `bash tests/phase04_nyquist.sh`
- Passed: `bash tests/phase05_nyquist.sh`
- Passed: `bash tests/phase06_nyquist.sh`
- Passed: `bash tests/phase07_nyquist.sh`
- Passed: `bash tests/phase08_nyquist.sh`
- Expected local limitation: `dotnet build NamazuFlippers/NamazuFlippers.csproj --no-restore` fails because this workspace has no resolved Dalamud SDK assemblies (`Dalamud`, ImGui bindings, Lumina, etc.). CI remains the authoritative compile/package gate.

## Next

Phase 9 should be a live-runtime observability spike. Do not implement assisted reconciliation until there is evidence for which Dalamud/game signals are reliable enough.
