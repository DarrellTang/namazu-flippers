using Xunit;

namespace NamazuFlippers.Tests;

public class ConfigurationDefaultsTests
{
    [Fact]
    public void Fresh_config_has_locked_tier1to3_defaults()
    {
        var c = new NamazuFlippers.Configuration();

        Assert.Equal(7, c.HoldingWindowDays);
        Assert.Equal(0.5, c.KellyFraction);
        Assert.True(c.EnableUniversalis);
        Assert.Equal(0.9, c.PriceCorroborationThreshold);
        Assert.Equal(3, c.MinRecentSalesToJudge);
    }

    [Fact]
    public void Fresh_config_has_expected_core_defaults()
    {
        var c = new NamazuFlippers.Configuration();

        Assert.Equal(25, c.PreferredRoi);
        Assert.Equal(10000, c.MinProfitAmount);
        Assert.Equal(1_000_000, c.MaxBudgetPerSession);
        Assert.Equal(10, c.MaxItemsPerSession);
        Assert.Equal(4, c.CacheDurationHours);
    }
}
