namespace NamazuFlippers;

/// <summary>
/// Dalamud-free half of <see cref="Configuration"/>: every persisted setting, its default, and
/// the snapshot / restore / reset logic. Split out from the IPluginConfiguration marker (in
/// Configuration.cs) so this logic is unit-testable in isolation — a setting that is added here
/// but not wired into all three of Snapshot/RestoreFrom/RestoreDefaults is caught by
/// ConfigurationPersistenceTests. See docs/dual-agent-review/VERIFICATION-POLICY.md.
/// </summary>
public partial class Configuration
{
    // === Named Category Presets ===
    // These constants map to Saddlebag category/subcategory IDs.
    // Application code should reference these presets, not raw IDs.
    // See: https://github.com/ff14-advanced-market-search/saddlebag-with-pockets/wiki/Item-categories-ids-and-list

    public static readonly int[] FurnitureIds = { 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 };
    public static readonly int[] CollectibleIds = { 75, 80, 90 };
    public static readonly int[] GlamourIds = { 1, 2, 3, 4, -5 };

    /// <summary>Combined default filter: Furniture + Collectibles + Glamour.</summary>
    public static readonly int[] DefaultCategoryFilters =
        [..FurnitureIds, ..CollectibleIds, ..GlamourIds];

    // === Version (future migration support) ===

    /// <summary>
    /// Configuration schema version for migration support. Bump when adding/removing properties.
    /// Also satisfies IPluginConfiguration (implemented on the partial in Configuration.cs).
    /// </summary>
    public int Version { get; set; } = 2;

    // === CONF-01: Home World ===

    /// <summary>
    /// Player's home world for price comparisons and market board listings.
    /// Empty string triggers the first-run popup.
    /// </summary>
    public string HomeWorld { get; set; } = "";

    // === CONF-02: Profit Thresholds ===

    /// <summary>
    /// Minimum ROI percentage for scan (e.g., 25 = 25% profit).
    /// </summary>
    public int PreferredRoi { get; set; } = 25;

    /// <summary>
    /// Minimum gil profit per item.
    /// </summary>
    public int MinProfitAmount { get; set; } = 10000;

    /// <summary>
    /// Minimum average price per unit on the home server. Items selling below this are ignored.
    /// </summary>
    public int MinDesiredAvgPpu { get; set; } = 10000;

    /// <summary>
    /// The half-Kelly capital pool (in gil) that <see cref="NamazuFlippers.Core.KellySizer"/>
    /// allocates across the session's opportunities to produce each recommended quantity. Each
    /// position gets a Kelly-weighted share of this pool, then is capped by market absorption and
    /// the remaining pool. This is no longer a RouteOptimizer route-cost cap — routing does not
    /// re-apply it. Set to 0 for no capital, which yields zero recommended quantities.
    /// </summary>
    public int MaxBudgetPerSession { get; set; } = 1_000_000;

    /// <summary>
    /// Minimum sales per day on the home server. Below this floor the API's
    /// sales-per-day estimate is too noisy to trust (the rate is computed from
    /// few observations in a 7-day window). Set to 0 to disable.
    /// 0.33 ≈ 2 sales/week — a reliability threshold, not a preference.
    /// </summary>
    public double MinSalesPerDay { get; set; } = 0.33;

    // === CONF-03: Velocity Floor ===

    /// <summary>
    /// Minimum sales per week in the 7-day window. Filters out items that sell too slowly.
    /// </summary>
    public int MinSalesPerWeek { get; set; } = 2;

    // === CONF-04: Region-wide Search ===

    /// <summary>
    /// When true, searches all data centers. When false, only searches the player's local DC.
    /// </summary>
    public bool RegionWide { get; set; } = false;

    // === CONF-05: Category Filters ===

    /// <summary>
    /// Category and subcategory IDs for /api/scan. Defaults to Furniture + Collectibles + Glamour.
    /// ⚠ Mutable array — copy before exposing to untrusted code.
    /// Use <see cref="DefaultCategoryFilters"/> or named presets (<see cref="FurnitureIds"/>, etc.) in application code.
    /// </summary>
    public int[] CategoryFilters { get; set; } = DefaultCategoryFilters;

    /// <summary>
    /// Human-readable category labels matching toggle states in ConfigWindow (Phase 4).
    /// ⚠ Mutable array — copy before exposing to untrusted code.
    /// </summary>
    public string[] PreferredCategories { get; set; } = { "Furniture", "Collectibles", "Glamour" };

    // === CONF-06: Vendor Items & Out-of-Stock ===

    /// <summary>
    /// When true, includes vendor NPCs as purchase sources (catches NPC-purchased flips).
    /// </summary>
    public bool IncludeVendors { get; set; } = true;

    /// <summary>
    /// When true, includes items with zero listings on the home server (OOS priority items).
    /// </summary>
    public bool ShowOutOfStock { get; set; } = true;

    // === CONF-07: Session Caps ===

    /// <summary>
    /// Maximum number of items in a single session's route.
    /// </summary>
    public int MaxItemsPerSession { get; set; } = 10;

    /// <summary>
    /// Maximum number of servers to visit in a single session.
    /// </summary>
    public int MaxServersToVisit { get; set; } = 10;

    // === CONF-08: Cache Duration ===

    /// <summary>
    /// How long (in hours) scan results remain valid before requiring a re-scan.
    /// </summary>
    public int CacheDurationHours { get; set; } = 4;

    // === Profit-per-gil: capital efficiency, Kelly sizing, Universalis (Tiers 1-3) ===

    /// <summary>
    /// Days the player will let gil sit in unsold inventory before a flip is "stuck". Sets the
    /// absorption ceiling (SalesPerDay × HoldingWindowDays) and the expected-demand window for
    /// sell confidence. CONTEXT § Holding Window.
    /// </summary>
    public int HoldingWindowDays { get; set; } = 7;

    /// <summary>
    /// Fraction of full Kelly to deploy per position. 0.5 = half-Kelly, which under-bets on
    /// purpose because the win-probabilities derived from noisy sales data are uncertain (ADR-0002).
    /// </summary>
    public double KellyFraction { get; set; } = 0.5;

    /// <summary>
    /// When true, enriches the top survivors of each scan with one batched Universalis call for
    /// home-world listing depth + recent sales. When false (or on any failure) the scan degrades to
    /// velocity-only behavior with depth = 0 and PriceConfidence = 1 (ADR-0003).
    /// </summary>
    public bool EnableUniversalis { get; set; } = true;

    /// <summary>
    /// Recent median sale price must reach this fraction of the expected sell price to leave price
    /// confidence at 1.0; below it, rank and size are discounted. Never a hard filter (criterion 4).
    /// </summary>
    public double PriceCorroborationThreshold { get; set; } = 0.9;

    /// <summary>
    /// Minimum number of recent home-world sales required before price corroboration is applied.
    /// Fewer than this ⇒ neutral price confidence (1.0).
    /// </summary>
    public int MinRecentSalesToJudge { get; set; } = 3;

    // === Optional: Shortage Predictor (Phase 6) ===

    /// <summary>
    /// When true, runs a supplementary /api/ffxiv/shortagefutures query after the main scan.
    /// Included here for config completeness; the feature is built in Phase 6.
    /// </summary>
    public bool EnableShortagePredictor { get; set; } = false;

    // === Snapshot / restore / reset (relocated from ConfigWindow so it is unit-testable) ===

    /// <summary>Deep copy of all settings, for the config window's cancel/discard baseline.</summary>
    public static Configuration Snapshot(Configuration source)
    {
        return new Configuration
        {
            Version                 = source.Version,
            HomeWorld               = source.HomeWorld,
            PreferredRoi            = source.PreferredRoi,
            MinProfitAmount         = source.MinProfitAmount,
            MinDesiredAvgPpu        = source.MinDesiredAvgPpu,
            MaxBudgetPerSession        = source.MaxBudgetPerSession,
            MinSalesPerDay          = source.MinSalesPerDay,
            MinSalesPerWeek         = source.MinSalesPerWeek,
            RegionWide              = source.RegionWide,
            CategoryFilters         = (int[])source.CategoryFilters.Clone(),
            PreferredCategories     = (string[])source.PreferredCategories.Clone(),
            IncludeVendors          = source.IncludeVendors,
            ShowOutOfStock          = source.ShowOutOfStock,
            MaxItemsPerSession      = source.MaxItemsPerSession,
            MaxServersToVisit       = source.MaxServersToVisit,
            CacheDurationHours      = source.CacheDurationHours,
            EnableShortagePredictor = source.EnableShortagePredictor,
            HoldingWindowDays          = source.HoldingWindowDays,
            KellyFraction              = source.KellyFraction,
            EnableUniversalis          = source.EnableUniversalis,
            PriceCorroborationThreshold = source.PriceCorroborationThreshold,
            MinRecentSalesToJudge      = source.MinRecentSalesToJudge,
        };
    }

    /// <summary>Copy every setting from a snapshot back onto the live config (discard path).</summary>
    public static void RestoreFrom(Configuration snapshot, Configuration target)
    {
        target.Version                 = snapshot.Version;
        target.HomeWorld               = snapshot.HomeWorld;
        target.PreferredRoi            = snapshot.PreferredRoi;
        target.MinProfitAmount         = snapshot.MinProfitAmount;
        target.MinDesiredAvgPpu        = snapshot.MinDesiredAvgPpu;
        target.MaxBudgetPerSession        = snapshot.MaxBudgetPerSession;
        target.MinSalesPerDay          = snapshot.MinSalesPerDay;
        target.MinSalesPerWeek         = snapshot.MinSalesPerWeek;
        target.RegionWide              = snapshot.RegionWide;
        target.CategoryFilters         = (int[])snapshot.CategoryFilters.Clone();
        target.PreferredCategories     = (string[])snapshot.PreferredCategories.Clone();
        target.IncludeVendors          = snapshot.IncludeVendors;
        target.ShowOutOfStock          = snapshot.ShowOutOfStock;
        target.MaxItemsPerSession      = snapshot.MaxItemsPerSession;
        target.MaxServersToVisit       = snapshot.MaxServersToVisit;
        target.CacheDurationHours      = snapshot.CacheDurationHours;
        target.EnableShortagePredictor = snapshot.EnableShortagePredictor;
        target.HoldingWindowDays          = snapshot.HoldingWindowDays;
        target.KellyFraction              = snapshot.KellyFraction;
        target.EnableUniversalis          = snapshot.EnableUniversalis;
        target.PriceCorroborationThreshold = snapshot.PriceCorroborationThreshold;
        target.MinRecentSalesToJudge      = snapshot.MinRecentSalesToJudge;
    }

    /// <summary>
    /// Reset search/route/cache preferences to their defaults. HomeWorld is preserved
    /// (player identity, not a tunable setting).
    /// </summary>
    public static void RestoreDefaults(Configuration target)
    {
        target.PreferredRoi            = 25;
        target.MinProfitAmount         = 10000;
        target.MinDesiredAvgPpu        = 10000;
        target.MaxBudgetPerSession        = 1_000_000;
        target.MinSalesPerDay          = 0.33;
        target.MinSalesPerWeek         = 2;
        target.RegionWide              = false;
        target.CategoryFilters         = (int[])DefaultCategoryFilters.Clone();
        target.PreferredCategories     = new[] { "Furniture", "Collectibles", "Glamour" };
        target.IncludeVendors          = true;
        target.ShowOutOfStock          = true;
        target.MaxItemsPerSession      = 10;
        target.MaxServersToVisit       = 10;
        target.CacheDurationHours      = 4;
        target.EnableShortagePredictor = false;
        target.HoldingWindowDays          = 7;
        target.KellyFraction              = 0.5;
        target.EnableUniversalis          = true;
        target.PriceCorroborationThreshold = 0.9;
        target.MinRecentSalesToJudge      = 3;
    }
}
