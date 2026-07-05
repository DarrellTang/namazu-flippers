using System.Collections;
using System.Reflection;
using Xunit;
using Cfg = NamazuFlippers.Configuration;

namespace NamazuFlippers.Tests;

/// <summary>
/// Guards the classic "added a setting but forgot to wire it into save/restore" bug. The
/// round-trip test walks every public setting by reflection, so a new field that is missed by
/// Snapshot or RestoreFrom fails automatically — no per-field test to remember to add.
/// </summary>
public class ConfigurationPersistenceTests
{
    private static IEnumerable<PropertyInfo> SettingProps() =>
        typeof(Cfg).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   .Where(p => p.CanRead && p.CanWrite);

    // Every setting set to a distinct, non-default value so a dropped field is detectable.
    private static Cfg Mutated() => new()
    {
        Version = 99,
        HomeWorld = "Behemoth",
        PreferredRoi = 42,
        MinProfitAmount = 123456,
        MinDesiredAvgPpu = 65432,
        MaxBudgetPerSession = 7_777_777,
        MinSalesPerDay = 1.25,
        MinSalesPerWeek = 9,
        RegionWide = true,
        CategoryFilters = [11, 22, 33],
        PreferredCategories = ["A", "B"],
        IncludeVendors = false,
        ShowOutOfStock = false,
        MaxItemsPerSession = 17,
        MaxServersToVisit = 8,
        CacheDurationHours = 13,
        EnableShortagePredictor = true,
        HoldingWindowDays = 21,
        KellyFraction = 0.33,
        EnableUniversalis = false,
        PriceCorroborationThreshold = 0.77,
        MinRecentSalesToJudge = 6,
    };

    private static bool ValuesEqual(object? a, object? b) =>
        a is IEnumerable ea and not string
            ? b is IEnumerable eb && ea.Cast<object>().SequenceEqual(eb.Cast<object>())
            : Equals(a, b);

    [Fact]
    public void Mutated_gives_every_setting_a_non_default_value()
    {
        // Protects the round-trip test: if a new setting is added but not mutated here it would
        // stay at its default and silently pass round-trip even if dropped from Snapshot/Restore.
        var def = new Cfg();
        var mut = Mutated();

        foreach (var p in SettingProps())
            Assert.False(ValuesEqual(p.GetValue(def), p.GetValue(mut)),
                $"{p.Name} was not given a non-default value in Mutated()");
    }

    [Fact]
    public void Snapshot_then_RestoreFrom_round_trips_every_setting()
    {
        var original = Mutated();
        var snapshot = Cfg.Snapshot(original);

        var target = new Cfg();
        Cfg.RestoreFrom(snapshot, target);

        foreach (var p in SettingProps())
            Assert.True(ValuesEqual(p.GetValue(original), p.GetValue(target)),
                $"{p.Name} did not round-trip through Snapshot/RestoreFrom");
    }

    [Fact]
    public void Snapshot_deep_copies_every_array_setting()
    {
        var original = Mutated();
        var snapshot = Cfg.Snapshot(original);

        // Every mutable array setting (CategoryFilters, PreferredCategories, and any future one)
        // must be an independent copy, not a shared reference.
        foreach (var p in SettingProps().Where(p => typeof(Array).IsAssignableFrom(p.PropertyType)))
        {
            var orig = (Array)p.GetValue(original)!;
            var snap = (Array)p.GetValue(snapshot)!;

            Assert.NotSame(orig, snap);   // a shared reference would be a shallow copy

            // Mutating the original must not reach into the snapshot's copy.
            var snapFirstBefore = snap.GetValue(0);
            var elemType = p.PropertyType.GetElementType()!;
            object sentinel = elemType == typeof(string) ? "__mutated__" : Convert.ChangeType(-12345, elemType);
            orig.SetValue(sentinel, 0);

            Assert.True(Equals(snapFirstBefore, snap.GetValue(0)),
                $"{p.Name} was shallow-copied by Snapshot (mutating the original changed the snapshot)");
        }
    }

    [Fact]
    public void RestoreDefaults_resets_every_tunable_and_preserves_identity_fields()
    {
        // RestoreDefaults resets search/route/cache preferences; it intentionally leaves the
        // player-identity/migration fields alone. Assert every other setting equals a fresh default.
        var preserved = new HashSet<string> { nameof(Cfg.HomeWorld), nameof(Cfg.Version) };

        var c = Mutated();
        Cfg.RestoreDefaults(c);
        var def = new Cfg();

        foreach (var p in SettingProps())
        {
            if (preserved.Contains(p.Name))
                continue;
            Assert.True(ValuesEqual(p.GetValue(def), p.GetValue(c)),
                $"{p.Name} was not reset to its default by RestoreDefaults");
        }

        Assert.Equal("Behemoth", c.HomeWorld);   // player identity is preserved, not reset
        Assert.Equal(99, c.Version);             // migration version is preserved, not reset
    }
}
