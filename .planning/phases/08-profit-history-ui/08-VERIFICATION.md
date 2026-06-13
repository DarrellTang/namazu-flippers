---
status: passed
verified_at: "2026-06-13T07:20:00.000Z"
phase: 08
phase_name: profit-history-ui
---

# Phase 8 Verification

## Automated Source Validation

| Check | Result |
| ----- | ------ |
| `bash tests/phase03_nyquist.sh` | Passed |
| `bash tests/phase04_nyquist.sh` | Passed |
| `bash tests/phase05_nyquist.sh` | Passed |
| `bash tests/phase06_nyquist.sh` | Passed |
| `bash tests/phase07_nyquist.sh` | Passed |
| `bash tests/phase08_nyquist.sh` | Passed |

## Requirement Coverage

| Requirement | Verification |
| ----------- | ------------ |
| HIST-01 | Profit History window computes and displays realized profit for today, 7 days, and 30 days from sale timestamps. |
| HIST-02 | Open tab reviews bought/listed positions that still have remaining quantity and projected remaining profit. |
| HIST-03 | Sold tab groups sale records by original buy date from the parent flip position. |
| HIST-04 | UI labels projected open-position values separately from realized sale values. |

## Local Build Note

`dotnet build NamazuFlippers/NamazuFlippers.csproj --no-restore` was run as a best-effort compile check and failed because local Dalamud assemblies are not resolved in this macOS workspace. The failure starts with missing references such as `Dalamud`, `Dalamud.Bindings.ImGui`, `FFXIVClientStructs`, and `Lumina`. This matches the documented project build policy; GitHub Actions remains the authoritative compile/package verification gate.

## Human UAT

Not yet performed in-game. Manual UAT should confirm:

- History button opens the Profit History window.
- Today, 7-day, and 30-day realized totals match manually recorded sales.
- Open tab shows remaining unsold lots and projected remaining profit.
- Sold tab groups sold records by original buy date.
- History window is readable at the user's Dalamud UI scale.
