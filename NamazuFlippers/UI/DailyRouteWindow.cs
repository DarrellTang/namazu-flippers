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
    // Color palette per 04-UI-SPEC.md (locked values — nyquist.sh asserts these literals).
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

        // Phase 5 D-10/D-11/D-13: Whole-route bulk-action row, placed AFTER the bought/listed
        // counter Text and BEFORE the Settings/Rescan row so it doesn't fight the GAP-E1 right-edge
        // pixel budget. Bought first, Listed second — left-to-right alignment with the counter row
        // directly above. Both buttons always enabled (D-13). No confirmation modal (D-12: each
        // individual checkbox is reversible).
        if (ImGui.Button("Mark All Bought"))
        {
            foreach (var item in routeItems) boughtState[item.ItemId] = true;
            plugin.QueueSessionSave(boughtState, listedState);
        }
        ImGui.SameLine();
        if (ImGui.Button("Mark All Listed"))
        {
            foreach (var item in routeItems) listedState[item.ItemId] = true;
            plugin.QueueSessionSave(boughtState, listedState);
        }

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
        if (plugin.ScanInProgress)
            ImGui.BeginDisabled();
        if (ImGui.Button("Rescan Route", new Vector2(rescanWidth, 0)))
            _ = plugin.RescanAsync(CancellationToken.None);
        if (plugin.ScanInProgress)
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
            DrawItems(stop);
        }
    }

    private void DrawItems(RouteStop stop)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
        try
        {
            foreach (var item in stop.Items)
            {
                var bought = boughtState.GetValueOrDefault(item.ItemId);
                if (ImGui.Checkbox($"##bought-{item.ItemId}", ref bought))
                {
                    boughtState[item.ItemId] = bought;
                    plugin.QueueSessionSave(boughtState, listedState);
                }

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
                if (ImGui.Checkbox($"##listed-{item.ItemId}", ref listed))
                {
                    listedState[item.ItemId] = listed;
                    plugin.QueueSessionSave(boughtState, listedState);
                }
                ImGui.SameLine();
                ImGui.TextColored(GilGold, $"List: {item.HomePrice:n0}");
            }
        }
        finally
        {
            ImGui.PopStyleVar();
        }
    }
}
