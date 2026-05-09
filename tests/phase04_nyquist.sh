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

echo "Phase 04 Nyquist validation"

# === File existence ===
require_file "NamazuFlippers/UI/DailyRouteWindow.cs"
require_file "NamazuFlippers/UI/ConfigWindow.cs"
require_file "NamazuFlippers/UI/FirstRunWindow.cs"
require_file "NamazuFlippers/NamazuFlippers.cs"

echo
echo "UI-01: WindowSystem wiring and DailyRouteWindow scaffolding"
require_all_patterns "NamazuFlippers/NamazuFlippers.cs" "WindowSystem registered and all windows added" \
  "WindowSystem" \
  "AddWindow" \
  "windowSystem\.Draw"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "class DailyRouteWindow.*: Window" "DailyRouteWindow extends Window base class"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "CollapsingHeader" "route stops rendered as CollapsingHeader"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "PurchaseSource" "route stops read RouteStop.PurchaseSource"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "LatestScanResult" "DailyRouteWindow reads LatestScanResult"

echo
echo "UI-02: bought checkbox and boughtState"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "bought checkbox uses itemId key and updates boughtState" \
  "##bought-" \
  "boughtState" \
  "ImGui\.Checkbox"

echo
echo "UI-03: listed checkbox in home stop"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "listed checkbox uses itemId key and updates listedState" \
  "##listed-" \
  "listedState"

echo
echo "UI-04: profit tally in GilGold"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "profit tally rendered with GilGold color" \
  "1\.0f, 0\.85f, 0\.1f" \
  "ExpectedDailyProfit"

echo
echo "UI-05: progress bars with PlotHistogram color override"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "progress bars use PlotHistogram color push" \
  "PlotHistogram" \
  "ProgressBar"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.2f, 0\.8f, 0\.3f" "SuccessGreen color value present"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.2f, 0\.85f, 0\.9f" "PurchaseCyan color value present"

echo
echo "UI-06: OOS highlighting with OosOrange"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "OOS items highlighted with OosOrange and [OOS] badge" \
  "1\.0f, 0\.55f, 0\.1f" \
  "\[OOS\]" \
  "OutOfStock"

echo
echo "UI-07: auto-collapse on stop completion"
require_all_patterns "NamazuFlippers/UI/DailyRouteWindow.cs" "auto-collapse uses SetNextItemOpen and per-stop flag" \
  "SetNextItemOpen" \
  "autoCollapsedStops" \
  "ImGuiCond\.Always"

echo
echo "UI-08: ConfigWindow scaffolding, Save, Reset modal, OpenConfigUi entry point"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "class ConfigWindow.*: Window" "ConfigWindow extends Window base class"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "SavePluginConfig" "ConfigWindow calls SavePluginConfig on Save"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "BeginPopupModal" "ConfigWindow uses BeginPopupModal for confirmations"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "Reset to Defaults" "ConfigWindow has Reset to Defaults button label"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "Save Settings" "ConfigWindow has Save Settings button label"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "OpenConfigUi" "UiBuilder.OpenConfigUi registered for gear icon access"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "public void OpenConfigWindow" "Plugin exposes OpenConfigWindow for in-window Settings button (D-07)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "plugin\.OpenConfigWindow|OpenConfigWindow" "DailyRouteWindow has Settings button calling OpenConfigWindow (D-07)"

echo
echo "CONF-01..09: all configuration widgets present in ConfigWindow"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "BeginCombo" "HomeWorld dropdown uses BeginCombo (CONF-01)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "HomeWorld" "HomeWorld property referenced (CONF-01)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "PreferredRoi" "PreferredRoi widget (CONF-02)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MinProfitAmount" "MinProfitAmount widget (CONF-02)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MinDesiredAvgPpu" "MinDesiredAvgPpu widget (CONF-02)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MaxBudgetPerSession" "MaxBudgetPerSession widget (CONF-02)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MinSalesPerDay" "MinSalesPerDay widget (CONF-03)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MinSalesPerWeek" "MinSalesPerWeek widget (CONF-03)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "RegionWide" "RegionWide checkbox (CONF-04)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "CategoryFilters|FurnitureIds|CollectibleIds|GlamourIds" "Category filter toggles (CONF-05)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "IncludeVendors" "IncludeVendors checkbox (CONF-06)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "ShowOutOfStock" "ShowOutOfStock checkbox (CONF-06)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MaxItemsPerSession" "MaxItemsPerSession slider (CONF-07)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "MaxServersToVisit" "MaxServersToVisit slider (CONF-07)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "CacheDurationHours" "CacheDurationHours slider (CONF-08)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "EnableShortagePredictor" "EnableShortagePredictor checkbox visible (Phase 6 inert)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" "isDirty" "ConfigWindow tracks dirty flag (D-12)"

echo
echo "Color token integrity (UI-SPEC compliance)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "1\.0f, 0\.85f, 0\.1f" "GilGold color value (1.0, 0.85, 0.1)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "1\.0f, 0\.55f, 0\.1f" "OosOrange color value (1.0, 0.55, 0.1)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.9f, 0\.2f, 0\.2f" "ErrorRed color value (0.9, 0.2, 0.2)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.5f, 0\.5f, 0\.5f" "CompletedGray color value (0.5, 0.5, 0.5)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.4f, 0\.7f, 1\.0f" "CacheBlue color value (0.4, 0.7, 1.0)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "0\.9f, 0\.7f, 0\.1f" "StaleAmber color value (0.9, 0.7, 0.1)"

echo
echo "FirstRunWindow migration to Window base class (D-05, D-06)"
require_pattern "NamazuFlippers/UI/FirstRunWindow.cs" "class FirstRunWindow.*: Window" "FirstRunWindow extends Window base class"

echo
echo "Lambda handler safety (RESEARCH.md Pitfall 1)"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "OnOpenConfigUi" "OpenConfigUi handler stored as named method (not anonymous lambda)"

echo
echo "Gap closure regression: isHomeStop string-compare must not return"
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "isHomeStop\s*=" \
  "isHomeStop string-compare assignment is gone (gap-closure 04-04)"
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "Configuration\.HomeWorld" \
  "DailyRouteWindow no longer references plugin.Configuration.HomeWorld for stop classification (gap-closure 04-04)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "##listed-" \
  "Listed checkbox renders on every item row (gap-closure 04-04)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "listedState\[item\.ItemId\]" \
  "Listed checkbox writes listedState by ItemId (gap-closure 04-04)"

echo
echo "Gap closure regression: ConfigWindow OnOpen must guard snapshot with !isDirty"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" \
  "if \(!isDirty\)" \
  "OnOpen wraps snapshot capture in !isDirty guard (gap-closure 04-06)"
require_pattern "NamazuFlippers/UI/ConfigWindow.cs" \
  "snapshot = Snapshot\(plugin\.Configuration\)" \
  "Snapshot capture call survives inside the guard (gap-closure 04-06)"

echo
echo "Gap closure regression (04-07): runtime ItemSpacing.X for Settings+Rescan group (GAP-D1)"
# GAP-D1: buttonSpacing in DrawProgressSection MUST be sourced from
# ImGui.GetStyle().ItemSpacing.X at runtime, not a compile-time constant.
# Hardcoding 8f overflowed Rescan past the right edge at FFXIV UI scale > 1.0
# (see 04-REVIEW.md WR-02, .planning/debug/rescan-button-still-cut-off.md).
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "var[[:space:]]+buttonSpacing[[:space:]]*=[[:space:]]*ImGui\.GetStyle\(\)\.ItemSpacing\.X" \
  "DrawProgressSection reads buttonSpacing from runtime ImGui.GetStyle().ItemSpacing.X (GAP-D1, 04-07)"
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "const[[:space:]]+float[[:space:]]+buttonSpacing[[:space:]]*=[[:space:]]*8f" \
  "Hardcoded const float buttonSpacing = 8f is gone (GAP-D1, 04-07)"
# Defense in depth: combinedWidth must reference the runtime value, not a literal 8f.
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "combinedWidth[[:space:]]*=[[:space:]]*rescanWidth[[:space:]]*\+[[:space:]]*buttonSpacing[[:space:]]*\+[[:space:]]*settingsWidth" \
  "combinedWidth still composed from rescanWidth + buttonSpacing + settingsWidth (GAP-D1, 04-07)"

echo
echo "Gap closure regression (04-07): listed checkbox column anchor (GAP-D2)"
# GAP-D2: the ##listed-{itemId} Checkbox MUST be preceded by an absolute-X anchor
# (SameLine with a non-empty argument OR SetCursorPosX) so the checkbox lands in
# a consistent column across rows. A bare ImGui.SameLine() chained after
# variable-width elements caused per-row drift (see
# .planning/debug/listed-checkbox-not-aligned.md).
#
# Strategy: scan DailyRouteWindow.cs and assert that within a small window of
# source lines BEFORE the ##listed- Checkbox call, at least one anchor pattern
# appears. We use awk to confine the search to the lines preceding the
# ##listed- match — pattern-existence-anywhere would not enforce the structural
# relationship.
listed_anchor_check() {
  local file="NamazuFlippers/UI/DailyRouteWindow.cs"
  local label="absolute-X anchor (SameLine(arg) or SetCursorPosX) precedes ##listed- Checkbox in DrawItems (GAP-D2, 04-07)"
  if [[ ! -f "$file" ]]; then
    fail "$label (file missing)"
    return
  fi
  # Find the line number of the first ##listed- occurrence.
  local listed_line
  listed_line="$(grep -nE '##listed-' "$file" | head -1 | cut -d: -f1 || true)"
  if [[ -z "$listed_line" ]]; then
    fail "$label (no ##listed- match found)"
    return
  fi
  # Look back up to 30 source lines for an anchor pattern.
  local window_start=$(( listed_line - 30 ))
  if [[ "$window_start" -lt 1 ]]; then window_start=1; fi
  local anchor
  anchor="$(awk -v a="$window_start" -v b="$listed_line" \
              'NR>=a && NR<b && (/SameLine\([^)[:space:]]/ || /SetCursorPosX/) {print NR; exit}' \
              "$file")"
  if [[ -n "$anchor" ]]; then
    pass "$label"
  else
    fail "$label"
  fi
}
listed_anchor_check

echo
echo "Gap closure regression (04-08): Rescan/Settings own-row + GlobalScale-scaled widths (GAP-E1)"
# GAP-E1: 04-07's runtime ItemSpacing.X fix was real but addressed the wrong
# mechanism. The user-visible bug was that (a) the buttons were chained on
# the same row as the bought/listed Text via SameLine(), so `avail` measured
# only the leftover row width — not the full content region — and (b) the
# button widths were literal pixels that did not scale with FFXIV UI scale,
# so "Rescan Route" could not fit inside a 110px frame at scaled font.
# See .planning/debug/rescan-button-still-cut-off-2.md for pixel arithmetic.
#
# Assertion 1 (structural): the line IMMEDIATELY following the bought/listed
# ImGui.Text(...) line in DailyRouteWindow.cs MUST NOT be ImGui.SameLine();
# Blank lines are skipped — we look for the first non-blank source line.
progress_buttons_own_row_check() {
  local file="NamazuFlippers/UI/DailyRouteWindow.cs"
  local label="Settings/Rescan buttons render on their own row — no SameLine() immediately after bought/listed Text (GAP-E1, 04-08)"
  if [[ ! -f "$file" ]]; then
    fail "$label (file missing)"
    return
  fi
  local text_line
  text_line="$(grep -nE 'ImGui\.Text\(\$"Bought:' "$file" | head -1 | cut -d: -f1 || true)"
  if [[ -z "$text_line" ]]; then
    fail "$label (bought/listed Text line not found)"
    return
  fi
  # Walk forward from text_line+1 to the first non-blank line.
  local next_nonblank
  next_nonblank="$(awk -v start="$((text_line + 1))" \
                     'NR>=start && $0 !~ /^[[:space:]]*$/ {print NR":"$0; exit}' \
                     "$file")"
  if [[ -z "$next_nonblank" ]]; then
    fail "$label (no following non-blank line)"
    return
  fi
  local next_content="${next_nonblank#*:}"
  # Strip leading whitespace for the comparison.
  local trimmed="${next_content#"${next_content%%[![:space:]]*}"}"
  if [[ "$trimmed" == "ImGui.SameLine();"* ]]; then
    fail "$label"
  else
    pass "$label"
  fi
}
progress_buttons_own_row_check

# Assertion 2: both button widths are scaled by ImGuiHelpers.GlobalScale.
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "rescanWidth[[:space:]]*=[[:space:]]*110f[[:space:]]*\*[[:space:]]*ImGuiHelpers\.GlobalScale" \
  "rescanWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" \
  "settingsWidth[[:space:]]*=[[:space:]]*80f[[:space:]]*\*[[:space:]]*ImGuiHelpers\.GlobalScale" \
  "settingsWidth multiplied by ImGuiHelpers.GlobalScale (GAP-E1, 04-08)"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 04 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 04 Nyquist validation passed.\n'
