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

require_order() {
  local file="$1"
  local first="$2"
  local second="$3"
  local label="$4"
  local first_line
  local second_line

  first_line="$(grep -nE "$first" "$file" | head -1 | cut -d: -f1 || true)"
  second_line="$(grep -nE "$second" "$file" | head -1 | cut -d: -f1 || true)"

  if [[ -n "$first_line" && -n "$second_line" && "$first_line" -lt "$second_line" ]]; then
    pass "$label"
  else
    fail "$label"
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

echo "Phase 03 Nyquist validation"

require_file "NamazuFlippers/API/SaddlebagClient.cs"
require_file "NamazuFlippers/Core/ScanEngine.cs"
require_file "NamazuFlippers/Core/RouteOptimizer.cs"
require_file "NamazuFlippers/Data/ScanCacheEnvelope.cs"
require_file "NamazuFlippers/Data/ScanCacheStore.cs"
require_file "NamazuFlippers/NamazuFlippers.cs"

echo
echo "SCAN-01: fresh scan filtering, ranking, and structured results"
require_pattern "NamazuFlippers/API/SaddlebagClient.cs" "NormalizeScanResponse" "scan response shape is normalized at API boundary"
require_all_patterns "NamazuFlippers/API/SaddlebagClient.cs" "normalizer accepts known wrapper shapes" \
  "TryGetArrayProperty\\(root, \"items\"" \
  "TryGetArrayProperty\\(root, \"results\"" \
  "TryGetArrayProperty\\(root, \"data\"" \
  "ApiJsonContext\\.Default\\.ListScanItem"
require_pattern "NamazuFlippers/Core/ScanEngine.cs" "client\\.ScanAsync\\(ct\\)" "fresh scan calls SaddlebagClient.ScanAsync"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "invalid scan rows are filtered before ranking" \
  "Where\\(IsUsable\\)" \
  "item\\.ItemId > 0" \
  "!string\\.IsNullOrWhiteSpace\\(item\\.Name\\)" \
  "!string\\.IsNullOrWhiteSpace\\(item\\.CheapestServer\\)" \
  "item\\.HomePrice > 0" \
  "item\\.CheapestPrice > 0" \
  "item\\.ExpectedDailyProfit > 0" \
  "item\\.SalesPerDay > 0"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "ranking is deterministic" \
  "OrderByDescending\\(item => item\\.ExpectedDailyProfit\\)" \
  "ThenByDescending\\(item => item\\.SalesPerDay\\)" \
  "ThenBy\\(item => item\\.CheapestPrice\\)"
# Final item-count cap moved to RouteOptimizer.TrimItemsPreservingStopOrder so the
# RouteOptimizer cumulative budget filter (GAP-F2) can skip past too-expensive
# top-rank items and reach affordable ones below.
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "fresh scan returns structured success, empty, and error outcomes" \
  "Status = ScanEngineStatus\\.Success" \
  "Status = ScanEngineStatus\\.Empty" \
  "catch \\(ApiException ex\\)" \
  "Status = ScanEngineStatus\\.Error" \
  "TechnicalDetails = ex\\.Message" \
  "UserMessage = "
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "vendor source metadata is preserved" \
  "PurchaseSource = item\\.CheapestServer" \
  "OutOfStock = item\\.OutOfStock" \
  "IsVendorSource = IsVendorSource\\(item\\.CheapestServer\\)" \
  "StartsWith\\(\"Vendor:\", StringComparison\\.OrdinalIgnoreCase\\)"

echo
echo "SCAN-02: route grouping and value-first stop ordering"
require_all_patterns "NamazuFlippers/Core/RouteOptimizer.cs" "route optimizer groups opportunities by purchase source" \
  "GroupBy\\(opportunity => opportunity\\.PurchaseSource, StringComparer\\.OrdinalIgnoreCase\\)" \
  "CreateRouteStop\\(group, configuration\\.HomeWorld\\)" \
  "PurchaseSource = purchaseSource"
require_all_patterns "NamazuFlippers/Core/RouteOptimizer.cs" "route optimizer enforces stop and item caps" \
  "Math\\.Max\\(1, configuration\\.MaxServersToVisit\\)" \
  "Math\\.Max\\(1, configuration\\.MaxItemsPerSession\\)" \
  "Take\\(stopLimit\\)" \
  "TrimItemsPreservingStopOrder"
require_all_patterns "NamazuFlippers/Core/RouteOptimizer.cs" "travel friction is limited to the 20 percent tie-break window" \
  "FrictionTieBreakWindow = 0\\.20" \
  "IsWithinFrictionTieBreakWindow" \
  "x\\.TravelFriction\\.CompareTo\\(y\\.TravelFriction\\)" \
  "y\\.TotalExpectedDailyProfit\\.CompareTo\\(x\\.TotalExpectedDailyProfit\\)"
require_order "NamazuFlippers/Core/RouteOptimizer.cs" "IsWithinFrictionTieBreakWindow" "y\\.TotalExpectedDailyProfit\\.CompareTo\\(x\\.TotalExpectedDailyProfit\\)" "value sort remains the fallback after friction tie-break"
require_all_patterns "NamazuFlippers/Data/WorldData.cs" "world data supports route friction decisions" \
  "StringComparer\\.OrdinalIgnoreCase" \
  "GetDataCenter" \
  "GetTravelFriction" \
  "return homeDc\\.Equals\\(purchaseDc, StringComparison\\.OrdinalIgnoreCase\\) \\? 1 : 2"
require_all_patterns "NamazuFlippers/Core/ScanEngineResult.cs" "scan result exposes route output for Phase 4" \
  "List<RouteStop> RouteStops" \
  "TotalExpectedDailyProfit"

echo
echo "SCAN-03: cache validity, reuse, and stale fallback"
require_all_patterns "NamazuFlippers/Data/ScanCacheEnvelope.cs" "cache envelope stores raw and derived scan data with schema metadata" \
  "CurrentSchemaVersion" \
  "CreatedAtUtc" \
  "ExpiresAtUtc" \
  "ConfigFingerprint" \
  "ScanResponse RawResponse" \
  "ScanEngineResult DerivedResult"
require_all_patterns "NamazuFlippers/Data/ScanCacheStore.cs" "cache is plugin-local and validity-gated" \
  "pluginInterface\\.ConfigDirectory\\.FullName" \
  "scan-cache\\.json" \
  "SchemaVersion == ScanCacheEnvelope\\.CurrentSchemaVersion" \
  "ExpiresAtUtc > nowUtc" \
  "ConfigFingerprint == expectedFingerprint"
require_all_patterns "NamazuFlippers/Data/ScanCacheStore.cs" "config fingerprint covers scan-affecting settings" \
  "configuration\\.HomeWorld" \
  "configuration\\.PreferredRoi" \
  "configuration\\.MinProfitAmount" \
  "configuration\\.MinDesiredAvgPpu" \
  "configuration\\.MinSalesPerWeek" \
  "configuration\\.RegionWide" \
  "configuration\\.IncludeVendors" \
  "configuration\\.ShowOutOfStock" \
  "configuration\\.MaxItemsPerSession" \
  "configuration\\.MaxServersToVisit" \
  "configuration\\.CategoryFilters"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "scan engine reuses valid cache and marks stale fallback" \
  "!forceRefresh && cacheStore != null" \
  "LoadValidAsync\\(ct\\)" \
  "ScanEngineStatus\\.UsingCache" \
  "LoadAnyAsync\\(ct\\)" \
  "ScanEngineStatus\\.UsingStaleCache" \
  "IsFresh = false"
require_absent_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "NamazuFlippers/Data/scan-cache|\\.planning|repo" "cache store does not hard-code repository paths"

echo
echo "SCAN-04: manual refresh, login scan, and duplicate guard"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "manual scan command bypasses cache" \
  "subcommand\\.Equals\\(\"scan\", StringComparison\\.OrdinalIgnoreCase\\)" \
  "RunScanAsync\\(forceRefresh: true, scanCts\\.Token\\)"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "bare command still toggles UI" \
  "if \\(!string\\.IsNullOrEmpty\\(subcommand\\)\\)" \
  "dailyRouteWindow\\.IsOpen = !dailyRouteWindow\\.IsOpen"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "duplicate scans are ignored and released" \
  "Interlocked\\.Exchange\\(ref scanInProgress, 1\\) == 1" \
  "scan already running" \
  "finally" \
  "Interlocked\\.Exchange\\(ref scanInProgress, 0\\)"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "login/startup auto-scan uses cache and skips missing setup" \
  "clientState\\.Login \\+= OnLogin" \
  "clientState\\.Login -= OnLogin" \
  "clientState\\.IsLoggedIn" \
  "QueueAutoScan" \
  "RunScanAsync\\(forceRefresh: false, scanCts\\.Token\\)" \
  "string\\.IsNullOrWhiteSpace\\(Configuration\\.HomeWorld\\)"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "latest scan state and errors are updated from structured result" \
  "LatestScanResult = result" \
  "LastApiError = result\\.Status == ScanEngineStatus\\.Error \\? result\\.UserMessage : null" \
  "result\\.RouteStops\\.Count" \
  "result\\.Opportunities\\.Count" \
  "result\\.TotalExpectedDailyProfit"

echo
echo "SCAN-05: sale_rates is sales/HOUR — convert to sales/day before MinSalesPerDay compare"
# Saddlebag /api/scan returns sale_rates as sales-per-HOUR averaged over hours_ago
# (verified empirically: with min_sales=2 and hours_ago=168, the lowest sale_rates
# observed is 2/168 = 0.0119). Treating it as per-day under-reports velocity by 24x
# and silently rejects most non-furniture items at the IsUsable MinSalesPerDay floor.
require_all_patterns "NamazuFlippers/API/SaddlebagClient.cs" "MapItem converts sale_rates from per-hour to per-day" \
  "TryParse\\(raw\\.SaleRates,.*out var salesPerHour\\)" \
  "salesPerHour \\* 24"

echo
echo "GAP-F2: cumulative budget cap is applied in RouteOptimizer (per-session, not per-item)"
require_all_patterns "NamazuFlippers/Core/RouteOptimizer.cs" "RouteOptimizer enforces cumulative MaxBudgetPerSession" \
  "configuration\\.MaxBudgetPerSession" \
  "spent \\+= item\\.PurchasePrice" \
  "item\\.PurchasePrice <= remaining"
require_absent_pattern "NamazuFlippers/Core/ScanEngine.cs" "MaxBudgetPerItem|MaxBudgetPerSession" \
  "ScanEngine no longer enforces budget cap (moved to RouteOptimizer for cumulative semantics)"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 03 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 03 Nyquist validation passed.\n'
