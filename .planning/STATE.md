---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: ready_to_plan
stopped_at: Phase 04 fully closed (build 1.0.32.0); ready to plan Phase 05 (Session Persistence)
last_updated: "2026-05-09T23:30:00Z"
last_activity: 2026-05-09 -- Phase 04 closed including post-04-08 UAT hotfixes (sale_rates per-hour fix, GAP-F1 column scaling, GAP-F2 cumulative budget, velocity label, RouteStops counter regression)
progress:
  total_phases: 7
  completed_phases: 4
  total_plans: 14
  completed_plans: 14
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-07)

**Core value:** A single button gives you today's best arbitrage route. Follow it, buy, list, done in under 20 minutes. Every day.
**Current focus:** Phase 05 — session-persistence (ready to plan)
**Build model:** macOS local builds are not expected to compile without Dalamud SDK assemblies; use source validation locally and GitHub Actions as the authoritative compiler/package gate.

## Current Position

Phase: 04 (core-ui) — COMPLETE and confirmed in build 1.0.32.0
Plan: All 9 phase-04 plans + 5 post-04 UAT-driven hotfixes shipped
Next: 05 — Session Persistence (ready to plan)
Status: Ready to plan Phase 05
Last activity: 2026-05-09 -- Phase 04 fully verified in-game (build 1.0.32.0)

Progress: [██████████] 100%

**Phase 1 deliverables:**

- NamazuFlippers solution + project (net10.0-windows, Dalamud.NET.Sdk/15.0.0)
- Plugin manifest (API level 15, /nflip command)
- Entry point with IDalamudPlugin lifecycle
- Configuration class (14 props, CONF-01 through CONF-09, IPluginConfiguration)
- FirstRunWindow with 85-world validation
- GitHub Actions CI/CD: cross-platform build (Ubuntu), auto-versioned releases, pluginmaster.json for Dalamud custom repo
- .gitignore for build artifacts

**Phase 3 validation/build status:**

- `tests/phase03_nyquist.sh` provides source-level automated validation for SCAN-01 through SCAN-04.
- GitHub Actions downloads Dalamud into `DALAMUD_HOME` and should be used for compile/package verification.
- macOS `dotnet build NamazuFlippers/NamazuFlippers.csproj` fails in this workspace because Dalamud assemblies are unavailable locally; this is a known environment limitation, not a project blocker.

## Performance Metrics

**Velocity:**

- Total plans completed: 15
- Total execution time: ~21 min

**By Phase:**
| Phase | Plans | Duration |
| ----- | ----- | -------- |
| 01    | 2     | ~21 min  |
| Phase 03 P01 | 18 min | 3 tasks | 6 files |
| Phase 03 P02 | 9 min | 5 tasks | 10 files |
| Phase 04-core-ui P04-02 | 12 | 2 tasks | 1 files |
| Phase 04 P04-03 | 4min | 2 tasks | 1 files |
| Phase 04 P04-07 | 2 min | 2 tasks | 2 files |
| Phase 04 P04-08 | 4 min | 2 tasks | 2 files |

## Accumulated Context

### Decisions

- Plugin scaffold architecture: thin entry point (62 lines), UI in dedicated window classes
- Cross-platform build via goatcorp.github.io/dalamud-distrib/latest.zip
- Auto-versioned CI releases: 1.0.{run}.0
- Named category constants: FurnitureIds, CollectibleIds, GlamourIds, DefaultCategoryFilters
- World validation: hash set of 85 FFXIV worlds, case-insensitive
- Repo: public, DarrellTang/namazu-flippers
- D-08 (04-01): WindowSystem ownership stays in NamazuFlippers.cs — 196-line entry point is under complexity threshold for indirection layer
- 04-01: DailyRouteWindow declares private color constants with literal Vector4 values for nyquist.sh assertions; UiColors.cs is the public canonical source
- [Phase ?]: D-12: showUnsavedModal trigger at top of Draw() ensures same-frame OpenPopup
- [Phase ?]: D-13: HomeWorld preserved on Reset to defaults
- 04-07: buttonSpacing in DrawProgressSection sourced from ImGui.GetStyle().ItemSpacing.X at runtime — never a compile-time constant — so reservation tracks the SameLine() actual gap at every Dalamud UI scale
- 04-07: Listed checkbox column anchored via ImGui.SameLine(GetWindowContentRegionMax().X - 150f) with a bare-SameLine fallback when the row is too narrow — fixed-X column independent of preceding widget widths
- 04-08: Settings + Rescan render on their OWN row (no SameLine after the bought/listed Text) so avail = ImGui.GetContentRegionAvail().X measures the FULL content region width — not the leftover after a partially-consumed row
- 04-08: rescanWidth = 110f * ImGuiHelpers.GlobalScale and settingsWidth = 80f * ImGuiHelpers.GlobalScale — literal-pixel button widths multiplied by FFXIV UI scale so frames grow with scaled fonts; closes GAP-E1 (the user-visible mechanism that 04-07's correct-but-insufficient fix didn't address)
- 04-08 lesson: when a UAT-round-N fix doesn't close the user-visible report on round N+1, re-derive the pixel arithmetic at the user's actual UI scale BEFORE attempting another math-only fix — round-N math correctness does not imply round-N causal sufficiency

### Pending Todos

None.

### Blockers/Concerns

No implementation blockers. Local macOS compiler verification is intentionally replaced by source validation plus CI build verification.

## Session Continuity

Last session: 2026-05-08T05:34:00Z
Stopped at: Completed 04-08-PLAN.md (Phase 4 gap-closure for GAP-E1, supersedes GAP-D1's user-visible mechanism)
Resume file: None
