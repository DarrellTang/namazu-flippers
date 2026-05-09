using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using NamazuFlippers.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace NamazuFlippers.UI;

/// <summary>
/// Settings editor window. Implements snapshot/dirty/save/discard pattern (D-12)
/// and Reset-to-Defaults confirmation modal (D-13). Renders one widget per
/// Configuration property; covers CONF-01..CONF-09. EnableShortagePredictor is
/// rendered but its API wiring is deferred to Phase 6.
/// </summary>
public class ConfigWindow : Window
{
    private readonly NamazuFlippers plugin;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;

    private Configuration? snapshot;
    private bool isDirty;
    private bool showUnsavedModal;
    private int selectedWorldIndex = -1;

    public ConfigWindow(NamazuFlippers plugin, IDalamudPluginInterface pluginInterface, IPluginLog log)
        : base("Namazu Flippers — Settings", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        this.pluginInterface = pluginInterface;
        this.log = log;
        Size = new Vector2(400, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void OnOpen()
    {
        // Guard against Dalamud's WindowHost.DrawInternal spuriously firing OnOpen on the
        // frame after OnClose cancels a dirty close (sets IsOpen = true to keep the window
        // alive while the unsaved-changes modal renders). On a genuine new open, isDirty
        // is always false because Save and Discard both clear it before closing, and
        // Cancel keeps the window open without dispatching OnClose. On the spurious
        // re-open, isDirty is true — and re-snapshotting at that moment would capture
        // the user's edited values, corrupting the Discard restore path.
        if (!isDirty)
        {
            snapshot = Snapshot(plugin.Configuration);
            selectedWorldIndex = Array.IndexOf(WorldData.KnownWorlds, plugin.Configuration.HomeWorld);
        }
    }

    public override void OnClose()
    {
        if (isDirty)
        {
            IsOpen = true;
            showUnsavedModal = true;
        }
    }

    public override void Draw()
    {
        // -- Unsaved-changes modal trigger (must be checked at top of Draw so OpenPopup fires this frame) --
        if (showUnsavedModal)
        {
            ImGui.OpenPopup("UnsavedChanges##config");
            showUnsavedModal = false;
        }

        // -- Home World (CONF-01) --
        if (ImGui.CollapsingHeader("Home World", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var preview = selectedWorldIndex >= 0 && selectedWorldIndex < WorldData.KnownWorlds.Length
                ? WorldData.KnownWorlds[selectedWorldIndex]
                : "(no world set)";
            ImGui.TextUnformatted($"Current home world: {plugin.Configuration.HomeWorld}");
            if (ImGui.BeginCombo("##config-home-world", preview))
            {
                for (int i = 0; i < WorldData.KnownWorlds.Length; i++)
                {
                    var isSelected = i == selectedWorldIndex;
                    if (ImGui.Selectable(WorldData.KnownWorlds[i], isSelected))
                    {
                        selectedWorldIndex = i;
                        plugin.Configuration.HomeWorld = WorldData.KnownWorlds[i];
                        isDirty = true;
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
        }

        // -- Profit Thresholds (CONF-02) --
        if (ImGui.CollapsingHeader("Profit Thresholds", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var roi = plugin.Configuration.PreferredRoi;
            if (ImGui.SliderInt("Min ROI %%", ref roi, 0, 100))
            {
                plugin.Configuration.PreferredRoi = Math.Clamp(roi, 0, 100);
                isDirty = true;
            }

            var minProfit = plugin.Configuration.MinProfitAmount;
            if (ImGui.InputInt("Min Profit (gil)", ref minProfit))
            {
                plugin.Configuration.MinProfitAmount = Math.Max(0, minProfit);
                isDirty = true;
            }

            var minPpu = plugin.Configuration.MinDesiredAvgPpu;
            if (ImGui.InputInt("Min Avg PPU (gil)", ref minPpu))
            {
                plugin.Configuration.MinDesiredAvgPpu = Math.Max(0, minPpu);
                isDirty = true;
            }

            var budget = plugin.Configuration.MaxBudgetPerSession;
            if (ImGui.InputInt("Budget Cap (gil)", ref budget))
            {
                plugin.Configuration.MaxBudgetPerSession = Math.Max(0, budget);
                isDirty = true;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Total gil to spend across the whole route. Set to 0 to disable.");
                ImGui.EndTooltip();
            }
        }

        // -- Velocity (CONF-03) --
        if (ImGui.CollapsingHeader("Velocity", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var salesDay = (float)plugin.Configuration.MinSalesPerDay;
            if (ImGui.SliderFloat("Min Sales/Day", ref salesDay, 0f, 5f, "%.2f"))
            {
                plugin.Configuration.MinSalesPerDay = Math.Max(0.0, salesDay);
                isDirty = true;
            }

            var salesWeek = plugin.Configuration.MinSalesPerWeek;
            if (ImGui.SliderInt("Min Sales/Week", ref salesWeek, 0, 20))
            {
                plugin.Configuration.MinSalesPerWeek = Math.Max(0, salesWeek);
                isDirty = true;
            }
        }

        // -- Filters (CONF-04, CONF-05, CONF-06) --
        if (ImGui.CollapsingHeader("Filters", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var regionWide = plugin.Configuration.RegionWide;
            if (ImGui.Checkbox("Region-wide search", ref regionWide))
            {
                plugin.Configuration.RegionWide = regionWide;
                isDirty = true;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Search all data centers, not just your DC");
                ImGui.EndTooltip();
            }

            // CategoryFilters (CONF-05) — three named-preset toggles
            ImGui.TextUnformatted("Categories:");
            var furnitureOn   = plugin.Configuration.CategoryFilters.Contains(Configuration.FurnitureIds[0]);
            var collectibleOn = plugin.Configuration.CategoryFilters.Contains(Configuration.CollectibleIds[0]);
            var glamourOn     = plugin.Configuration.CategoryFilters.Contains(Configuration.GlamourIds[0]);

            if (ImGui.Checkbox("Furniture", ref furnitureOn))
            {
                plugin.Configuration.CategoryFilters = ApplyCategoryToggle(
                    plugin.Configuration.CategoryFilters, Configuration.FurnitureIds, furnitureOn);
                plugin.Configuration.PreferredCategories = SyncCategoryLabels(plugin.Configuration);
                isDirty = true;
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Collectibles", ref collectibleOn))
            {
                plugin.Configuration.CategoryFilters = ApplyCategoryToggle(
                    plugin.Configuration.CategoryFilters, Configuration.CollectibleIds, collectibleOn);
                plugin.Configuration.PreferredCategories = SyncCategoryLabels(plugin.Configuration);
                isDirty = true;
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Glamour", ref glamourOn))
            {
                plugin.Configuration.CategoryFilters = ApplyCategoryToggle(
                    plugin.Configuration.CategoryFilters, Configuration.GlamourIds, glamourOn);
                plugin.Configuration.PreferredCategories = SyncCategoryLabels(plugin.Configuration);
                isDirty = true;
            }

            var includeVendors = plugin.Configuration.IncludeVendors;
            if (ImGui.Checkbox("Include vendor sources", ref includeVendors))
            {
                plugin.Configuration.IncludeVendors = includeVendors;
                isDirty = true;
            }

            var showOos = plugin.Configuration.ShowOutOfStock;
            if (ImGui.Checkbox("Include out-of-stock items", ref showOos))
            {
                plugin.Configuration.ShowOutOfStock = showOos;
                isDirty = true;
            }
        }

        // -- Route Caps (CONF-07) --
        if (ImGui.CollapsingHeader("Route Caps", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var maxItems = plugin.Configuration.MaxItemsPerSession;
            if (ImGui.SliderInt("Max Items", ref maxItems, 1, 20))
            {
                plugin.Configuration.MaxItemsPerSession = Math.Clamp(maxItems, 1, 20);
                isDirty = true;
            }

            var maxServers = plugin.Configuration.MaxServersToVisit;
            if (ImGui.SliderInt("Max Servers", ref maxServers, 1, 15))
            {
                plugin.Configuration.MaxServersToVisit = Math.Clamp(maxServers, 1, 15);
                isDirty = true;
            }
        }

        // -- Cache (CONF-08) --
        if (ImGui.CollapsingHeader("Cache", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var cacheHours = plugin.Configuration.CacheDurationHours;
            if (ImGui.SliderInt("Cache Duration (hours)", ref cacheHours, 1, 24))
            {
                plugin.Configuration.CacheDurationHours = Math.Clamp(cacheHours, 1, 24);
                isDirty = true;
            }
        }

        // -- Phase 6 preview: Shortage Predictor (rendered visible but inert in Phase 4) --
        if (ImGui.CollapsingHeader("Shortage Predictor (Phase 6 preview)", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var shortage = plugin.Configuration.EnableShortagePredictor;
            if (ImGui.Checkbox("Enable Shortage Predictor (Phase 6 — not yet wired)", ref shortage))
            {
                plugin.Configuration.EnableShortagePredictor = shortage;
                isDirty = true;
            }
        }

        ImGui.Separator();

        // -- Save / Reset buttons --
        if (ImGui.Button("Save Settings", new Vector2(140, 0)))
        {
            pluginInterface.SavePluginConfig(plugin.Configuration);
            snapshot = Snapshot(plugin.Configuration);
            isDirty = false;
        }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, UiColors.ErrorRed);
        if (ImGui.Button("Reset to Defaults", new Vector2(160, 0)))
            ImGui.OpenPopup("ConfirmReset##config");
        ImGui.PopStyleColor();

        // -- Reset confirmation modal (D-13) --
        var resetOpen = true;
        if (ImGui.BeginPopupModal("ConfirmReset##config", ref resetOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Reset all settings to defaults?");
            ImGui.Spacing();
            if (ImGui.Button("Reset", new Vector2(120, 0)))
            {
                RestoreDefaults(plugin.Configuration);
                isDirty = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        // -- Unsaved-changes modal (D-12) --
        var unsavedOpen = true;
        if (ImGui.BeginPopupModal("UnsavedChanges##config", ref unsavedOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Save changes before closing?");
            ImGui.Spacing();
            if (ImGui.Button("Save", new Vector2(120, 0)))
            {
                pluginInterface.SavePluginConfig(plugin.Configuration);
                snapshot = Snapshot(plugin.Configuration);
                isDirty = false;
                IsOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Discard", new Vector2(120, 0)))
            {
                if (snapshot != null) RestoreFrom(snapshot, plugin.Configuration);
                isDirty = false;
                IsOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                IsOpen = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private static int[] ApplyCategoryToggle(int[] current, int[] presetIds, bool include)
    {
        var set = new HashSet<int>(current);
        if (include) foreach (var id in presetIds) set.Add(id);
        else         foreach (var id in presetIds) set.Remove(id);
        return set.ToArray();
    }

    private static string[] SyncCategoryLabels(Configuration cfg)
    {
        var labels = new List<string>(3);
        if (cfg.CategoryFilters.Contains(Configuration.FurnitureIds[0]))   labels.Add("Furniture");
        if (cfg.CategoryFilters.Contains(Configuration.CollectibleIds[0])) labels.Add("Collectibles");
        if (cfg.CategoryFilters.Contains(Configuration.GlamourIds[0]))     labels.Add("Glamour");
        return labels.ToArray();
    }

    private static Configuration Snapshot(Configuration source)
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
        };
    }

    private static void RestoreFrom(Configuration snapshot, Configuration target)
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
    }

    private static void RestoreDefaults(Configuration target)
    {
        // Note: HomeWorld is preserved (player identity, not a tunable setting).
        // Reset only resets search/route/cache preferences.
        target.PreferredRoi            = 25;
        target.MinProfitAmount         = 10000;
        target.MinDesiredAvgPpu        = 10000;
        target.MaxBudgetPerSession        = 1_000_000;
        target.MinSalesPerDay          = 0.33;
        target.MinSalesPerWeek         = 2;
        target.RegionWide              = false;
        target.CategoryFilters         = (int[])Configuration.DefaultCategoryFilters.Clone();
        target.PreferredCategories     = new[] { "Furniture", "Collectibles", "Glamour" };
        target.IncludeVendors          = true;
        target.ShowOutOfStock          = true;
        target.MaxItemsPerSession      = 10;
        target.MaxServersToVisit       = 10;
        target.CacheDurationHours      = 4;
        target.EnableShortagePredictor = false;
    }
}
