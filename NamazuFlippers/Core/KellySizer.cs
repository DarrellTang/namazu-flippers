namespace NamazuFlippers.Core;

/// <summary>
/// Absorption-capped half-Kelly position sizing (ADR-0002). Pure — no Dalamud or I/O — so the
/// allocation, under-deploy, and graceful-degradation paths are unit-testable on fixtures.
///
/// Each opportunity is allocated a gil target proportional to its Kelly weight
/// (edge × sellConfidence × priceConfidence) scaled by the Kelly fraction, then converted to an
/// integer buy quantity bounded by (a) its absorption cap and (b) the remaining budget. Total
/// deployment can fall below the pool when absorption is the binding constraint — that under-deploy
/// is the intended signal, not a bug.
/// </summary>
public static class KellySizer
{
    /// <summary>
    /// Assigns <see cref="RankedOpportunity.RecommendedQuantity"/> in place, walking the list in
    /// the order given (expected to be final-rank order). Weights use each opportunity's already-set
    /// SellConfidence / PriceConfidence / AbsorptionCap, so graceful degradation (depth = 0,
    /// PriceConfidence = 1) falls out automatically — sizing becomes velocity-only.
    /// </summary>
    /// <param name="ranked">Opportunities in rank order; mutated with recommended quantities.</param>
    /// <param name="budgetPool">Kelly capital pool in gil (MaxBudgetPerSession). ≤ 0 ⇒ no capital,
    /// so every recommended quantity is 0.</param>
    /// <param name="kellyFraction">Fraction of full Kelly to deploy (0.5 = half-Kelly).</param>
    public static void AssignQuantities(
        IReadOnlyList<RankedOpportunity> ranked,
        long budgetPool,
        double kellyFraction)
    {
        ArgumentNullException.ThrowIfNull(ranked);

        if (ranked.Count == 0)
            return;

        // MaxBudgetPerSession is the Kelly capital pool (criterion 6). A non-positive pool is no
        // capital, so nothing deploys — every position is bounded by the (zero) remaining budget.
        if (budgetPool <= 0)
        {
            foreach (var opportunity in ranked)
                opportunity.RecommendedQuantity = 0;
            return;
        }

        var fraction = Math.Max(0.0, kellyFraction);

        var weights = new double[ranked.Count];
        var totalWeight = 0.0;
        for (var i = 0; i < ranked.Count; i++)
        {
            weights[i] = KellyWeight(ranked[i]);
            totalWeight += weights[i];
        }

        var remainingBudget = budgetPool;

        for (var i = 0; i < ranked.Count; i++)
        {
            var opportunity = ranked[i];
            var price = opportunity.PurchasePrice;
            var absorptionUnits = (long)Math.Floor(Math.Max(0.0, opportunity.AbsorptionCap));

            long quantity;
            if (price <= 0 || absorptionUnits <= 0 || totalWeight <= 0.0 || weights[i] <= 0.0)
            {
                quantity = 0;
            }
            else
            {
                // Kelly gil target for this position: its weight share of the pool, at the Kelly
                // fraction. Converted to whole units, then bounded by absorption and the budget.
                var targetGil = fraction * (weights[i] / totalWeight) * budgetPool;
                var quantityByGil = (long)Math.Floor(targetGil / price);

                quantity = Math.Min(quantityByGil, absorptionUnits);

                // Never spend past what's left in the pool (keeps total deployment ≤ budget).
                var quantityByRemaining = (long)Math.Floor(remainingBudget / (double)price);
                quantity = Math.Min(quantity, quantityByRemaining);
                quantity = Math.Max(0, quantity);
            }

            opportunity.RecommendedQuantity = (int)Math.Min(quantity, int.MaxValue);

            if (quantity > 0)
            {
                var spent = quantity * (long)price;
                remainingBudget = Math.Max(0, remainingBudget - spent);
            }
        }
    }

    /// <summary>
    /// Total gil the recommended quantities deploy: Σ quantity × purchasePrice. May be less than the
    /// budget pool when absorption-limited.
    /// </summary>
    public static long TotalDeployedGil(IReadOnlyList<RankedOpportunity> ranked)
    {
        ArgumentNullException.ThrowIfNull(ranked);

        long total = 0;
        foreach (var opportunity in ranked)
            total += (long)opportunity.RecommendedQuantity * opportunity.PurchasePrice;
        return total;
    }

    /// <summary>
    /// Total gil the absorption ceiling would allow: Σ floor(absorptionCap) × purchasePrice. The
    /// upper bound the market can absorb, shown alongside deployed/budget in the session summary.
    /// </summary>
    public static long TotalAbsorptionCeilingGil(IReadOnlyList<RankedOpportunity> ranked)
    {
        ArgumentNullException.ThrowIfNull(ranked);

        long total = 0;
        foreach (var opportunity in ranked)
            total += (long)Math.Floor(Math.Max(0.0, opportunity.AbsorptionCap)) * opportunity.PurchasePrice;
        return total;
    }

    // Kelly weight = edge × sellConfidence × priceConfidence, where edge is the per-flip return
    // fraction profitPerUnit / purchasePrice. Clamped at 0 (no negative bets).
    private static double KellyWeight(RankedOpportunity opportunity)
    {
        if (opportunity.PurchasePrice <= 0)
            return 0.0;

        var edge = (double)opportunity.ProfitPerUnit / opportunity.PurchasePrice;
        var weight = edge * opportunity.SellConfidence * opportunity.PriceConfidence;
        return Math.Max(0.0, weight);
    }
}
