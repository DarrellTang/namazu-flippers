using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using NamazuFlippers.Core;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace NamazuFlippers.UI;

/// <summary>
/// Daily route window. Renders today's arbitrage route from plugin.LatestScanResult.
/// In Phase 4 plan 01 the layout is read-only; checkboxes, profit tally, progress bar
/// fractions, and auto-collapse are wired in plan 04-02. ConfigWindow body is in 04-03.
/// </summary>
public class DailyRouteWindow : Window
{
    // Color palette per 04-UI-SPEC.md (locked values).
    private static readonly Vector4 GilGold = new(1.0f, 0.85f, 0.1f, 1.0f);
    private static readonly Vector4 PurchaseCyan = new(0.2f, 0.85f, 0.9f, 1.0f);
    private static readonly Vector4 VendorCyan = new(0.2f, 0.85f, 0.9f, 1.0f);
    private static readonly Vector4 OosOrange = new(1.0f, 0.55f, 0.1f, 1.0f);
    private static readonly Vector4 StaleAmber = new(0.9f, 0.7f, 0.1f, 1.0f);
    private static readonly Vector4 ErrorRed = new(0.9f, 0.2f, 0.2f, 1.0f);
    private static readonly Vector4 SuccessGreen = new(0.2f, 0.8f, 0.3f, 1.0f);
    private static readonly Vector4 CompletedGray = new(0.5f, 0.5f, 0.5f, 0.7f);
    private static readonly Vector4 CacheBlue = new(0.4f, 0.7f, 1.0f, 1.0f);

    private readonly NamazuFlippers plugin;
    private readonly IPluginLog log;

    // Wave 1: declared but unused (empty dicts -> 0/0 progress). Wave 2 (04-02) wires interactions.
    private Dictionary<int, bool> boughtState = new();
    private Dictionary<int, bool> listedState = new();
    private Dictionary<string, bool> autoCollapsedStops = new();
    private ScanEngineResult? lastSeenResult;
    private RankedOpportunity? pendingBuyItem;
    private string pendingBuySource = "";
    private DateTimeOffset pendingBuyRouteCreatedAtUtc;
    private int pendingBuyQuantity = 1;
    private int pendingBuyUnitPrice;
    private bool openBuyConfirmation;
    private List<RankedOpportunity> pendingBulkBuyItems = [];
    private DateTimeOffset pendingBulkBuyRouteCreatedAtUtc;
    private bool openBulkBuyConfirmation;

    // FFXIV market board takes 5% in retainer fees on every sale (also defined as
    // MarketTaxRate in SaddlebagClient — kept here as a literal so the UI calc is
    // self-contained and works on items loaded from caches that pre-date this change).
    private const double MarketTaxRate = 0.95;

    private static int ProfitPerSale(RankedOpportunity item) =>
        (int)(Math.Floor(item.HomePrice * MarketTaxRate) - item.PurchasePrice);

    public DailyRouteWindow(NamazuFlippers plugin, IPluginLog log)
        : base("Namazu Flippers — Daily Route", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        this.log = log;
        Size = new Vector2(420, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        var result = plugin.LatestScanResult;

        // Status banner (UI-SPEC § Status States)
        DrawStatusBanner(result);
        ImGui.Separator();

        // Progress / profit summary (dimmed empty in this plan; values flow in 04-02)
        DrawProgressSection(result);
        ImGui.Separator();

        // Null guard (Pitfall 6) and Empty/Error status: stop after summary
        if (result == null || result.Status == ScanEngineStatus.Empty || result.Status == ScanEngineStatus.Error)
            return;

        // Detect result-change to wipe state (Phase 4 D-09) and hydrate from persisted session
        // (Phase 5 D-08). Rescan envelopes have empty SessionState by construction, so hydrating
        // from them is the wipe; cache-hit envelopes carry the previously-persisted clicks.
        if (!ReferenceEquals(result, lastSeenResult))
        {
            boughtState.Clear();
            listedState.Clear();
            autoCollapsedStops.Clear();

            var session = plugin.CurrentSessionState;
            if (session != null)
            {
                foreach (var kv in session.Bought) boughtState[kv.Key] = kv.Value;
                foreach (var kv in session.Listed) listedState[kv.Key] = kv.Value;
            }

            lastSeenResult = result;
        }

        // Route stops
        foreach (var stop in result.RouteStops)
            DrawRouteStop(stop, result);

        DrawMarkBoughtPopup();
        DrawBulkMarkBoughtPopup();
    }

    private void DrawStatusBanner(ScanEngineResult? result)
    {
        if (result == null)
        {
            ImGui.TextWrapped("Scanning for opportunities... Use /nflip scan to refresh.");
            return;
        }

        switch (result.Status)
        {
            case ScanEngineStatus.Success:
                ImGui.TextColored(SuccessGreen,
                    $"Fresh scan from {result.CreatedAtUtc.ToLocalTime():HH:mm}.");
                break;
            case ScanEngineStatus.UsingCache:
                ImGui.TextColored(CacheBlue,
                    $"Using cached route from {result.CreatedAtUtc.ToLocalTime():HH:mm}. /nflip scan to refresh.");
                break;
            case ScanEngineStatus.UsingStaleCache:
                ImGui.TextColored(StaleAmber, "Route is outdated. /nflip scan to refresh.");
                break;
            case ScanEngineStatus.Empty:
                ImGui.TextWrapped("No opportunities matched your current settings.");
                break;
            case ScanEngineStatus.Error:
                ImGui.TextColored(ErrorRed, result.UserMessage);
                break;
        }

        if (result.Warnings.Count > 0)
        {
            var warning = result.Warnings[0];
            ImGui.TextColored(StaleAmber, $"Warning: {warning.UserMessage}");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                foreach (var detail in result.Warnings)
                {
                    ImGui.TextUnformatted($"{detail.FailureType} • retries: {detail.RetryCount}");
                    if (!string.IsNullOrWhiteSpace(detail.AffectedWorld))
                        ImGui.TextUnformatted($"World: {detail.AffectedWorld}");
                    if (!string.IsNullOrWhiteSpace(detail.AffectedItemName))
                        ImGui.TextUnformatted($"Item: {detail.AffectedItemName}");
                    if (!string.IsNullOrWhiteSpace(detail.TechnicalDetails))
                        ImGui.TextWrapped(detail.TechnicalDetails);
                }
                ImGui.EndTooltip();
            }
        }
    }

    private void DrawProgressSection(ScanEngineResult? result)
    {
        // Counters reflect the route the user actually sees. After the GAP-F2 cumulative
        // budget fix, result.Opportunities is the unbounded post-budget candidate pool
        // (can be hundreds); the route is RouteStops, trimmed to MaxItemsPerSession.
        // Iterating RouteStops keeps Bought/Listed denominators aligned with the rendered rows.
        var routeItems = result?.RouteStops.SelectMany(stop => stop.Items).ToList() ?? [];
        var totalItems = routeItems.Count;
        var boughtCount = routeItems.Count(o => boughtState.GetValueOrDefault(o.ItemId));
        var listedCount = routeItems.Count(o => listedState.GetValueOrDefault(o.ItemId));
        // Per-sale profit, not per-day: assumes one buy → one sale per item.
        // The /day number was misleading because the route deliberately spreads risk
        // across N items rather than concentrating budget on one fast-mover.
        var totalProfit = routeItems.Sum(ProfitPerSale);
        var listedProfit = routeItems
            .Where(o => listedState.GetValueOrDefault(o.ItemId))
            .Sum(ProfitPerSale);

        ImGui.Text($"Bought: {boughtCount}/{totalItems}   Listed: {listedCount}/{totalItems}");

        // Whole-route bulk-action row, placed AFTER the bought/listed counter Text and BEFORE
        // the Settings/Rescan row so it doesn't fight the GAP-E1 right-edge pixel budget.
        // Phase 6 changes Mark All Bought from a session-only shortcut into a confirmation
        // that creates durable quantity-1 lots at routed buy prices.
        var routeMutationsDisabled = plugin.ScanInProgress || totalItems == 0;
        if (routeMutationsDisabled)
            ImGui.BeginDisabled();

        if (ImGui.Button("Mark All Bought"))
        {
            pendingBulkBuyItems = routeItems
                .Where(item => !boughtState.GetValueOrDefault(item.ItemId))
                .ToList();
            pendingBulkBuyRouteCreatedAtUtc = result?.CreatedAtUtc ?? DateTimeOffset.UtcNow;
            openBulkBuyConfirmation = pendingBulkBuyItems.Count > 0;
        }
        ImGui.SameLine();
        if (ImGui.Button("Mark All Listed"))
        {
            foreach (var item in routeItems) listedState[item.ItemId] = true;
            plugin.QueueSessionSave(boughtState, listedState);
        }

        if (routeMutationsDisabled)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Positions"))
            plugin.OpenPositionsWindow();
        ImGui.SameLine();
        if (ImGui.Button("History"))
            plugin.OpenProfitHistoryWindow();

        // GAP-E1 (04-08): buttons on their OWN row (no SameLine after the Text) so
        // avail = ImGui.GetContentRegionAvail().X measures the full content region
        // width, not the remainder of a partially-consumed row. Button widths are
        // multiplied by ImGuiHelpers.GlobalScale so the 110/80 base sizes grow with
        // the FFXIV UI scale and "Rescan Route" fits inside the frame at scale > 1.0.
        // See .planning/debug/rescan-button-still-cut-off-2.md for the pixel arithmetic.
        var rescanWidth = 110f * ImGuiHelpers.GlobalScale;
        var settingsWidth = 80f * ImGuiHelpers.GlobalScale;
        // Source the gap from runtime ImGui style so it tracks Dalamud's UI scale —
        // the SameLine() between Settings and Rescan below uses this same value.
        // (Hardcoding 8f overflowed Rescan past the right edge at FFXIV UI scale > 1.0;
        // see 04-REVIEW.md WR-02 and .planning/debug/rescan-button-still-cut-off.md.)
        var buttonSpacing = ImGui.GetStyle().ItemSpacing.X;
        var avail = ImGui.GetContentRegionAvail().X;
        var combinedWidth = rescanWidth + buttonSpacing + settingsWidth;
        if (avail > combinedWidth)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - combinedWidth);

        // Settings button (D-07) — second entry point alongside UiBuilder.OpenConfigUi.
        // Rendered FIRST (leftmost of the right-aligned pair) so Rescan ends up at the
        // window's right edge and Settings sits inside the content region beside it.
        if (ImGui.Button("Settings", new Vector2(settingsWidth, 0)))
            plugin.OpenConfigWindow();

        ImGui.SameLine();
        // Snapshot the flag: it flips on a thread-pool thread (scan completion), and
        // re-reading it after the Button call can unbalance BeginDisabled/EndDisabled,
        // leaking disabled state into the shared ImGui context.
        var scanning = plugin.ScanInProgress;
        if (scanning)
            ImGui.BeginDisabled();
        if (ImGui.Button("Rescan Route", new Vector2(rescanWidth, 0)))
            _ = plugin.RescanAsync(CancellationToken.None);
        if (scanning)
            ImGui.EndDisabled();

        var boughtFraction = totalItems > 0 ? (float)boughtCount / totalItems : 0f;
        var listedFraction = totalItems > 0 ? (float)listedCount / totalItems : 0f;

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, totalItems > 0 ? SuccessGreen : CompletedGray);
        ImGui.ProgressBar(boughtFraction, new Vector2(-1, 16), "");
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, totalItems > 0 ? PurchaseCyan : CompletedGray);
        ImGui.ProgressBar(listedFraction, new Vector2(-1, 16), "");
        ImGui.PopStyleColor();

        ImGui.TextColored(GilGold, $"Profit: {listedProfit:n0} / {totalProfit:n0} gil");

        // Session deployment summary (criterion 9): gil the recommended quantities deploy, versus the
        // Kelly budget pool, versus what the home market could absorb. Deployed < budget is correct
        // when absorption is the binding constraint (ADR-0002), so all three are shown side by side.
        var deployedGil = KellySizer.TotalDeployedGil(routeItems);
        var absorptionCeilingGil = KellySizer.TotalAbsorptionCeilingGil(routeItems);
        var budgetPool = plugin.Configuration.MaxBudgetPerSession;
        ImGui.TextColored(GilGold,
            $"Deployed: {deployedGil:n0} / budget {budgetPool:n0} / absorption ceiling {absorptionCeilingGil:n0} gil");
    }

    private void DrawRouteStop(RouteStop stop, ScanEngineResult result)
    {
        // Stop is "done" once all items are LISTED at home (not just bought).
        // Bought = visited the source server; Listed = posted on home market board.
        // Auto-collapsing on bought hides items before the user has actually moved them
        // to the market, which is the wrong moment.
        bool allListed = stop.Items.Count > 0
            && stop.Items.All(item => listedState.GetValueOrDefault(item.ItemId));

        // Auto-collapse on first all-listed frame; reset flag when user un-checks (UI-07, Pitfall 2)
        if (allListed && !autoCollapsedStops.GetValueOrDefault(stop.PurchaseSource))
        {
            ImGui.SetNextItemOpen(false, ImGuiCond.Always);
            autoCollapsedStops[stop.PurchaseSource] = true;
        }
        else if (!allListed)
        {
            autoCollapsedStops[stop.PurchaseSource] = false;
        }

        // Per-sale total for this stop (one buy/sell per item), not the historical /day rate.
        var stopProfit = stop.Items.Sum(ProfitPerSale);

        // Header label — checkmark prefix and CompletedGray when all listed
        string headerLabel;
        if (allListed)
        {
            headerLabel = stop.IsVendorStop
                ? $"✓ Vendor: {stop.PurchaseSource} — {stop.Items.Count} items — {stopProfit:n0} gil"
                : $"✓ {stop.PurchaseSource} ({stop.DataCenter}) — {stop.Items.Count} items — {stopProfit:n0} gil";
        }
        else
        {
            headerLabel = stop.IsVendorStop
                ? $"Vendor: {stop.PurchaseSource} — {stop.Items.Count} items — {stopProfit:n0} gil"
                : $"{stop.PurchaseSource} ({stop.DataCenter}) — {stop.Items.Count} items — {stopProfit:n0} gil";
        }

        // Apply header color: CompletedGray when all listed, VendorCyan for vendor stops
        bool pushColor = allListed || stop.IsVendorStop;
        if (pushColor)
            ImGui.PushStyleColor(ImGuiCol.Text, allListed ? CompletedGray : VendorCyan);

        bool open = ImGui.CollapsingHeader(headerLabel);

        if (pushColor)
            ImGui.PopStyleColor();

        if (open)
        {
            DrawItems(stop, result);
        }
    }

    private void DrawItems(RouteStop stop, ScanEngineResult result)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
        try
        {
            foreach (var item in stop.Items)
            {
                var bought = boughtState.GetValueOrDefault(item.ItemId);
                var scanning = plugin.ScanInProgress;
                if (scanning)
                    ImGui.BeginDisabled();
                if (ImGui.Checkbox($"##bought-{item.ItemId}", ref bought))
                {
                    if (bought)
                        OpenMarkBoughtConfirmation(item, stop.PurchaseSource, result.CreatedAtUtc);
                    else
                    {
                        boughtState[item.ItemId] = false;
                        plugin.QueueSessionSave(boughtState, listedState);
                    }
                }
                if (scanning)
                    ImGui.EndDisabled();

                ImGui.SameLine();

                if (bought)
                    ImGui.TextColored(CompletedGray, item.Name);
                else if (item.OutOfStock)
                    ImGui.TextColored(OosOrange, item.Name);
                else
                    ImGui.Text(item.Name);

                // Click name to copy to clipboard for pasting into the market board search.
                // IsItemHovered + IsMouseClicked is robust on plain Text (which has no native
                // click capture); SetClipboardText routes through Dalamud's clipboard provider.
                var nameHovered = ImGui.IsItemHovered();
                if (nameHovered)
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (nameHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    ImGui.SetClipboardText(item.Name);

                if (nameHovered)
                {
                    ImGui.BeginTooltip();
                    ImGui.Text($"Avg {item.SalesPerDay:F2} sales/day");
                    if (item.SalesPerDay > 0)
                        ImGui.Text($"~{1.0 / item.SalesPerDay:F1} days between sales");
                    // Secondary signals (criterion 9): competition depth + the confidence multipliers
                    // that discounted this item's rank and size. Kept out of the inline row.
                    ImGui.Text($"Home listings (depth): {item.Depth}");
                    ImGui.Text($"Sell confidence: {item.SellConfidence:P0}");
                    ImGui.Text($"Price confidence: {item.PriceConfidence:P0}");
                    ImGui.Text($"Recommended qty: {item.RecommendedQuantity:n0} (absorption {Math.Floor(item.AbsorptionCap):n0})");
                    ImGui.Separator();
                    ImGui.TextDisabled("Click to copy name");
                    ImGui.EndTooltip();
                }

                if (item.OutOfStock)
                {
                    ImGui.SameLine(0, 4);
                    ImGui.TextColored(OosOrange, "[OOS]");
                }
                if (item.IsVendorSource)
                {
                    ImGui.SameLine(0, 4);
                    ImGui.TextColored(VendorCyan, "[Vendor]");
                }

                ImGui.SameLine();
                ImGui.TextColored(PurchaseCyan, $"Buy: {item.PurchasePrice:n0}");

                // Recommended absorption-capped half-Kelly quantity (criterion 9). This is the
                // primary sizing signal — depth and sell confidence stay in the tooltip below.
                ImGui.SameLine();
                ImGui.TextColored(SuccessGreen, $"Qty {item.RecommendedQuantity:n0}");

                ImGui.SameLine();
                ImGui.TextColored(GilGold, $"+{ProfitPerSale(item):n0}");

                // Inline velocity hint so the user can judge whether the daily-profit number
                // comes from many small sales (~fast) or few big sales with a wait (~slow).
                // Compact format: "2.4/d" when >= 1/day, "~Nd" (rounded) when < 1/day.
                ImGui.SameLine();
                var velocityLabel = item.SalesPerDay >= 1.0
                    ? $"{item.SalesPerDay:F1}/d"
                    : item.SalesPerDay > 0
                        ? $"~{Math.Max(1, (int)Math.Round(1.0 / item.SalesPerDay))}d"
                        : "—";
                ImGui.TextDisabled(velocityLabel);

                // Anchor the Listed checkbox + price label to a fixed X column right-aligned
                // inside the row, so the checkbox lands in the same column on every item
                // regardless of name length / [OOS] / [Vendor] / price / profit text widths.
                // Computing the X each frame from GetContentRegionMax keeps the column resilient
                // if the window is later resized; window is currently locked at 420px width.
                // Width budget for the trailing column = checkbox (~22 px) + ItemSpacing
                // (~8 px scaled) + "List: 9,999,999" worst-case label (~150 px at scale 1.0).
                // Multiplied by GlobalScale so the column grows with FFXIV UI scale (same fix
                // as the GAP-E1 button widths). 180f base accommodates 7-digit prices that
                // appear once non-furniture items pass MinSalesPerDay.
                var listedColumnWidth = 180f * ImGuiHelpers.GlobalScale;
                var rowCursorPosX = ImGui.GetCursorPosX();
                var contentMaxX = ImGui.GetWindowContentRegionMax().X;
                var listedAnchorX = contentMaxX - listedColumnWidth;
                // Fall back to bare SameLine() if the row is too narrow to honor the anchor —
                // prevents the checkbox from jumping LEFT into the prior text on a too-narrow row.
                if (listedAnchorX > rowCursorPosX)
                    ImGui.SameLine(listedAnchorX);
                else
                    ImGui.SameLine();
                var listed = listedState.GetValueOrDefault(item.ItemId);
                scanning = plugin.ScanInProgress;
                if (scanning)
                    ImGui.BeginDisabled();
                if (ImGui.Checkbox($"##listed-{item.ItemId}", ref listed))
                {
                    listedState[item.ItemId] = listed;
                    plugin.QueueSessionSave(boughtState, listedState);
                }
                if (scanning)
                    ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.TextColored(GilGold, $"List: {item.HomePrice:n0}");
            }
        }
        finally
        {
            ImGui.PopStyleVar();
        }
    }

    private void OpenMarkBoughtConfirmation(
        RankedOpportunity item,
        string sourceWorld,
        DateTimeOffset routeCreatedAtUtc)
    {
        pendingBuyItem = item;
        pendingBuySource = sourceWorld;
        pendingBuyRouteCreatedAtUtc = routeCreatedAtUtc;
        pendingBuyQuantity = 1;
        pendingBuyUnitPrice = Math.Max(1, item.PurchasePrice);
        openBuyConfirmation = true;
    }

    private void DrawMarkBoughtPopup()
    {
        if (openBuyConfirmation)
        {
            ImGui.OpenPopup("ConfirmBoughtLot##daily");
            openBuyConfirmation = false;
        }

        var popupOpen = true;
        if (ImGui.BeginPopupModal("ConfirmBoughtLot##daily", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (pendingBuyItem == null)
            {
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            ImGui.TextUnformatted(pendingBuyItem.Name);
            ImGui.TextDisabled($"Source: {pendingBuySource}");
            ImGui.Spacing();

            if (ImGui.InputInt("Qty", ref pendingBuyQuantity))
                pendingBuyQuantity = Math.Max(1, pendingBuyQuantity);
            if (ImGui.InputInt("Unit buy", ref pendingBuyUnitPrice))
                pendingBuyUnitPrice = Math.Max(1, pendingBuyUnitPrice);

            var projectedProfit = (int)Math.Floor(pendingBuyItem.HomePrice * MarketTaxRate) - pendingBuyUnitPrice;
            ImGui.TextDisabled($"Expected list {pendingBuyItem.HomePrice:n0} • planned/unit {projectedProfit:n0} gil");
            ImGui.Spacing();

            var canSave = !plugin.ScanInProgress && pendingBuyQuantity > 0 && pendingBuyUnitPrice > 0;
            if (!canSave)
                ImGui.BeginDisabled();
            if (ImGui.Button("Save Lot", new Vector2(120, 0)))
            {
                plugin.QueueBoughtLotSave(
                    pendingBuyItem,
                    pendingBuyQuantity,
                    pendingBuyUnitPrice,
                    pendingBuySource,
                    pendingBuyRouteCreatedAtUtc);
                boughtState[pendingBuyItem.ItemId] = true;
                plugin.QueueSessionSave(boughtState, listedState);
                ClearPendingBuy();
                ImGui.CloseCurrentPopup();
            }
            if (!canSave)
                ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                ClearPendingBuy();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void ClearPendingBuy()
    {
        pendingBuyItem = null;
        pendingBuySource = "";
        pendingBuyRouteCreatedAtUtc = default;
        pendingBuyQuantity = 1;
        pendingBuyUnitPrice = 0;
    }

    private void DrawBulkMarkBoughtPopup()
    {
        if (openBulkBuyConfirmation)
        {
            ImGui.OpenPopup("ConfirmBulkBoughtLots##daily");
            openBulkBuyConfirmation = false;
        }

        var popupOpen = true;
        if (ImGui.BeginPopupModal("ConfirmBulkBoughtLots##daily", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped(
                $"Create {pendingBulkBuyItems.Count} bought lots at quantity 1 using the routed buy prices?");
            ImGui.TextDisabled("Correct quantity or unit buy price afterward from Positions.");
            ImGui.Spacing();

            var canSave = !plugin.ScanInProgress && pendingBulkBuyItems.Count > 0;
            if (!canSave)
                ImGui.BeginDisabled();
            if (ImGui.Button("Save Lots", new Vector2(120, 0)))
            {
                foreach (var item in pendingBulkBuyItems)
                {
                    plugin.QueueBoughtLotSave(
                        item,
                        quantity: 1,
                        actualUnitBuyPrice: Math.Max(1, item.PurchasePrice),
                        sourceWorld: item.PurchaseSource,
                        routeCreatedAtUtc: pendingBulkBuyRouteCreatedAtUtc);
                    boughtState[item.ItemId] = true;
                }
                plugin.QueueSessionSave(boughtState, listedState);
                ClearPendingBulkBuy();
                ImGui.CloseCurrentPopup();
            }
            if (!canSave)
                ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                ClearPendingBulkBuy();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void ClearPendingBulkBuy()
    {
        pendingBulkBuyItems = [];
        pendingBulkBuyRouteCreatedAtUtc = default;
    }
}
