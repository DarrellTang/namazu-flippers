using NamazuFlippers.Core;
using Xunit;

namespace NamazuFlippers.Tests;

public class PriceResolutionTests
{
    // === ResolveSellPrice: median-when-corroborated, fallback otherwise ===

    [Fact]
    public void ResolveSellPrice_EnoughRecentSales_UsesRoundedMedian()
    {
        var (price, verified) = OpportunityScoring.ResolveSellPrice(
            saddlebagSellPrice: 649_685, recentMedianSalePrice: 50_000.4, recentSalesCount: 15, minRecentSalesToJudge: 3);

        Assert.True(verified);
        Assert.Equal(50_000, price);
    }

    [Fact]
    public void ResolveSellPrice_TooFewRecentSales_FallsBackToSaddlebagUnverified()
    {
        // Only 2 recent sales (< 3) — can't corroborate, so keep Saddlebag's average and flag it.
        var (price, verified) = OpportunityScoring.ResolveSellPrice(
            saddlebagSellPrice: 649_685, recentMedianSalePrice: 50_000, recentSalesCount: 2, minRecentSalesToJudge: 3);

        Assert.False(verified);
        Assert.Equal(649_685, price);
    }

    [Fact]
    public void ResolveSellPrice_NoMedianData_FallsBackUnverified()
    {
        var (price, verified) = OpportunityScoring.ResolveSellPrice(
            saddlebagSellPrice: 120_000, recentMedianSalePrice: 0.0, recentSalesCount: 9, minRecentSalesToJudge: 3);

        Assert.False(verified);
        Assert.Equal(120_000, price);
    }

    [Fact]
    public void ResolveSellPrice_ExactlyMinSales_Verifies()
    {
        var (_, verified) = OpportunityScoring.ResolveSellPrice(
            saddlebagSellPrice: 100_000, recentMedianSalePrice: 90_000, recentSalesCount: 3, minRecentSalesToJudge: 3);

        Assert.True(verified);
    }

    // The real Damaged Highland Turret case: 3 misclick sales at ~1M drag Saddlebag's mean to 649k,
    // but the median of the 15 recent sales is 50k. Correction picks the median → fluke collapses.
    [Fact]
    public void ResolveSellPrice_OutlierInflatedAverage_MedianWins()
    {
        var recent = new[] { 23000, 48000, 48000, 48000, 50000, 50000, 50000, 50000, 50000, 50000, 50057, 60000, 899000, 999999, 1000000 };
        var median = Median(recent);

        var (price, verified) = OpportunityScoring.ResolveSellPrice(
            saddlebagSellPrice: 649_685, recentMedianSalePrice: median, recentSalesCount: recent.Length, minRecentSalesToJudge: 3);

        Assert.True(verified);
        Assert.Equal(50_000, price);
    }

    // === NetProfitPerUnit: post-tax profit, and how correction flips admissibility ===

    [Theory]
    [InlineData(50_000, 7_900, 39_600)]   // corrected turret: floor(50000*0.95) - 7900
    [InlineData(649_685, 7_900, 609_300)] // fluke turret: floor(649685*0.95) - 7900
    [InlineData(10_000, 10_000, -500)]    // sells for less than buy after tax → negative
    public void NetProfitPerUnit_ComputesPostTaxProfit(int sell, int buy, int expected)
    {
        Assert.Equal(expected, OpportunityScoring.NetProfitPerUnit(sell, buy));
    }

    [Fact]
    public void NetProfitPerUnit_CorrectionDropsFlukeBelowFloor()
    {
        const int floor = 150_000;
        var flukeProfit = OpportunityScoring.NetProfitPerUnit(649_685, 7_900);  // uncorrected
        var realProfit = OpportunityScoring.NetProfitPerUnit(50_000, 7_900);    // median-corrected

        Assert.True(flukeProfit >= floor);   // would pass the floor with the fake average
        Assert.True(realProfit < floor);     // correctly fails it once the median is used
    }

    private static double Median(IReadOnlyList<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }
}
