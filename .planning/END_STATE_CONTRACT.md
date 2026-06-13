# Namazu Flippers End-State Contract

**Created:** 2026-06-13
**Purpose:** Fresh-context handoff for completing Phases 6-10 with minimal human-in-the-loop ceremony.

## Product End State

Namazu Flippers should become a daily FFXIV arbitrage companion that:

- Finds a practical route of items to buy and list.
- Lets the user record actual bought lots with quantity and actual buy price.
- Lets the user later record what sold and for how much.
- Computes realized item-level profit tied to the original buy date.
- Shows enough history to answer whether the flipping workflow is making money.

This is not a full gil accounting application.

## Source of Truth

The authoritative profit source is the item-level flip ledger.

Gil totals, retainer gil totals, chat observations, or other game-observed signals may be used only as optional approximate sanity checks unless a later spike proves they are reliable enough for assisted reconciliation.

Teleport costs, repairs, unrelated purchases, rewards, market board taxes beyond the modeled sale tax, and incidental gil movement are accepted blind spots.

## Completion Bar For Phases 6-10

The remaining work is complete when the plugin supports:

- Durable bought lots independent of scan-cache expiry.
- Manual sold entry for bought/listed positions.
- Actual sale price capture.
- Realized profit calculation using FFXIV market tax: `floor(sale_price * 0.95) - actual_or_planned_buy_price`.
- Today, 7-day, and 30-day realized profit views.
- Open positions view.
- Sold history review by original buy date.
- Retainer/gil observability spike with evidence-backed go/no-go findings.
- Assisted reconciliation only if runtime evidence supports it.
- Updated source validation for the actual runtime-discovered behavior.
- CI remaining the authoritative compile/package gate.

## Automation Ceiling

Recommended/default rule: suggest matches with confirmation only.

The plugin may suggest sale matches when confidence is high, but it should not silently close or mutate ledger records from observed game signals unless explicitly approved in a later product decision.

## Ambiguous Sales

Default matching rule: oldest listed matching lot first, visibly correctable.

If multiple open lots could match a sale, the plugin may preselect the oldest listed lot, but the user must be able to correct the match.

## Historical Correction

Records remain editable/deleteable indefinitely.

This is a personal tool, not an auditable accounting system. Old buys and sales can be corrected if the user discovers a mistake.

## Autonomy Rule

The agent may proceed through Phases 6-10 without asking for every implementation decision.

Ask the user only when a decision:

- Risks data loss or irreversible history mutation.
- Changes the product contract in this file.
- Enables automation that could close, match, or mutate records incorrectly.
- Adds significant daily-use friction.
- Depends on uncertain live-game observability findings.

The agent should choose defaults for:

- File layout and naming.
- Internal abstractions.
- Validation script structure.
- UI layout details that follow existing project patterns.
- Error handling mechanics that preserve user data and keep the workflow usable.

## Phase Guidance

### Phase 6: Runtime Hardening & Ledger Foundation

Build reliable persistence and durable bought-lot records. Include mark-bought with quantity and actual unit buy price confirmation, plus a minimal open-position correction/debug view.

Do not build sold-entry or profit-history workflows here.

### Phase 7: Manual Realized Profit Tracking

Add manual sold-state workflow, actual sale price entry, tax-adjusted realized profit, and close/partial-close behavior for lots.

### Phase 8: Profit History UI

Show realized profit for today, 7 days, and 30 days. Show open positions and sold history grouped or filterable by original buy date. Clearly separate projected profit from realized profit.

### Phase 9: Retainer/Gil Detection Spike

Investigate what Dalamud can reliably observe from retainers, gil totals, chat, sale history, or other safe runtime signals. Produce a go/no-go recommendation with evidence and blind spots.

### Phase 10: Assisted Reconciliation & Polish

If Phase 9 finds reliable signals, add confirmation-based assisted reconciliation. If only approximate signals exist, label them as approximate. Finish release-quality edge cases and validation.

## Non-Goals

- Full accounting across all income and expenses.
- Automatic ledger mutation from uncertain game-observed signals.
- Undercut monitoring or relisting alerts.
- Background market polling.
- Shortage predictor or additional opportunity sources before the profit-history loop is trustworthy.
- Multi-character or multi-home-world support.

## Fresh Chat Instruction

In a new chat, load this file first, then load:

- `.planning/ROADMAP.md`
- `.planning/REQUIREMENTS.md`
- `.planning/STATE.md`
- `.planning/phases/06-runtime-hardening-ledger-foundation/06-SPEC.md`
- `.planning/phases/06-runtime-hardening-ledger-foundation/06-CONTEXT.md`

Proceed autonomously within the Autonomy Rule above.
