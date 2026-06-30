using NamazuFlippers.Core;
using Xunit;

namespace NamazuFlippers.Tests;

public class KellySizerTests
{
    private static RankedOpportunity MakeOpportunity(
        int profitPerUnit,
        int purchasePrice,
        double sellConfidence = 1.0,
        double priceConfidence = 1.0,
        double absorptionCap = 0.0)
    {
        return new RankedOpportunity
        {
            ProfitPerUnit = profitPerUnit,
            PurchasePrice = purchasePrice,
            SellConfidence = sellConfidence,
            PriceConfidence = priceConfidence,
            AbsorptionCap = absorptionCap,
        };
    }

    // === Basic allocation ===

    [Fact]
    public void AssignQuantities_SingleOpportunity_GetsFullWeightShareOfKellyTarget()
    {
        // Sole opportunity ⇒ weight share = 1.0, so target gil = kellyFraction × budgetPool.
        var opp = MakeOpportunity(profitPerUnit: 500, purchasePrice: 1000, absorptionCap: 1000);
        var ranked = new[] { opp };

        KellySizer.AssignQuantities(ranked, budgetPool: 100_000, kellyFraction: 0.5);

        // floor(0.5 * 100_000 / 1000) = 50
        Assert.Equal(50, opp.RecommendedQuantity);
    }

    // === Absorption cap binds (under-deploy) ===

    [Fact]
    public void AssignQuantities_SmallAbsorptionCap_BindsBelowKellyTarget_AndUnderDeploys()
    {
        var opp = MakeOpportunity(profitPerUnit: 50, purchasePrice: 100, absorptionCap: 3);
        var ranked = new[] { opp };

        KellySizer.AssignQuantities(ranked, budgetPool: 1_000_000, kellyFraction: 0.5);

        Assert.Equal(3, opp.RecommendedQuantity);
        Assert.Equal((int)Math.Floor(opp.AbsorptionCap), opp.RecommendedQuantity);
        Assert.True(KellySizer.TotalDeployedGil(ranked) < 1_000_000);
    }

    // === Budget exhausted across two items ===

    [Fact]
    public void AssignQuantities_TinyBudget_SecondItemCappedByRemainingBudget()
    {
        // Weight share A = 0.75, B = 0.25 (edge = profit/price, sellConf = priceConf = 1).
        // kellyFraction > 1 makes the sum of continuous Kelly targets exceed the budget pool,
        // so A is satisfied in full but B is then capped by whatever remains.
        // All values are dyadic (powers of two) so the floating-point math is exact, avoiding
        // floor() rounding artifacts from binary-fraction drift.
        var oppA = MakeOpportunity(profitPerUnit: 3, purchasePrice: 4, absorptionCap: 1000);
        var oppB = MakeOpportunity(profitPerUnit: 1, purchasePrice: 4, absorptionCap: 1000);
        var ranked = new[] { oppA, oppB };

        KellySizer.AssignQuantities(ranked, budgetPool: 128, kellyFraction: 1.25);

        // A: targetGil = 1.25 * 0.75 * 128 = 120 -> floor(120/4) = 30 (own target, budget allows it).
        Assert.Equal(30, oppA.RecommendedQuantity);
        // B: own target = floor(1.25 * 0.25 * 128 / 4) = floor(40/4) = 10, but only 8 gil remains
        // after A spent 120 of the 128 pool, so remaining caps it to floor(8/4) = 2.
        Assert.Equal(2, oppB.RecommendedQuantity);

        var totalSpent = KellySizer.TotalDeployedGil(ranked);
        Assert.True(totalSpent <= 128);
    }

    // === Graceful degradation (criterion 8) ===

    [Fact]
    public void AssignQuantities_GracefulDegradation_VelocityOnlySizing_HigherEdgeGetsMoreOrEqual()
    {
        // SellConfidence/PriceConfidence neutral (as when depth = 0 / Universalis unavailable);
        // absorption and budget generous enough not to bind, so sizing is purely edge-driven.
        var highEdge = MakeOpportunity(profitPerUnit: 8, purchasePrice: 10, absorptionCap: 1000);
        var lowEdge = MakeOpportunity(profitPerUnit: 2, purchasePrice: 10, absorptionCap: 1000);
        var ranked = new[] { highEdge, lowEdge };

        KellySizer.AssignQuantities(ranked, budgetPool: 10_000, kellyFraction: 0.5);

        Assert.True(highEdge.RecommendedQuantity > 0);
        Assert.True(lowEdge.RecommendedQuantity > 0);
        Assert.True(highEdge.RecommendedQuantity >= lowEdge.RecommendedQuantity);
        // Exact values: weight share High = 0.8, Low = 0.2.
        Assert.Equal(400, highEdge.RecommendedQuantity); // floor(0.5*0.8*10000/10)
        Assert.Equal(100, lowEdge.RecommendedQuantity);  // floor(0.5*0.2*10000/10)
    }

    // === Zero/empty guards ===

    [Fact]
    public void AssignQuantities_EmptyList_DoesNotThrow()
    {
        var ranked = Array.Empty<RankedOpportunity>();

        var exception = Record.Exception(() => KellySizer.AssignQuantities(ranked, budgetPool: 1000, kellyFraction: 0.5));

        Assert.Null(exception);
    }

    [Fact]
    public void AssignQuantities_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KellySizer.AssignQuantities(null!, budgetPool: 1000, kellyFraction: 0.5));
    }

    [Fact]
    public void AssignQuantities_ZeroPurchasePrice_QuantityIsZero()
    {
        var opp = MakeOpportunity(profitPerUnit: 10, purchasePrice: 0, absorptionCap: 100);
        var ranked = new[] { opp };

        KellySizer.AssignQuantities(ranked, budgetPool: 1000, kellyFraction: 0.5);

        Assert.Equal(0, opp.RecommendedQuantity);
    }

    [Fact]
    public void AssignQuantities_ZeroAbsorptionCap_QuantityIsZero()
    {
        var opp = MakeOpportunity(profitPerUnit: 5, purchasePrice: 10, absorptionCap: 0);
        var ranked = new[] { opp };

        KellySizer.AssignQuantities(ranked, budgetPool: 1000, kellyFraction: 0.5);

        Assert.Equal(0, opp.RecommendedQuantity);
    }

    [Fact]
    public void AssignQuantities_NoBudgetCap_BoundedOnlyByAbsorption()
    {
        var opp = MakeOpportunity(profitPerUnit: 5, purchasePrice: 10, absorptionCap: 37.6);
        var ranked = new[] { opp };

        KellySizer.AssignQuantities(ranked, budgetPool: 0, kellyFraction: 0.5);

        Assert.Equal(37, opp.RecommendedQuantity);
    }

    [Fact]
    public void AssignQuantities_NegativeBudgetPool_TreatedAsNoCap_BoundedOnlyByAbsorption()
    {
        var opp = MakeOpportunity(profitPerUnit: 5, purchasePrice: 10, absorptionCap: 37.6);
        var ranked = new[] { opp };

        KellySizer.AssignQuantities(ranked, budgetPool: -100, kellyFraction: 0.5);

        Assert.Equal(37, opp.RecommendedQuantity);
    }

    // === TotalDeployedGil / TotalAbsorptionCeilingGil ===

    [Fact]
    public void TotalDeployedGil_SumsQuantityTimesPrice()
    {
        var oppA = MakeOpportunity(profitPerUnit: 0, purchasePrice: 10, absorptionCap: 15.7);
        oppA.RecommendedQuantity = 5;
        var oppB = MakeOpportunity(profitPerUnit: 0, purchasePrice: 20, absorptionCap: 3.2);
        oppB.RecommendedQuantity = 2;
        var ranked = new[] { oppA, oppB };

        var deployed = KellySizer.TotalDeployedGil(ranked);

        Assert.Equal(5 * 10 + 2 * 20, deployed);
    }

    [Fact]
    public void TotalAbsorptionCeilingGil_SumsFlooredCapTimesPrice()
    {
        var oppA = MakeOpportunity(profitPerUnit: 0, purchasePrice: 10, absorptionCap: 15.7);
        var oppB = MakeOpportunity(profitPerUnit: 0, purchasePrice: 20, absorptionCap: 3.2);
        var ranked = new[] { oppA, oppB };

        var ceiling = KellySizer.TotalAbsorptionCeilingGil(ranked);

        // floor(15.7) * 10 + floor(3.2) * 20 = 150 + 60 = 210
        Assert.Equal(210, ceiling);
    }

    [Fact]
    public void TotalDeployedGil_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => KellySizer.TotalDeployedGil(null!));
    }

    [Fact]
    public void TotalAbsorptionCeilingGil_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => KellySizer.TotalAbsorptionCeilingGil(null!));
    }
}
