#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

failures=0

pass() { printf 'ok - %s\n' "$1"; }
fail() { printf 'not ok - %s\n' "$1" >&2; failures=$((failures + 1)); }

require_file() {
  if [[ -f "$1" ]]; then pass "$1 exists"; else fail "$1 exists"; fi
}

require_pattern() {
  if grep -Eq "$2" "$1"; then pass "$3"; else fail "$3"; fi
}

require_all_patterns() {
  local file="$1" label="$2"; shift 2
  local missing=() pattern
  for pattern in "$@"; do
    grep -Eq "$pattern" "$file" || missing+=("$pattern")
  done
  if [[ "${#missing[@]}" -eq 0 ]]; then pass "$label"; else fail "$label (missing: ${missing[*]})"; fi
}

echo "Phase 10 Nyquist validation — Holding Window slider + Universalis transient-error retry"

require_file "NamazuFlippers/UI/ConfigWindow.cs"
require_file "NamazuFlippers/API/UniversalisClient.cs"

echo
echo "Criterion 1: Holding Window is an editable, clamped slider with a trade-off tooltip"
require_all_patterns "NamazuFlippers/UI/ConfigWindow.cs" "ConfigWindow renders the Holding Window slider" \
  "SliderInt\(\"Holding Window \(days\)\"" \
  "plugin\.Configuration\.HoldingWindowDays" \
  "Math\.Clamp\(holdingWindow, 1, 30\)" \
  "absorption"

echo
echo "Criterion 2: HoldingWindowDays persists through snapshot/restore/defaults"
require_all_patterns "NamazuFlippers/UI/ConfigWindow.cs" "HoldingWindowDays round-trips config-window flows" \
  "HoldingWindowDays          = source\.HoldingWindowDays" \
  "HoldingWindowDays          = snapshot\.HoldingWindowDays" \
  "HoldingWindowDays          = 7"

echo
echo "Criterion 3: Universalis retries transient 5xx / network / timeout with bounded backoff"
require_all_patterns "NamazuFlippers/API/UniversalisClient.cs" "UniversalisClient retries transient failures" \
  "GetWithRetryAsync" \
  "MaxAttempts = 3" \
  "status < 500" \
  "Task\.Delay" \
  "Math\.Pow\(2, attempt - 1\)" \
  "ex is HttpRequestException or TaskCanceledException"

echo
echo "Criterion 4: graceful degradation preserved — only genuine cancellation propagates"
require_all_patterns "NamazuFlippers/API/UniversalisClient.cs" "retry path still degrades, never throws except cancellation" \
  "catch \(OperationCanceledException\) when \(ct\.IsCancellationRequested\)" \
  "if \(body == null\)" \
  "return result;"

if [[ "$failures" -ne 0 ]]; then
  printf '\nPhase 10 Nyquist validation failed: %d check(s) failed.\n' "$failures" >&2
  exit 1
fi

printf '\nPhase 10 Nyquist validation passed.\n'
