---
status: passed
verified_at: "2026-06-13T06:45:00.000Z"
phase: 07
phase_name: manual-realized-profit-tracking
---

# Phase 7 Verification

## Automated Source Validation

| Check | Result |
| ----- | ------ |
| `bash tests/phase03_nyquist.sh` | Passed |
| `bash tests/phase04_nyquist.sh` | Passed |
| `bash tests/phase05_nyquist.sh` | Passed |
| `bash tests/phase06_nyquist.sh` | Passed |
| `bash tests/phase07_nyquist.sh` | Passed |

## Requirement Coverage

| Requirement | Verification |
| ----------- | ------------ |
| PROFIT-01 | Positions UI exposes a manual sold-entry action for open bought lots. |
| PROFIT-02 | Sold-entry modal captures actual unit sale price and quantity. |
| PROFIT-03 | Ledger stores actual sale price, net sale price, buy price, realized unit profit, and total realized profit using FFXIV market tax math. |
| PROFIT-04 | Sale records remain attached to the original position, preserving buy timestamp, route session, source world, and bought-lot trace. |

## Local Build Note

`dotnet build NamazuFlippers/NamazuFlippers.csproj --no-restore` was run as a best-effort compile check and failed because local Dalamud assemblies are not resolved in this macOS workspace. The failure starts with missing references such as `Dalamud`, `Dalamud.Bindings.ImGui`, `FFXIVClientStructs`, and `Lumina`. This matches the documented project build policy; GitHub Actions remains the authoritative compile/package verification gate.

## Human UAT

Not yet performed in-game. Manual UAT should confirm:

- Open Positions window shows the new Sold action.
- Sale modal is fast enough after checking retainer sales.
- Partial quantity sold leaves the position open with the expected remaining quantity.
- Full quantity sold removes the lot from open positions.
- Realized profit preview matches the persisted sale result.
