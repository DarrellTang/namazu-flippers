# Phase 1: Plugin Shell & Configuration - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-05-04
**Phase:** 01-plugin-shell
**Areas discussed:** Plugin identity/command, Configuration storage, Project structure, First-run UX

---

## Plugin Identity & Command

| Option | Description | Selected |
|--------|-------------|----------|
| Saddlebag Arbitrage / /saddlebag | Display: "Saddlebag Arbitrage", Command: /saddlebag | |
| Saddlebag / /saddlebag | Display: "Saddlebag", Shorter brand-focused | |
| Saddlebag Arbitrage / /pbag | Display: "Saddlebag Arbitrage", Command: /pbag (short but cryptic) | |
| **Namazu Flippers / /nflip** | FFXIV-themed (Namazu beast tribe + "flipping"), short command | ✓ |

**User's choice:** Namazu Flippers with `/nflip` command, PascalCase namespace `NamazuFlippers`
**Notes:** User brainstormed FFXIV-lore names. Namazu are the commerce-obsessed fish beast tribe from Stormblood. Double pun (fish + flipping items). Repository renamed from `saddlebag-arbitrage` to `namazu-flippers`.

---

## Configuration Storage

| Option | Description | Selected |
|--------|-------------|----------|
| **Dalamud built-in config** | `DalamudPluginInterface.SavePluginConfig<T>()`, standard plugin pattern | ✓ |
| Custom JSON file | System.Text.Json, full control over path/format | |

**User's choice:** Dalamud built-in config serialization
**Notes:** Standard pattern used by most Dalamud plugins. Less boilerplate. Config model covers CONF-01 through CONF-09.

---

## Project Structure

| Option | Description | Selected |
|--------|-------------|----------|
| **Minimal scaffold** | Only Phase 1 files, add folders as each phase introduces them | ✓ |
| Full layout from day one | Create Core/, API/, Data/, UI/, Integration/ now | |

**User's choice:** Minimal scaffold — expand per phase
**Notes:** Cleaner git history. No empty directories. SPEC.md layout serves as a reference target.

---

## First-Run Home World Selection

| Option | Description | Selected |
|--------|-------------|----------|
| **Simple ImGui popup** | Small window with world name input, auto-dismisses once set | ✓ |
| Chat message prompt | Text-based instructions like `/nflip home Adamantoise` | |
| Auto-open minimal config | Barebones ImGui window preview of full ConfigWindow | |

**User's choice:** Simple ImGui popup
**Notes:** Lightweight, appears once on first run. Consistent with eventual ConfigWindow style in Phase 4.

---

## the agent's Discretion

- Exact ImGui popup layout
- Config model class design (property types, defaults, validation)
- Plugin entry point boilerplate structure
- .csproj and manifest.json template details
- Namespace organization

## Deferred Ideas

- Full ConfigWindow with all settings controls → Phase 4
- All other plugin features → Phases 2-7
- Repo rename completed during discussion
