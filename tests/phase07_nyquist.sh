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

echo "Phase 07 Nyquist validation"

require_file "NamazuFlippers/Data/FlipSale.cs"
require_file "NamazuFlippers/Data/FlipPosition.cs"
require_file "NamazuFlippers/Data/FlipLedgerEnvelope.cs"
require_file "NamazuFlippers/Data/FlipLedgerStore.cs"
require_file "NamazuFlippers/UI/PositionsWindow.cs"
require_file "NamazuFlippers/NamazuFlippers.cs"
require_file "NamazuFlippers/API/Models/ApiJsonContext.cs"

echo
echo "PROFIT-01/02: manual sold-state and actual sale price capture"
require_all_patterns "NamazuFlippers/UI/PositionsWindow.cs" "Positions UI exposes manual sold entry" \
  "ImGui\\.Button\\(\"Sold\"" \
  "RecordSoldLot##positions" \
  "ImGui\\.InputInt\\(\"Sold qty\"" \
  "ImGui\\.InputInt\\(\"Unit sale\"" \
  "Record Sale" \
  "plugin\\.QueuePositionSold"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "plugin queues sold entry through ledger store" \
  "public void QueuePositionSold" \
  "ledgerStore\\.RecordSaleAsync" \
  "await RefreshOpenPositionsAsync"

echo
echo "PROFIT-03: tax-adjusted realized profit"
require_all_patterns "NamazuFlippers/Data/FlipSale.cs" "sale record stores sale price, net price, buy price, and realized profit" \
  "ActualUnitSalePrice" \
  "NetUnitSalePrice" \
  "UnitBuyPrice" \
  "RealizedUnitProfit" \
  "TotalRealizedProfit"
require_all_patterns "NamazuFlippers/Data/FlipLedgerStore.cs" "ledger computes FFXIV market-tax realized profit" \
  "MarketTaxRate = 0\\.95" \
  "Math\\.Floor\\(unitSalePrice \\* MarketTaxRate\\)" \
  "realizedUnitProfit = netUnitSalePrice - unitBuyPrice" \
  "TotalRealizedProfit = realizedUnitProfit \\* soldQuantity"
require_pattern "NamazuFlippers/UI/PositionsWindow.cs" "Math\\.Floor\\(salePriceInputs\\[position\\.Id\\] \\* 0\\.95\\)" \
  "sale confirmation previews after-tax sale price"

echo
echo "PROFIT-04: sold outcomes remain tied to original buy date/session"
require_all_patterns "NamazuFlippers/Data/FlipPosition.cs" "position preserves sales while retaining buy/session trace" \
  "DateTimeOffset BuyTimestampUtc" \
  "DateTimeOffset RouteCreatedAtUtc" \
  "string RouteSessionId" \
  "List<FlipSale> Sales" \
  "DateTimeOffset\\? LastSoldAtUtc" \
  "TotalRealizedProfit"
require_all_patterns "NamazuFlippers/Data/FlipLedgerStore.cs" "partial close updates quantities and status without deleting sold history" \
  "Math\\.Clamp\\(quantity, 1, position\\.RemainingQuantity\\)" \
  "position\\.Sales\\.Add\\(sale\\)" \
  "position\\.SoldQuantity = Math\\.Clamp\\(position\\.SoldQuantity \\+ soldQuantity" \
  "position\\.RemainingQuantity = Math\\.Max\\(0, position\\.BoughtQuantity - position\\.SoldQuantity\\)" \
  "position\\.Status = position\\.RemainingQuantity > 0" \
  "FlipPositionStatus\\.Sold"

echo
echo "Ledger compatibility and serialization"
require_pattern "NamazuFlippers/Data/FlipLedgerEnvelope.cs" "CurrentSchemaVersion = 1" \
  "Phase 7 keeps Phase 6 ledger schema compatible instead of discarding v1 files"
require_pattern "NamazuFlippers/Data/FlipLedgerStore.cs" "position\\.Sales \\?\\?= \\[\\]" \
  "old/malformed sale lists are normalized before appending"
require_all_patterns "NamazuFlippers/API/Models/ApiJsonContext.cs" "sale types registered for source-generated JSON" \
  "JsonSerializable\\(typeof\\(FlipSale\\)\\)" \
  "JsonSerializable\\(typeof\\(List<FlipSale>\\)\\)"
require_absent_pattern "NamazuFlippers/Data/FlipLedgerStore.cs" "auto.?match|retainer|gil total|chat" \
  "Phase 7 remains manual and does not add game-observed reconciliation"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 07 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 07 Nyquist validation passed.\n'
