using NamazuFlippers.Core;
using Xunit;

namespace NamazuFlippers.Tests;

public class OpportunityScoringTests
{
    // === CapitalEfficiency ===

    [Fact]
    public void CapitalEfficiency_KnownValue_ComputesRatioTimesVelocity()
    {
        var result = OpportunityScoring.CapitalEfficiency(profitPerUnit: 5000, cheapestPrice: 10000, salesPerDay: 2.0);

        Assert.Equal(1.0, result, 6);
    }

    [Fact]
    public void CapitalEfficiency_ZeroPrice_ReturnsZero()
    {
        var result = OpportunityScoring.CapitalEfficiency(profitPerUnit: 5000, cheapestPrice: 0, salesPerDay: 2.0);

        Assert.Equal(0.0, result, 6);
    }

    [Fact]
    public void CapitalEfficiency_NegativePrice_ReturnsZero()
    {
        var result = OpportunityScoring.CapitalEfficiency(profitPerUnit: 5000, cheapestPrice: -10, salesPerDay: 2.0);

        Assert.Equal(0.0, result, 6);
    }

    [Theory]
    [InlineData(1000, 100, 3.0, 30.0)]
    [InlineData(200, 50, 1.5, 6.0)]
    [InlineData(0, 100, 5.0, 0.0)]
    public void CapitalEfficiency_TheoryRows(int profitPerUnit, int cheapestPrice, double salesPerDay, double expected)
    {
        var result = OpportunityScoring.CapitalEfficiency(profitPerUnit, cheapestPrice, salesPerDay);

        Assert.Equal(expected, result, 6);
    }

    // === ExpectedDemand ===

    [Fact]
    public void ExpectedDemand_KnownValue_MultipliesVelocityByWindow()
    {
        var result = OpportunityScoring.ExpectedDemand(salesPerDay: 2.0, holdingWindowDays: 7);

        Assert.Equal(14.0, result, 6);
    }

    [Fact]
    public void ExpectedDemand_NegativeSalesPerDay_FlooredAtZero()
    {
        var result = OpportunityScoring.ExpectedDemand(salesPerDay: -5.0, holdingWindowDays: 7);

        Assert.Equal(0.0, result, 6);
    }

    [Fact]
    public void ExpectedDemand_NegativeHoldingWindow_FlooredAtZero()
    {
        var result = OpportunityScoring.ExpectedDemand(salesPerDay: 2.0, holdingWindowDays: -3);

        Assert.Equal(0.0, result, 6);
    }

    // === SellConfidence ===

    [Fact]
    public void SellConfidence_ZeroDepth_ReturnsOne_EvenWithZeroDemand()
    {
        var result = OpportunityScoring.SellConfidence(expectedDemand: 0.0, depth: 0);

        Assert.Equal(1.0, result, 6);
    }

    [Fact]
    public void SellConfidence_PositiveDepth_ComputesRatio()
    {
        var result = OpportunityScoring.SellConfidence(expectedDemand: 14.0, depth: 6);

        Assert.Equal(0.7, result, 6);
    }

    [Fact]
    public void SellConfidence_NegativeDepth_ReturnsOne()
    {
        var result = OpportunityScoring.SellConfidence(expectedDemand: 14.0, depth: -3);

        Assert.Equal(1.0, result, 6);
    }

    [Fact]
    public void SellConfidence_HigherDepth_LowersConfidence_Monotonic()
    {
        var low = OpportunityScoring.SellConfidence(expectedDemand: 14.0, depth: 3);
        var mid = OpportunityScoring.SellConfidence(expectedDemand: 14.0, depth: 6);
        var high = OpportunityScoring.SellConfidence(expectedDemand: 14.0, depth: 12);

        Assert.True(low > mid);
        Assert.True(mid > high);
    }

    // === AbsorptionCap ===

    [Fact]
    public void AbsorptionCap_KnownValue_SubtractsDepth()
    {
        var result = OpportunityScoring.AbsorptionCap(expectedDemand: 14.0, depth: 6);

        Assert.Equal(8.0, result, 6);
    }

    [Fact]
    public void AbsorptionCap_DepthExceedsDemand_FlooredAtZero()
    {
        var result = OpportunityScoring.AbsorptionCap(expectedDemand: 5.0, depth: 20);

        Assert.Equal(0.0, result, 6);
    }

    [Fact]
    public void AbsorptionCap_ZeroDepth_EqualsExpectedDemand()
    {
        // Graceful-degradation path (criterion 5): depth unavailable ⇒ A = d_exp.
        var result = OpportunityScoring.AbsorptionCap(expectedDemand: 14.0, depth: 0);

        Assert.Equal(14.0, result, 6);
    }

    // === PriceConfidence ===

    [Fact]
    public void PriceConfidence_BelowMinRecentSales_ReturnsOne_EvenWithLowMedian()
    {
        // Median is far below what would otherwise discount, but count < min ⇒ neutral floor.
        var result = OpportunityScoring.PriceConfidence(
            recentMedianSalePrice: 100,
            recentSalesCount: 2,
            expectedSellPrice: 10000,
            corroborationThreshold: 0.9,
            minRecentSalesToJudge: 3);

        Assert.Equal(1.0, result, 6);
    }

    [Fact]
    public void PriceConfidence_RatioAtOrAboveThreshold_ReturnsOne()
    {
        var result = OpportunityScoring.PriceConfidence(
            recentMedianSalePrice: 9500,
            recentSalesCount: 10,
            expectedSellPrice: 10000,
            corroborationThreshold: 0.9,
            minRecentSalesToJudge: 3);

        Assert.Equal(1.0, result, 6);
    }

    [Fact]
    public void PriceConfidence_BelowThreshold_ScalesLinearly()
    {
        var result = OpportunityScoring.PriceConfidence(
            recentMedianSalePrice: 4500,
            recentSalesCount: 10,
            expectedSellPrice: 10000,
            corroborationThreshold: 0.9,
            minRecentSalesToJudge: 3);

        Assert.Equal(0.5, result, 6);
    }

    [Fact]
    public void PriceConfidence_ExactlyAtThreshold_IsContinuous()
    {
        // median == threshold * expected ⇒ ratio == threshold ⇒ >= branch ⇒ 1.0.
        var result = OpportunityScoring.PriceConfidence(
            recentMedianSalePrice: 9000,
            recentSalesCount: 10,
            expectedSellPrice: 10000,
            corroborationThreshold: 0.9,
            minRecentSalesToJudge: 3);

        Assert.Equal(1.0, result, 6);
    }

    [Fact]
    public void PriceConfidence_NonPositiveExpectedSellPrice_ReturnsOne()
    {
        var result = OpportunityScoring.PriceConfidence(
            recentMedianSalePrice: 100,
            recentSalesCount: 10,
            expectedSellPrice: 0,
            corroborationThreshold: 0.9,
            minRecentSalesToJudge: 3);

        Assert.Equal(1.0, result, 6);
    }

    [Fact]
    public void PriceConfidence_NonPositiveThreshold_ReturnsOne()
    {
        var result = OpportunityScoring.PriceConfidence(
            recentMedianSalePrice: 100,
            recentSalesCount: 10,
            expectedSellPrice: 10000,
            corroborationThreshold: 0.0,
            minRecentSalesToJudge: 3);

        Assert.Equal(1.0, result, 6);
    }

    [Theory]
    [InlineData(0, 10, 10000, 0.9, 3)]
    [InlineData(-500, 10, 10000, 0.9, 3)]
    [InlineData(double.MaxValue, 10, 10000, 0.9, 3)]
    [InlineData(100, 0, 10000, 0.9, 3)]
    [InlineData(100, -5, 10000, 0.9, 3)]
    [InlineData(100, 10, -10000, 0.9, 3)]
    public void PriceConfidence_NeverThrows_AndIsAlwaysBoundedZeroToOne(
        double recentMedianSalePrice,
        int recentSalesCount,
        int expectedSellPrice,
        double corroborationThreshold,
        int minRecentSalesToJudge)
    {
        // "Never a hard filter": no input combination should throw, and the result always
        // stays within the valid multiplier range.
        var result = OpportunityScoring.PriceConfidence(
            recentMedianSalePrice, recentSalesCount, expectedSellPrice, corroborationThreshold, minRecentSalesToJudge);

        Assert.InRange(result, 0.0, 1.0);
    }

    // === FinalRank ===

    [Fact]
    public void FinalRank_KnownTriple_IsProduct()
    {
        var result = OpportunityScoring.FinalRank(capitalEfficiency: 1.0, sellConfidence: 0.7, priceConfidence: 0.5);

        Assert.Equal(0.35, result, 6);
    }
}
