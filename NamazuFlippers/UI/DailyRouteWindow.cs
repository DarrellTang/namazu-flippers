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

        // Detect result-change to wipe state (D-09) — wave 2 acts on this; wave 1 just tracks last seen.
        if (!ReferenceEquals(result, lastSeenResult))
        {
            boughtState.Clear();
            listedState.Clear();
            autoCollapsedStops.Clear();
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
            // Success: no banner.
        }
    }

    private void DrawProgressSection(ScanEngineResult? result)
    {
        var totalItems = result?.Opportunities.Count ?? 0;
        var boughtCount = result?.Opportunities.Count(o => boughtState.GetValueOrDefault(o.ItemId)) ?? 0;
        var listedCount = result?.Opportunities.Count(o => listedState.GetValueOrDefault(o.ItemId)) ?? 0;
        var totalProfit = result?.TotalExpectedDailyProfit ?? 0;
        var listedProfit = result?.Opportunities
            .Where(o => listedState.GetValueOrDefault(o.ItemId))
            .Sum(o => o.ExpectedDailyProfit) ?? 0;

        ImGui.Text($"Bought: {boughtCount}/{totalItems}   Listed: {listedCount}/{totalItems}");

        ImGui.SameLine();
        const float buttonWidth = 110f;
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > buttonWidth)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - buttonWidth);

        if (plugin.ScanInProgress)
            ImGui.BeginDisabled();
        if (ImGui.Button("Rescan Route", new Vector2(buttonWidth, 0)))
            _ = plugin.RescanAsync(CancellationToken.None);
        if (plugin.ScanInProgress)
            ImGui.EndDisabled();

        // Settings button (D-07) — second entry point alongside UiBuilder.OpenConfigUi.
        ImGui.SameLine();
        if (ImGui.Button("Settings", new Vector2(80, 0)))
            plugin.OpenConfigWindow();

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
        // Auto-collapse on stop completion (UI-07) — wave 2 wires ImGui.SetNextItemOpen(false, ImGuiCond.Always).
        // Wave 1: read-only headers, no auto-collapse trigger. State dicts tracked for wave 2.
        // 04-02 adds: SetNextItemOpen with ImGuiCond.Always once allBought first becomes true.
        bool allBought = stop.Items.Count > 0 && stop.Items.All(item => boughtState.GetValueOrDefault(item.ItemId));

        if (allBought && !autoCollapsedStops.GetValueOrDefault(stop.PurchaseSource))
        {
            // 04-02: ImGui.SetNextItemOpen(false, ImGuiCond.Always);
            autoCollapsedStops[stop.PurchaseSource] = true;
        }
        else if (!allBought)
        {
            autoCollapsedStops[stop.PurchaseSource] = false;
        }

        var headerLabel = stop.IsVendorStop
            ? $"Vendor: {stop.PurchaseSource} — {stop.Items.Count} items — {stop.TotalExpectedDailyProfit:n0} gil/day"
            : $"{stop.PurchaseSource} ({stop.DataCenter}) — {stop.Items.Count} items — {stop.TotalExpectedDailyProfit:n0} gil/day";

        if (stop.IsVendorStop)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, VendorCyan);
            var open = ImGui.CollapsingHeader(headerLabel);
            ImGui.PopStyleColor();
            if (open)
                DrawItems(stop, isHomeStop: false);
        }
        else
        {
            if (ImGui.CollapsingHeader(headerLabel))
                DrawItems(stop, isHomeStop: stop.PurchaseSource.Equals(plugin.Configuration.HomeWorld, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void DrawItems(RouteStop stop, bool isHomeStop)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
        try
        {
            foreach (var item in stop.Items)
            {
                var bought = boughtState.GetValueOrDefault(item.ItemId);
                if (ImGui.Checkbox($"##bought-{item.ItemId}", ref bought))
                    boughtState[item.ItemId] = bought;

                ImGui.SameLine();

                if (bought)
                    ImGui.TextColored(CompletedGray, item.Name);
                else if (item.OutOfStock)
                    ImGui.TextColored(OosOrange, item.Name);
                else
                    ImGui.Text(item.Name);

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text($"Avg {item.SalesPerDay:F1} sales/day");
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
                ImGui.TextColored(GilGold, $"+{item.ExpectedDailyProfit:n0}/day");

                if (isHomeStop)
                {
                    ImGui.SameLine();
                    var listed = listedState.GetValueOrDefault(item.ItemId);
                    if (ImGui.Checkbox($"##listed-{item.ItemId}", ref listed))
                        listedState[item.ItemId] = listed;
                    ImGui.SameLine();
                    ImGui.TextColored(GilGold, $"List: {item.HomePrice:n0}");
                }
            }
        }
        finally
        {
            ImGui.PopStyleVar();
        }
    }
}
