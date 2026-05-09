using Dalamud.Configuration;

namespace NamazuFlippers;

/// <summary>
/// Plugin configuration — all settings for the Namazu Flippers arbitrage workflow.
/// Persisted automatically by Dalamud's built-in JSON serialization via
/// <see cref="Dalamud.Plugin.IDalamudPluginInterface.GetPluginConfig"/> and
/// <see cref="Dalamud.Plugin.IDalamudPluginInterface.SavePluginConfig"/>.
///
/// Corresponding requirements: CONF-01 through CONF-09.
/// Configuration UI (ConfigWindow) is built in Phase 4.
/// </summary>
public class Configuration : IPluginConfiguration
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
    /// </summary>
    public int Version { get; set; } = 1;

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
    /// Maximum cumulative gil to spend across the entire route in one session.
    /// RouteOptimizer takes items in profit-rank order and stops adding once the
    /// running sum of CheapestPrice would exceed this cap. Items priced above the
    /// remaining budget are skipped, not the entire route.
    /// Set to 0 to disable the budget cap.
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

    // === Optional: Shortage Predictor (Phase 6) ===

    /// <summary>
    /// When true, runs a supplementary /api/ffxiv/shortagefutures query after the main scan.
    /// Included here for config completeness; the feature is built in Phase 6.
    /// </summary>
    public bool EnableShortagePredictor { get; set; } = false;
}
