---
phase: 04
slug: core-ui
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-06
---

# Phase 04 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Bash source-level Nyquist validation (same pattern as `tests/phase03_nyquist.sh`) |
| **Config file** | `tests/phase04_nyquist.sh` (Wave 0 — does not exist yet) |
| **Quick run command** | `bash tests/phase04_nyquist.sh` |
| **Full suite command** | `bash tests/phase04_nyquist.sh` locally; GitHub Actions for compile/package |
| **Estimated runtime** | ~1 second (grep-based, no Dalamud runtime) |

---

## Sampling Rate

- **After every task commit:** Run `bash tests/phase04_nyquist.sh`
- **After every plan wave:** Run `bash tests/phase04_nyquist.sh` + check GitHub Actions build
- **Before `/gsd-verify-work`:** Full source validation green + GitHub Actions build green
- **Max feedback latency:** ~1 second (local grep), ~2 minutes (CI build)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 04-00-01 | 00 | 0 | All UI-* | — | N/A | source | `bash tests/phase04_nyquist.sh` | ❌ W0 | ⬜ pending |
| 04-01-* | 01 | 1 | UI-01 | — | Window registered, route renders | source | `bash tests/phase04_nyquist.sh` | ❌ W0 | ⬜ pending |
| 04-02-* | 02 | 2 | UI-02, UI-03, UI-04, UI-05, UI-07 | — | Interactions wired, profit/progress computed | source | `bash tests/phase04_nyquist.sh` | ❌ W0 | ⬜ pending |
| 04-03-* | 03 | 1 | UI-06, UI-08, CONF-01..09 | — | OOS visual + ConfigWindow controls | source | `bash tests/phase04_nyquist.sh` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Detailed per-requirement source-pattern assertions are documented in `04-RESEARCH.md` § Validation Architecture.*

---

## Wave 0 Requirements

- [ ] `tests/phase04_nyquist.sh` — covers UI-01 through UI-08 and CONF-01 through CONF-09 with the source-pattern assertions enumerated in `04-RESEARCH.md`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Window appears in-game at correct size/position | UI-01 | Requires Dalamud runtime | Launch FFXIV with plugin → `/namazu` → DailyRouteWindow opens at spec'd 720×900 with min/max constraints |
| Escape key closes DailyRouteWindow via WindowSystem integration | UI-01 | Requires Dalamud runtime | Open window in-game → press Escape → window closes |
| Dirty-state close-prompt modal renders, Save/Discard/Cancel work | UI-08 | Requires Dalamud runtime | Open ConfigWindow → change a setting → click X → modal appears → all three buttons behave correctly |
| ConfigWindow accessible via `/xlsettings` gear icon | UI-08 | Requires Dalamud runtime | Open Dalamud Settings → Plugin Installer → Namazu Flippers → gear icon opens ConfigWindow |
| Live profit tally updates as boxes are checked | UI-04 | Requires interaction | Check listed boxes one by one → tally increments by item profit each click |
| Auto-collapse fires on first all-bought frame, doesn't re-fire | UI-07 | Requires runtime + interaction | Check all bought boxes for one stop → collapses → manually re-expand → stays open |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (`tests/phase04_nyquist.sh`)
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s (target: ~1s grep)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
