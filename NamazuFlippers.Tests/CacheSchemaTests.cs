using NamazuFlippers.Data;
using Xunit;

namespace NamazuFlippers.Tests;

public class CacheSchemaTests
{
    [Fact]
    public void Current_version_is_3() => Assert.Equal(3, CacheSchema.CurrentVersion);

    [Fact]
    public void Current_schema_is_accepted() => Assert.True(CacheSchema.IsCurrent(3));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]   // the v2 envelope that must be treated as stale, not misread (criterion 11)
    [InlineData(4)]
    public void Non_current_schema_is_rejected(int version) => Assert.False(CacheSchema.IsCurrent(version));
}
