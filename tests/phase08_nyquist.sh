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

echo "Phase 08 Nyquist validation"

require_file "NamazuFlippers/UI/ProfitHistoryWindow.cs"
require_file "NamazuFlippers/UI/DailyRouteWindow.cs"
require_file "NamazuFlippers/NamazuFlippers.cs"
require_file "NamazuFlippers/Data/FlipLedgerStore.cs"

echo
echo "HIST-01: realized profit windows"
require_all_patterns "NamazuFlippers/UI/ProfitHistoryWindow.cs" "history UI shows today, 7-day, and 30-day realized profit" \
  "DrawRealizedSummary" \
  "Realized profit" \
  "Today:" \
  "7 days:" \
  "30 days:" \
  "TotalRealizedProfit" \
  "SoldAtUtc"

echo
echo "HIST-02: open positions review"
require_all_patterns "NamazuFlippers/UI/ProfitHistoryWindow.cs" "history UI includes open positions review" \
  "BeginTabItem\\(\"Open\"" \
  "DrawOpenPositions" \
  "RemainingQuantity > 0" \
  "Projected/unit" \
  "projected remaining"

echo
echo "HIST-03: sold history by original buy date/session"
require_all_patterns "NamazuFlippers/UI/ProfitHistoryWindow.cs" "sold history groups sales by original buy date" \
  "BeginTabItem\\(\"Sold\"" \
  "DrawSoldHistory" \
  "GroupBy\\(row => row\\.Position\\.BuyTimestampUtc\\.ToLocalTime\\(\\)\\.Date\\)" \
  "CollapsingHeader" \
  "ActualUnitSalePrice" \
  "NetUnitSalePrice" \
  "RealizedUnitProfit"

echo
echo "HIST-04: projected and realized values are separated"
require_all_patterns "NamazuFlippers/UI/ProfitHistoryWindow.cs" "projected vs realized terminology is explicit" \
  "Projected/unit" \
  "projected remaining" \
  "Realized/unit" \
  "Realized profit"

echo
echo "Window wiring and ledger source"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "plugin exposes all-ledger snapshot and history window" \
  "private List<FlipPosition> ledgerPositions" \
  "public IReadOnlyList<FlipPosition> LedgerPositions" \
  "ProfitHistoryWindow profitHistoryWindow" \
  "OpenProfitHistoryWindow" \
  "windowSystem\\.AddWindow\\(profitHistoryWindow\\)" \
  "ledgerStore\\.LoadPositionsAsync"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "daily route exposes History button" \
  "ImGui\\.Button\\(\"History\"" \
  "plugin\\.OpenProfitHistoryWindow"
require_pattern "NamazuFlippers/Data/FlipLedgerStore.cs" "public async Task<IReadOnlyList<FlipPosition>> LoadPositionsAsync" \
  "ledger store exposes all positions for history"

echo
echo "Automation boundary"
require_absent_pattern "NamazuFlippers/UI/ProfitHistoryWindow.cs" "QueuePositionSold|RecordSaleAsync|DeletePosition|auto.?match|retainer|gil total|chat" \
  "Phase 8 history UI is read-only and does not add reconciliation automation"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 08 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 08 Nyquist validation passed.\n'
