#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

failures=0

pass() {
  printf 'ok - %s\n' "$1"
}

fail() {
  printf 'not ok - %s\n' "$1" >&2
  failures=$((failures + 1))
}

require_file() {
  local file="$1"
  if [[ -f "$file" ]]; then
    pass "$file exists"
  else
    fail "$file exists"
  fi
}

require_pattern() {
  local file="$1"
  local pattern="$2"
  local label="$3"

  if grep -Eq "$pattern" "$file"; then
    pass "$label"
  else
    fail "$label"
  fi
}

require_absent_pattern() {
  local file="$1"
  local pattern="$2"
  local label="$3"

  if grep -Eq "$pattern" "$file"; then
    fail "$label"
  else
    pass "$label"
  fi
}

require_all_patterns() {
  local file="$1"
  local label="$2"
  shift 2

  local missing=()
  local pattern
  for pattern in "$@"; do
    if ! grep -Eq "$pattern" "$file"; then
      missing+=("$pattern")
    fi
  done

  if [[ "${#missing[@]}" -eq 0 ]]; then
    pass "$label"
  else
    fail "$label (missing: ${missing[*]})"
  fi
}

require_count_at_least() {
  local file="$1"
  local pattern="$2"
  local min="$3"
  local label="$4"
  local count

  count="$(grep -Ec "$pattern" "$file" || true)"
  if [[ "$count" -ge "$min" ]]; then
    pass "$label"
  else
    fail "$label (found $count, expected at least $min)"
  fi
}

echo "Phase 06 Nyquist validation"

require_file "NamazuFlippers/Data/ScanCacheStore.cs"
require_file "NamazuFlippers/Data/FlipPosition.cs"
require_file "NamazuFlippers/Data/FlipPositionStatus.cs"
require_file "NamazuFlippers/Data/FlipLedgerEnvelope.cs"
require_file "NamazuFlippers/Data/FlipLedgerStore.cs"
require_file "NamazuFlippers/Core/ScanWarning.cs"
require_file "NamazuFlippers/UI/PositionsWindow.cs"
require_file "NamazuFlippers/UI/DailyRouteWindow.cs"
require_file "NamazuFlippers/NamazuFlippers.cs"
require_file "NamazuFlippers/API/Models/ApiJsonContext.cs"

echo
echo "HARD-01/HARD-02: serialized cache writes and complete fingerprint"
require_all_patterns "NamazuFlippers/Data/ScanCacheStore.cs" "scan and session saves share one write gate and atomic writer" \
  "SemaphoreSlim writeGate" \
  "public async Task SaveAsync" \
  "public async Task SaveSessionAsync" \
  "WriteEnvelopeAsync\(envelope, ct\)" \
  "cachePath \+ \"\\.tmp\"" \
  "File\\.Move\(tempPath, cachePath, overwrite: true\)"
require_count_at_least "NamazuFlippers/Data/ScanCacheStore.cs" "await writeGate\\.WaitAsync\\(ct\\)" 2 "both SaveAsync and SaveSessionAsync acquire writeGate"
require_count_at_least "NamazuFlippers/Data/ScanCacheStore.cs" "writeGate\\.Release\\(\\)" 2 "both SaveAsync and SaveSessionAsync release writeGate"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "configuration\\.MinSalesPerDay" "MinSalesPerDay participates in cache fingerprint"
require_absent_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "sessionSaveLock" "old session-only write gate removed"

echo
echo "HARD-02/HARD-03: deterministic in-flight UI behavior and release diagnostics"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "route mutating controls are disabled during in-flight scans" \
  "plugin\\.ScanInProgress \\|\\| totalItems == 0" \
  "ImGui\\.BeginDisabled\\(\\)" \
  "OpenMarkBoughtConfirmation" \
  "ConfirmBoughtLot##daily" \
  "ConfirmBulkBoughtLots##daily"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "scan state remains explicit and visible to UI" \
  "public bool ScanInProgress" \
  "Interlocked\\.Exchange\\(ref scanInProgress, 1\\) == 1" \
  "Interlocked\\.Exchange\\(ref scanInProgress, 0\\)"
require_absent_pattern "NamazuFlippers/NamazuFlippers.cs" "SetObserved|UnobservedTaskException|UnhandledException|DrawHeartbeat|Draw heartbeat" \
  "release code does not globally suppress exceptions or emit draw heartbeats"

echo
echo "LEDGER-01..03: independent durable bought-lot ledger"
require_all_patterns "NamazuFlippers/Data/FlipLedgerStore.cs" "ledger persists independently from scan cache with backup-on-write" \
  "flip-ledger\\.json" \
  "backupPath = ledgerPath \\+ \"\\.bak\"" \
  "File\\.Copy\\(ledgerPath, backupPath, overwrite: true\\)" \
  "JsonSerializer\\.SerializeAsync" \
  "File\\.Move\\(tempPath, ledgerPath, overwrite: true\\)"
require_all_patterns "NamazuFlippers/Data/FlipLedgerEnvelope.cs" "ledger envelope is schema-versioned and stores positions" \
  "CurrentSchemaVersion = 1" \
  "UpdatedAtUtc" \
  "List<FlipPosition> Positions"
require_all_patterns "NamazuFlippers/Data/FlipPosition.cs" "bought-lot model preserves buy date, quantity lifecycle, and route trace" \
  "string Id" \
  "int ItemId" \
  "string ItemName" \
  "DateTimeOffset BuyTimestampUtc" \
  "string SourceWorld" \
  "int ActualUnitBuyPrice" \
  "int ExpectedUnitSellPrice" \
  "int PlannedUnitProfit" \
  "int BoughtQuantity" \
  "int ListedQuantity" \
  "int SoldQuantity" \
  "int RemainingQuantity" \
  "FlipPositionStatus Status" \
  "DateTimeOffset RouteCreatedAtUtc" \
  "string RouteSessionId" \
  "string HomeWorld"
require_all_patterns "NamazuFlippers/Data/FlipLedgerStore.cs" "multiple positions for the same item id are allowed" \
  "Guid\\.NewGuid\\(\\)\\.ToString\\(\"N\"\\)" \
  "envelope\\.Positions\\.Add\\(position\\)"

echo
echo "Bought workflow and correction UI"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "route row bought action confirms quantity and actual unit buy price" \
  "OpenMarkBoughtConfirmation" \
  "ImGui\\.InputInt\\(\"Qty\"" \
  "ImGui\\.InputInt\\(\"Unit buy\"" \
  "Save Lot" \
  "plugin\\.QueueBoughtLotSave" \
  "boughtState\\[pendingBuyItem\\.ItemId\\] = true"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "bulk bought path confirms before creating default lots" \
  "ConfirmBulkBoughtLots##daily" \
  "Create .* bought lots at quantity 1" \
  "Save Lots" \
  "actualUnitBuyPrice: Math\\.Max\\(1, item\\.PurchasePrice\\)"
require_all_patterns "NamazuFlippers/UI/PositionsWindow.cs" "open positions window supports correction and deletion" \
  "public sealed class PositionsWindow" \
  "plugin\\.OpenPositions" \
  "ImGui\\.InputInt\\(\"Qty\"" \
  "ImGui\\.InputInt\\(\"Unit buy\"" \
  "plugin\\.QueueOpenPositionCorrection" \
  "plugin\\.QueueOpenPositionDelete" \
  "DeletePosition##positions"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "plugin owns ledger store and exposes narrow UI methods" \
  "FlipLedgerStore ledgerStore" \
  "public IReadOnlyList<FlipPosition> OpenPositions" \
  "public void QueueBoughtLotSave" \
  "public void QueueOpenPositionCorrection" \
  "public void QueueOpenPositionDelete" \
  "windowSystem\\.AddWindow\\(positionsWindow\\)"

echo
echo "Structured warnings and JSON registration"
require_all_patterns "NamazuFlippers/Core/ScanWarning.cs" "structured scan warning fields exist" \
  "FailureType" \
  "AffectedItemName" \
  "AffectedWorld" \
  "TimestampUtc" \
  "RetryCount" \
  "UserMessage" \
  "TechnicalDetails"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "scan failures surface structured warning detail" \
  "ApiRetryCount = 3" \
  "RefreshFailedStaleCache" \
  "ApiException" \
  "UnexpectedException" \
  "Warnings ="
require_pattern "NamazuFlippers/API/SaddlebagClient.cs" "MaxRetries = 3" "HTTP scan has bounded retry count"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "warnings render inline with tooltip details" \
  "result\\.Warnings\\.Count > 0" \
  "Warning:" \
  "FailureType" \
  "RetryCount" \
  "TechnicalDetails"
require_all_patterns "NamazuFlippers/API/Models/ApiJsonContext.cs" "new Phase 6 types are registered for JSON source generation" \
  "JsonSerializable\\(typeof\\(ScanWarning\\)\\)" \
  "JsonSerializable\\(typeof\\(List<ScanWarning>\\)\\)" \
  "JsonSerializable\\(typeof\\(FlipPosition\\)\\)" \
  "JsonSerializable\\(typeof\\(FlipPositionStatus\\)\\)" \
  "JsonSerializable\\(typeof\\(FlipLedgerEnvelope\\)\\)" \
  "JsonSerializable\\(typeof\\(List<FlipPosition>\\)\\)"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 06 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 06 Nyquist validation passed.\n'
