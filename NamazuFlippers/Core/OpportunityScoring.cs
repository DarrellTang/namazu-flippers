namespace NamazuFlippers.Core;

/// <summary>
/// Pure scoring functions for the profit-per-gil ranking. No Dalamud or I/O dependencies,
/// so they can be unit-tested directly on hand-built fixtures (see NamazuFlippers.Tests).
/// All inputs are primitives; callers pass values off <see cref="RankedOpportunity"/> /
/// <see cref="Configuration"/>.
/// </summary>
public static class OpportunityScoring
{
    /// <summary>FFXIV market board retainer fee: 5% is taken on every sale, so a unit nets 95%.</summary>
    public const double MarketTaxRate = 0.95;

    /// <summary>
    /// Resolves the expected sell price. Prefers the outlier-robust <b>median</b> of recent
    /// home-world Universalis sales when at least <paramref name="minRecentSalesToJudge"/> are
    /// available; otherwise falls back to Saddlebag's average and reports the price as unverified.
    /// The median is immune to the occasional 1M-gil misclick sale that inflates Saddlebag's mean.
    /// </summary>
    /// <returns>The sell price to use, and whether it was corroborated by recent sales.</returns>
    public static (int SellPrice, bool Verified) ResolveSellPrice(
        int saddlebagSellPrice,
        double recentMedianSalePrice,
        int recentSalesCount,
        int minRecentSalesToJudge)
    {
        if (recentSalesCount >= minRecentSalesToJudge && recentMedianSalePrice > 0)
            return ((int)Math.Round(recentMedianSalePrice, MidpointRounding.AwayFromZero), true);

        return (saddlebagSellPrice, false);
    }

    /// <summary>
    /// Post-tax profit per unit: floor(expectedSellPrice × 0.95) − purchasePrice, clamped to int.
    /// </summary>
    public static int NetProfitPerUnit(int expectedSellPrice, int purchasePrice)
    {
        var net = Math.Floor(expectedSellPrice * MarketTaxRate) - purchasePrice;
        if (net >= int.MaxValue)
            return int.MaxValue;
        if (net <= int.MinValue)
            return int.MinValue;
        return (int)net;
    }

    /// <summary>
    /// CapitalEfficiency = (profitPerUnit / cheapestPrice) × salesPerDay — the velocity-aware
    /// return-per-gil-per-day that ranks opportunities (ADR-0001). Zero when price is non-positive.
    /// </summary>
    public static double CapitalEfficiency(int profitPerUnit, int cheapestPrice, double salesPerDay)
    {
        if (cheapestPrice <= 0)
            return 0.0;

        return ((double)profitPerUnit / cheapestPrice) * salesPerDay;
    }

    /// <summary>
    /// Expected demand over the holding window: d_exp = salesPerDay × holdingWindowDays.
    /// Both factors are floored at 0.
    /// </summary>
    public static double ExpectedDemand(double salesPerDay, int holdingWindowDays) =>
        Math.Max(0.0, salesPerDay) * Math.Max(0, holdingWindowDays);

    /// <summary>
    /// Sell confidence c = d_exp / (d_exp + depth). A depth of 0 (no competing listings, or
    /// listing data unavailable) yields c = 1 — no penalty (CONTEXT § Sell Confidence).
    /// </summary>
    public static double SellConfidence(double expectedDemand, int depth)
    {
        if (depth <= 0)
            return 1.0;

        var denominator = expectedDemand + depth;
        if (denominator <= 0.0)
            return 1.0;

        return expectedDemand / denominator;
    }

    /// <summary>
    /// Absorption cap A = max(0, d_exp − depth): the unit ceiling the home market can clear within
    /// the holding window. When depth is unavailable callers pass depth = 0, giving A = d_exp.
    /// </summary>
    public static double AbsorptionCap(double expectedDemand, int depth) =>
        Math.Max(0.0, expectedDemand - Math.Max(0, depth));

    /// <summary>
    /// Price confidence (persistence): a 0–1 multiplier that discounts rank and size when the
    /// recent home-world median sale price falls below corroborationThreshold × expectedSellPrice.
    /// Fewer than minRecentSalesToJudge recent sales ⇒ neutral 1.0. Never a hard filter.
    /// </summary>
    public static double PriceConfidence(
        double recentMedianSalePrice,
        int recentSalesCount,
        int expectedSellPrice,
        double corroborationThreshold,
        int minRecentSalesToJudge)
    {
        // Too little recent evidence to judge — stay neutral.
        if (recentSalesCount < minRecentSalesToJudge)
            return 1.0;

        if (expectedSellPrice <= 0 || corroborationThreshold <= 0.0)
            return 1.0;

        var ratio = recentMedianSalePrice / expectedSellPrice;

        // At or above the corroboration threshold the expected price is supported — no discount.
        if (ratio >= corroborationThreshold)
            return 1.0;

        // Below threshold: scale linearly down to 0 as the recent median collapses. Continuous
        // at the threshold (ratio/threshold = 1 when ratio == threshold).
        return Math.Clamp(ratio / corroborationThreshold, 0.0, 1.0);
    }

    /// <summary>
    /// Final ranking key = CapitalEfficiency × SellConfidence × PriceConfidence
    /// (criterion 3 / ADR-0001).
    /// </summary>
    public static double FinalRank(double capitalEfficiency, double sellConfidence, double priceConfidence) =>
        capitalEfficiency * sellConfidence * priceConfidence;
}
