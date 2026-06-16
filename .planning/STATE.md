---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: in_progress
stopped_at: Phase 6 implemented and draft PR CI passed
last_updated: "2026-06-13T05:59:00.000Z"
last_activity: 2026-06-13 -- Phase 6 runtime hardening and ledger foundation implemented on draft PR #1
progress:
  total_phases: 10
  completed_phases: 6
  total_plans: 16
  completed_plans: 16
  percent: 60
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-06-13)

**Core value:** A single button gives you today's best arbitrage route, then tracks which flips sold and how much realized profit they produced.
**Current focus:** Phase 07 — manual-realized-profit-tracking
**Build model:** macOS local builds are not expected to compile without Dalamud SDK assemblies; use source validation locally and GitHub Actions as the authoritative compiler/package gate.

## Current Position

Phase: 07 (manual-realized-profit-tracking) — READY FOR SPEC/PLAN
Plan: 0 of TBD
Next: 07 — Manual Realized Profit Tracking (`$gsd-spec-phase 7` or implement directly from the end-state contract)
Status: Phase 06 implemented on `codex/tracking`; draft PR #1 is mergeable and GitHub Actions build passed
Last activity: 2026-06-13 -- Phase 6 runtime hardening and ledger foundation implemented, validated, and pushed

Progress: [████████████░░░░░░░░] 6/10 phases complete; next phases intentionally unplanned

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
| Phase 06 P06-01 | ~1 session | 1 bundled plan | 15 code/test files |

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
- Phase 05 completed by artifact count: one plan and one summary delivered session persistence, cache envelope schema v2, bought/listed hydration, and Mark All actions.
- Post-Phase-5 in-game use produced builds 33-38 with one-off runtime corrections: success banner, diagnostic logging, OOS sentinel correction, local MinProfit/ROI enforcement, all-listed completion semantics, click-to-copy names, per-sale profit display, and ConfigureAwait(false).
- Product direction changed after real usage: shortage predictor is deferred; the next value is a reliable flip-profit journal that tracks bought items, sold items, sale price, and realized profit by original buy date.
- Profit tracking will be position-based rather than full accounting. Teleports, repairs, incidental purchases, and unrelated gil changes are accepted blind spots; item-level sale outcomes are the primary signal.
- Phase 06 implemented durable bought-lot storage in `flip-ledger.json` with schema versioning and backup-on-write, plus mark-bought confirmation and an open positions correction view.
- Phase 06 kept automatic mutation below the automation ceiling: bought lots are created only from explicit user confirmation, and no sold-state or reconciliation automation exists yet.
- Upstream UI alpha self-heal from main was retained during conflict resolution, but periodic draw heartbeat logging remains removed.

### Pending Todos

- Phase 07 needs sold-state workflow design/implementation: actual sale price entry, close/partial-close behavior, and tax-adjusted realized profit.
- Keep Phase 07 manual and confirmation-based; assisted reconciliation waits for Phase 9/10 observability.

### Blockers/Concerns

- Phase 06 draft PR #1 should still get in-game UAT before merging because it changes route bought interactions and adds a durable ledger file.
- Local macOS compiler verification is intentionally replaced by source validation plus CI build verification.

## Session Continuity

Last session: 2026-06-13T05:59:00.000Z
Stopped at: Phase 6 implemented; draft PR #1 build passed
Next command: `$gsd-spec-phase 7` or continue implementing Phase 7 manual realized-profit tracking
