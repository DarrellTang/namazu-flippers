# Size positions by market absorption (ADV participation), bounded half-Kelly

Per-opportunity quantity is sized as `min(half-Kelly edge-weighted share of the session
budget, absorption cap)`, where the **absorption cap** = `sales per day × holding window
− competing listings` (an average-daily-volume participation limit). We deliberately do
**not** detect the player's total gil.

**Why:** with a large bankroll, the binding constraint is not the player's wallet but how
much capital the market can absorb at acceptable velocity before gil is stuck — the
classic strategy-capacity / market-impact problem. The accepted real-world playbook is
`size = min(Kelly desired, ADV participation cap)`; we adopt it. Full Kelly over-bets
when win-probabilities are uncertain (they are, from noisy sales data), so half-Kelly.
Treating the session budget as the capital pool avoids reintroducing gil tracking, which
was deliberately deprioritized.

**Trade-off:** the tool may recommend deploying *less* than the configured budget when the
market is thin (intended — it surfaces the absorption ceiling). The volume proxy
(`SalesPerDay`) and holding window are domain mappings to be calibrated against the
realized-profit ledger, not treated as exact.

**Boundary recorded:** player gil is not read at runtime; sizing is relative to the
session budget, not true wealth.
