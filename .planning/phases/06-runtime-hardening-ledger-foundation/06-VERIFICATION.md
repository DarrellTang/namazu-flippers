# Phase 6 Verification

**Verified:** 2026-06-13
**Scope:** Runtime hardening, durable bought-lot ledger foundation, source validation, CI build.

## Automated Source Validation

All local source-validation scripts passed after merging latest `origin/main`:

```text
bash tests/phase03_nyquist.sh
bash tests/phase04_nyquist.sh
bash tests/phase05_nyquist.sh
bash tests/phase06_nyquist.sh
```

Coverage highlights:

- `SaveAsync` and `SaveSessionAsync` share `ScanCacheStore.writeGate` and a common atomic write helper.
- `MinSalesPerDay` is included in `CreateConfigFingerprint`.
- Route mutation controls are disabled during scans, with stable scan-state snapshots for disabled scopes.
- Global exception suppression and draw heartbeat logging are absent.
- Ledger storage uses independent `flip-ledger.json`, schema versioning, and backup-on-write.
- Bought-lot schema supports duplicate item ids and quantity lifecycle fields.
- Mark-bought confirms quantity and actual unit buy price before creating a durable lot.
- Open positions view supports quantity/unit-buy correction and deletion.
- Structured scan warnings are serialized and rendered inline.

## CI Verification

Draft PR #1 build passed:

- Workflow: `Build & Release`
- Check: `build`
- Result: pass

## Local Compile Caveat

Local macOS compile remains intentionally unavailable in this workspace:

```text
dotnet build NamazuFlippers/NamazuFlippers.csproj --no-restore
```

The failure is caused by missing local Dalamud assemblies (`Dalamud`, `Dalamud.Bindings.ImGui`, `IPluginLog`, etc.). This is expected per project policy. CI downloads Dalamud into `DALAMUD_HOME` and is the authoritative compile/package gate.

## In-Game UAT Still Recommended

Before merging/releasing the Phase 6 PR, test in Dalamud:

- Route still renders and scans normally.
- Individual bought checkbox opens quantity/unit-buy confirmation.
- Save Lot creates an open position and keeps the route row checked.
- Cancel creates no ledger record.
- Mark All Bought opens confirmation and creates default quantity-1 lots.
- Positions opens, lists open lots, persists across plugin reload, and supports edit/delete.
- Listing checkboxes and session resume still behave as before.

