# Phase 6: Runtime Hardening & Ledger Foundation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-13
**Phase:** 6-Runtime Hardening & Ledger Foundation
**Areas discussed:** Ledger creation timing, Position identity and duplicates, Persistence boundary, Hardening behavior during scans, Phase 6 visible surface

---

## Ledger Creation Timing

| Option | Description | Selected |
|--------|-------------|----------|
| When bought | Only items actually bought enter history, keeping the ledger tied to real action. | ✓ |
| When listed | Tracks only items that made it to market, but loses buy-side history if listing is delayed. | |
| When routed | Captures every recommendation, but creates cleanup work for skipped items. | |

**User's choice:** When bought.
**Notes:** The ledger should represent real actions, not every opportunity shown by the scanner.

---

## Position Identity and Duplicates

| Option | Description | Selected |
|--------|-------------|----------|
| Separate lots by purchase | Each buy action creates its own position with its own buy date, quantity, and expected profit. | ✓ |
| Merge by item and world | Repeated buys of the same item from the same world become one averaged position. | |
| Merge by item only | All buys of the same item collapse together, regardless of source world. | |

**User's choice:** Track quantities for purchased and listed copies; duplicate items are expected when margins and sell-through look good.
**Notes:** Follow-up decisions: one lot per buy action; track bought/listed/sold/remaining counters; default future sale matching to oldest listed matching lot unless overridden.

---

## Persistence Boundary

| Option | Description | Selected |
|--------|-------------|----------|
| Purchase-side foundation only | Persist item, quantity bought, buy date/time, source world, unit buy price, expected sell price, and notes/status. | ✓ |
| Purchase plus listing fields | Include list date, listed quantity, listed unit price, and retainer when known. | |
| Full profit fields now | Include purchase, listing, sale, fees, and net profit fields in Phase 6. | |

**User's choice:** Purchase-side foundation only.
**Notes:** Include quantity lifecycle fields in the schema now, keep history indefinitely, and use versioned schema plus backup-on-write.

---

## Hardening Behavior During Scans

| Option | Description | Selected |
|--------|-------------|----------|
| Keep partial results with clear warnings | Show usable results, mark incomplete/stale parts, and avoid crashing. | ✓ |
| Fail the whole scan | Discard all results unless every requested world/item succeeds. | |
| Silently skip bad data | Keep the UI clean but omit failed worlds/items without prominent warning. | |

**User's choice:** Keep partial results with clear warnings.
**Notes:** Warnings should be inline but compact. Transient failures should retry lightly with bounded backoff, then continue partial. Diagnostics should be structured summaries.

---

## Phase 6 Visible Surface

| Option | Description | Selected |
|--------|-------------|----------|
| Minimal foundation UI | Mark bought from route results plus a simple open positions/debug view. | ✓ |
| No new user-facing UI | Implement only storage and hardening foundation with tests/logs. | |
| Full ledger screen now | Build a polished positions/history UI in Phase 6. | |

**User's choice:** Minimal foundation UI.
**Notes:** Mark bought should confirm quantity and actual unit buy price. The simple view should show open lots and allow basic edits/delete for mistakes.

## the agent's Discretion

No areas were explicitly delegated with "you decide."

## Deferred Ideas

- Manual sold-state and sale-price entry: Phase 7.
- Historical realized-profit UI: Phase 8.
- Retainer/gil observability and assisted reconciliation: Phase 9/10.
- Shortage predictor and opportunity expansion: backlog.
