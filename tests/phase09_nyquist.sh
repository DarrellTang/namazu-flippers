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

echo "Phase 09 Nyquist validation — profit-per-gil ranking, Kelly sizing, Universalis enrichment"

require_file "NamazuFlippers/Core/ScanEngine.cs"
require_file "NamazuFlippers/Core/RouteOptimizer.cs"
require_file "NamazuFlippers/Core/OpportunityScoring.cs"
require_file "NamazuFlippers/Core/KellySizer.cs"
require_file "NamazuFlippers/API/UniversalisClient.cs"
require_file "NamazuFlippers/API/Models/UniversalisModels.cs"
require_file "NamazuFlippers/UI/DailyRouteWindow.cs"
require_file "NamazuFlippers/Configuration.cs"
require_file "NamazuFlippers/Data/ScanCacheEnvelope.cs"
require_file "NamazuFlippers.Tests/NamazuFlippers.Tests.csproj"

echo
echo "Criterion 1: capital-efficiency ranking replaces the absolute-profit primary sort"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "ScanEngine ranks by capital efficiency then final rank" \
  "OrderByDescending\(opportunity => opportunity\.CapitalEfficiency\)" \
  "OrderByDescending\(opportunity => opportunity\.FinalRank\)" \
  "ThenByDescending\(opportunity => opportunity\.SalesPerDay\)" \
  "ThenByDescending\(opportunity => opportunity\.ExpectedDailyProfit\)" \
  "ThenBy\(opportunity => opportunity\.PurchasePrice\)"
require_absent_pattern "NamazuFlippers/Core/ScanEngine.cs" "OrderByDescending\(item => item\.ExpectedDailyProfit\)" \
  "old ExpectedDailyProfit primary sort is gone"

echo
echo "Criterion 2: admissibility floors remain flat (not velocity-banded)"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "IsUsable still enforces MinProfitAmount, PreferredRoi, MinSalesPerDay" \
  "config\.MinProfitAmount" \
  "config\.PreferredRoi" \
  "config\.MinSalesPerDay"
require_pattern "NamazuFlippers/Core/RouteOptimizer.cs" "configuration\.MaxItemsPerSession" \
  "RouteOptimizer still caps the route by MaxItemsPerSession"

echo
echo "Criteria 3/4/5: confidence + absorption math is pure and applied"
require_all_patterns "NamazuFlippers/Core/OpportunityScoring.cs" "OpportunityScoring exposes the pure scoring functions" \
  "public static double CapitalEfficiency" \
  "public static double ExpectedDemand" \
  "public static double SellConfidence" \
  "public static double PriceConfidence" \
  "public static double AbsorptionCap" \
  "public static double FinalRank"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "ScanEngine applies sell/price confidence and absorption cap" \
  "OpportunityScoring\.SellConfidence" \
  "OpportunityScoring\.PriceConfidence" \
  "OpportunityScoring\.AbsorptionCap" \
  "configuration\.HoldingWindowDays" \
  "configuration\.PriceCorroborationThreshold" \
  "configuration\.MinRecentSalesToJudge"

echo
echo "Criterion 6: absorption-capped half-Kelly sizing with MaxBudgetPerSession as the pool"
require_all_patterns "NamazuFlippers/Core/KellySizer.cs" "KellySizer sizes by edge x confidence, capped by absorption + budget" \
  "public static void AssignQuantities" \
  "AbsorptionCap" \
  "RecommendedQuantity" \
  "TotalDeployedGil" \
  "TotalAbsorptionCeilingGil"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "ScanEngine runs Kelly sizing with the budget pool + Kelly fraction" \
  "KellySizer\.AssignQuantities" \
  "configuration\.MaxBudgetPerSession" \
  "configuration\.KellyFraction"

echo
echo "Criterion 7: one batched Universalis enrichment of the top survivors, gated by EnableUniversalis"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "ScanEngine enriches top survivors via Universalis" \
  "MaxEnrichItems = 100" \
  "\.Take\(MaxEnrichItems\)" \
  "configuration\.EnableUniversalis" \
  "universalisClient" \
  "FetchAsync"
require_all_patterns "NamazuFlippers/API/UniversalisClient.cs" "UniversalisClient batches a home-world v2 request" \
  "public async Task<IReadOnlyDictionary<int, UniversalisItemData>> FetchAsync" \
  "/api/v2/"
require_all_patterns "NamazuFlippers/API/Models/UniversalisModels.cs" "UniversalisItemData carries depth + recent sales" \
  "class UniversalisItemData" \
  "Depth" \
  "RecentMedianSalePrice" \
  "RecentSalesCount"

echo
echo "Criterion 8: graceful degradation — a scan never fails because Universalis failed"
require_all_patterns "NamazuFlippers/Core/ScanEngine.cs" "ScanEngine catches Universalis failure and degrades to velocity-only" \
  "catch \(Exception" \
  "velocity-only" \
  "UniversalisEnrichmentFailed"
require_pattern "NamazuFlippers/API/UniversalisClient.cs" "catch \(OperationCanceledException\)" \
  "UniversalisClient only rethrows genuine cancellation; other failures degrade"

echo
echo "Criterion 9: route window shows recommended quantity + a session deployment summary"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "DailyRouteWindow renders quantity + deployment summary" \
  "item\.RecommendedQuantity" \
  "KellySizer\.TotalDeployedGil" \
  "KellySizer\.TotalAbsorptionCeilingGil" \
  "Deployed:" \
  "absorption ceiling"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "sell-confidence + depth are secondary (tooltip), not inline" \
  "item\.Depth" \
  "item\.SellConfidence" \
  "item\.PriceConfidence"

echo
echo "Criterion 10: new persisted config settings with the locked defaults"
require_all_patterns "NamazuFlippers/Configuration.cs" "Configuration has the Tier 1-3 settings + defaults" \
  "HoldingWindowDays.*= 7" \
  "KellyFraction.*= 0\.5" \
  "EnableUniversalis.*= true" \
  "PriceCorroborationThreshold.*= 0\.9" \
  "MinRecentSalesToJudge.*= 3"

echo
echo "Criterion 11: ScanCacheEnvelope bumped v2 -> v3; v2 treated as stale"
require_pattern "NamazuFlippers/Data/ScanCacheEnvelope.cs" "CurrentSchemaVersion = 3" \
  "cache schema version is 3"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "SchemaVersion == ScanCacheEnvelope\.CurrentSchemaVersion" \
  "IsValid rejects envelopes whose SchemaVersion != 3 (v2 caches go stale)"

echo
echo "Criterion 12: RouteOptimizer drops the budget cap + travel cost; groups the sized set"
require_all_patterns "NamazuFlippers/Core/RouteOptimizer.cs" "RouteOptimizer groups the Kelly-sized set into stops" \
  "RecommendedQuantity > 0" \
  "GroupBy" \
  "MaxServersToVisit"
require_absent_pattern "NamazuFlippers/Core/RouteOptimizer.cs" "MaxBudgetPerSession" \
  "RouteOptimizer no longer applies the budget cap (Kelly owns sizing)"
require_absent_pattern "NamazuFlippers/Core/RouteOptimizer.cs" "FrictionTieBreakWindow" \
  "RouteOptimizer no longer biases selection by travel friction (world travel is free)"

echo
echo "Wiring: plugin constructs the Universalis client and injects it into the scan engine"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "NamazuFlippers wires UniversalisClient into ScanEngine" \
  "new UniversalisClient\(" \
  "new ScanEngine\(.*universalisClient\)"

echo
echo "CI: dotnet test + phase09 nyquist are wired into the build workflow"
require_all_patterns ".github/workflows/build.yml" "build.yml runs the xUnit tests and this nyquist script" \
  "dotnet test" \
  "phase09_nyquist"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 09 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 09 Nyquist validation passed.\n'
