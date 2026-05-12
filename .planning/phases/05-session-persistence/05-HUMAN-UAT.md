---
status: partial
phase: 05-session-persistence
source: [05-VERIFICATION.md]
started: 2026-05-12T16:25:31Z
updated: 2026-05-12T16:25:31Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Toggle a checkbox, /xlreload, reopen route window — checkmarks restored
expected: Bought/Listed checks reappear; no banner; counters/progress bars match pre-reload state
result: [pending]

### 2. Mark All Bought → /xlreload → verify all bought boxes still checked
expected: Every routed item is bought=true after reload; one save round-trip persisted the bulk action
result: [pending]

### 3. Rescan after Mark All Bought — verify in-memory dicts wipe and persisted envelope SessionState is empty
expected: Progress bars reset to 0/N; on next reload the cleared state survives (clean slate after Rescan)
result: [pending]

### 4. Manually downgrade scan-cache.json SchemaVersion to 1, restart plugin
expected: Envelope auto-discarded; fresh scan starts with empty SessionState; no migration code path runs
result: [pending]

### 5. Simulate save failure (set scan-cache.json read-only or attach AV-style file lock) and toggle a checkbox
expected: log.Warning entry in /xllog with 'could not save session state'; no banner, no chat message, UI continues normally
result: [pending]

### 6. Toggle a checkbox while a scan is in progress (auto-scan-on-login race)
expected: Final on-disk envelope contains BOTH the fresh scan data AND the user's toggle — no cache corruption, no lost click
result: [pending]
notes: BLOCKER-01/02 from 05-REVIEW.md predicts cache loss here; goal-backward this is the most likely real-world failure mode

### 7. FFXIV UI scale = 1.5 — verify Mark All row + Settings/Rescan row both fit inside the window without overflow
expected: Both buttons visible on their own row; no clipping past right edge
result: [pending]

### 8. CI compile/package on GitHub Actions
expected: Build succeeds, .dll produced, repo.json updated for the test build
result: [pending]

## Summary

total: 8
passed: 0
issues: 0
pending: 8
skipped: 0
blocked: 0

## Gaps
