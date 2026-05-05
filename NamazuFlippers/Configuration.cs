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
    /// Category and subcategory IDs passed to the /api/scan endpoint.
    /// Default: Furniture (56,65-72,81-82) + Collectibles (75,80,90) + Glamour (1-4,-5).
    /// </summary>
    public int[] CategoryFilters { get; set; } =
    {
        56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82,  // Furniture
        75, 80, 90,                                     // Collectibles
        1, 2, 3, 4, -5                                  // Glamour
    };

    /// <summary>
    /// Human-readable category labels matching the toggle states in ConfigWindow (Phase 4).
    /// Used to render category toggles in the settings UI.
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
