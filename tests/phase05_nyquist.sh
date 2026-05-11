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

echo "Phase 05 Nyquist validation"

# === File existence ===
require_file "NamazuFlippers/Data/SessionState.cs"
require_file "NamazuFlippers/Data/ScanCacheEnvelope.cs"
require_file "NamazuFlippers/Data/ScanCacheStore.cs"
require_file "NamazuFlippers/UI/DailyRouteWindow.cs"
require_file "NamazuFlippers/NamazuFlippers.cs"
require_file "NamazuFlippers/API/Models/ApiJsonContext.cs"

echo
echo "SESS-01: Envelope schema bump and SessionState POCO (D-01, D-03)"
require_pattern "NamazuFlippers/Data/ScanCacheEnvelope.cs" "CurrentSchemaVersion = 2" "envelope schema bumped to 2 (D-01)"
require_pattern "NamazuFlippers/Data/ScanCacheEnvelope.cs" "SessionState SessionState" "envelope holds SessionState field (D-01)"
require_pattern "NamazuFlippers/Data/SessionState.cs" "namespace NamazuFlippers\.Data;" "SessionState lives in Data namespace"
require_pattern "NamazuFlippers/Data/SessionState.cs" "public sealed class SessionState" "SessionState is sealed POCO"
require_pattern "NamazuFlippers/Data/SessionState.cs" "Dictionary<int, bool> Bought" "SessionState.Bought present (D-03)"
require_pattern "NamazuFlippers/Data/SessionState.cs" "Dictionary<int, bool> Listed" "SessionState.Listed present (D-03)"
require_absent_pattern "NamazuFlippers/Data/SessionState.cs" "AutoCollapsed|LastModifiedUtc" "SessionState has no AutoCollapsed/LastModifiedUtc (D-03)"

echo
echo "SESS-01: JSON source-gen registration (D-01)"
require_pattern "NamazuFlippers/API/Models/ApiJsonContext.cs" "JsonSerializable\(typeof\(SessionState\)\)" "SessionState registered in source-gen context"
require_pattern "NamazuFlippers/API/Models/ApiJsonContext.cs" "JsonSerializable\(typeof\(Dictionary<int, ?bool>\)\)" "Dictionary<int,bool> registered in source-gen context"

echo
echo "SESS-01: ScanCacheStore.SaveSessionAsync persistence wiring (D-04, D-05, D-06)"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "public async Task SaveSessionAsync\(SessionState" "SaveSessionAsync method exists"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "SemaphoreSlim sessionSaveLock" "SemaphoreSlim sessionSaveLock declared (D-05)"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "await sessionSaveLock\.WaitAsync\(ct\)" "WaitAsync invoked before save (D-05)"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "sessionSaveLock\.Release\(\)" "Release invoked after save (D-05)"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "cachePath \+ \"\.tmp\"" "atomic temp-file pattern reused"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "File\.Move\(tempPath, cachePath, overwrite: true\)" "atomic rename reused"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "could not save session state" "silent log on save failure (D-06)"
require_pattern "NamazuFlippers/Data/ScanCacheStore.cs" "ex is IOException or JsonException or UnauthorizedAccessException" "save uses same exception filter as load (D-06)"

echo
echo "SESS-02: Plugin entry point exposes session state (D-08)"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "private readonly ScanCacheStore cacheStore" "cacheStore promoted to field"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "public SessionState\? CurrentSessionState" "CurrentSessionState property exposed"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "QueueSessionSave" "QueueSessionSave method exposed (D-04, D-05)"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "_ = Task\.Run" "save dispatched as fire-and-forget Task.Run (D-05)"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "cacheStore\.SaveSessionAsync" "QueueSessionSave calls SaveSessionAsync"
require_pattern "NamazuFlippers/NamazuFlippers.cs" "CurrentSessionState = envelope\?\.SessionState" "CurrentSessionState populated from envelope post-scan (D-08)"

echo
echo "SESS-02: DailyRouteWindow hydrate on first sight + save on toggle (D-04, D-08, D-09)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "plugin\.CurrentSessionState" "DailyRouteWindow reads CurrentSessionState (D-08)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "foreach \(var kv in session\.Bought\) boughtState\[kv\.Key\] = kv\.Value" "Bought hydrate loop"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "foreach \(var kv in session\.Listed\) listedState\[kv\.Key\] = kv\.Value" "Listed hydrate loop"
require_order "NamazuFlippers/UI/DailyRouteWindow.cs" "autoCollapsedStops\.Clear\(\);" "plugin\.CurrentSessionState" "hydrate runs AFTER the Clear() block"
require_order "NamazuFlippers/UI/DailyRouteWindow.cs" "plugin\.CurrentSessionState" "lastSeenResult = result;" "hydrate runs BEFORE lastSeenResult assignment"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "plugin\.QueueSessionSave\(boughtState, listedState\)" "DailyRouteWindow calls QueueSessionSave (D-04)"
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "Resumed your session|Restored session" "no restore banner (D-09)"

echo
echo "SESS-03: Mark All Bought / Mark All Listed bulk actions (D-10, D-11, D-12, D-13)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "Mark All Bought" "Mark All Bought button label (D-10)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "Mark All Listed" "Mark All Listed button label (D-10)"
require_order "NamazuFlippers/UI/DailyRouteWindow.cs" "Mark All Bought" "Mark All Listed" "Bought button rendered before Listed button"
require_order "NamazuFlippers/UI/DailyRouteWindow.cs" "ImGui\.Text\(\\\$\"Bought:" "Mark All Bought" "Mark All row sits AFTER bought/listed counter Text (D-11)"
require_order "NamazuFlippers/UI/DailyRouteWindow.cs" "Mark All Bought" "ImGui\.ProgressBar" "Mark All row sits BEFORE the progress bars (D-11)"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "foreach \(var item in routeItems\) boughtState\[item\.ItemId\] = true" "Mark All Bought iterates routeItems"
require_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "foreach \(var item in routeItems\) listedState\[item\.ItemId\] = true" "Mark All Listed iterates routeItems"
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "BeginDisabled\(\);[[:space:]]*\n[[:space:]]*if \(ImGui\.Button\(\"Mark All" "Mark All buttons not wrapped in BeginDisabled (D-13)"
require_absent_pattern "NamazuFlippers/UI/DailyRouteWindow.cs" "Confirm Mark All|Are you sure.*Mark All" "no confirmation modal for Mark All (D-12)"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 05 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 05 Nyquist validation passed.\n'
