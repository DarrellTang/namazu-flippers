namespace NamazuFlippers.Core;

/// <summary>
/// Ranked arbitrage opportunity selected from a Saddlebag scan row, enriched with
/// Universalis competition/price signals and an absorption-capped Kelly quantity.
/// </summary>
public sealed class RankedOpportunity
{
    public int ItemId { get; set; }

    public string Name { get; set; } = "";

    public int HomePrice { get; set; }

    public string PurchaseSource { get; set; } = "";

    public int PurchasePrice { get; set; }

    public double SalesPerDay { get; set; }

    public int ExpectedDailyProfit { get; set; }

    /// <summary>Post-tax profit per unit (Saddlebag ProfitPerUnit). Numerator of the Kelly edge
    /// and of CapitalEfficiency.</summary>
    public int ProfitPerUnit { get; set; }

    public bool OutOfStock { get; set; }

    public bool IsVendorSource { get; set; }

    // === Tier 1: capital-efficiency ranking ===

    /// <summary>(ProfitPerUnit / PurchasePrice) × SalesPerDay — the primary, velocity-aware
    /// return-per-gil-per-day signal that replaces ExpectedDailyProfit as the rank key.</summary>
    public double CapitalEfficiency { get; set; }

    /// <summary>CapitalEfficiency × SellConfidence × PriceConfidence — the final ranking key.</summary>
    public double FinalRank { get; set; }

    // === Tier 2/3: Universalis enrichment + confidence ===

    /// <summary>Home-world competing listing count from Universalis. 0 when Universalis is
    /// disabled, failed, OOS, or the item was not enriched (treated as no competition).</summary>
    public int Depth { get; set; }

    /// <summary>Sell confidence c = d_exp / (d_exp + depth); 1.0 when depth is 0/unavailable.</summary>
    public double SellConfidence { get; set; } = 1.0;

    /// <summary>0–1 persistence multiplier from recent home-world sales corroboration; 1.0 when
    /// Universalis is unavailable or too few recent sales to judge.</summary>
    public double PriceConfidence { get; set; } = 1.0;

    /// <summary>True when the expected sell price is the median of enough recent home-world
    /// Universalis sales (outlier-robust). False when it is Saddlebag's average — shown as an
    /// "unverified price" hint in the UI so a fluke-inflated average is never taken at face value.</summary>
    public bool PriceVerified { get; set; }

    /// <summary>Per-opportunity unit ceiling A = max(0, d_exp − depth). Caps the recommended
    /// quantity so the player's gil is not stuck behind unsold listings.</summary>
    public double AbsorptionCap { get; set; }

    /// <summary>Absorption-capped half-Kelly recommended buy quantity for this session.</summary>
    public int RecommendedQuantity { get; set; }
}
