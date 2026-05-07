using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using NamazuFlippers.Data;
using System.Numerics;

namespace NamazuFlippers.UI;

/// <summary>
/// First-run home world selection popup. Offers an alphabetical dropdown of all 85 FFXIV worlds.
/// Appears automatically on first /nflip when no home world is configured.
/// Dismisses after a world is selected and confirmed.
/// Migrated from project root to UI/ in plan 04-01: now extends Dalamud.Interface.Windowing.Window.
/// </summary>
public class FirstRunWindow : Window
{
    private readonly Configuration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;

    private int selectedWorldIndex = -1;

    public FirstRunWindow(
        Configuration configuration,
        IDalamudPluginInterface pluginInterface,
        IPluginLog log)
        : base("Welcome to Namazu Flippers", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize)
    {
        this.configuration = configuration;
        this.pluginInterface = pluginInterface;
        this.log = log;
    }

    /// <summary>
    /// Whether the first-run popup is still pending (home world not yet set).
    /// </summary>
    public bool IsPending => string.IsNullOrEmpty(configuration.HomeWorld);

    /// <summary>
    /// Renders the first-run popup if pending. WindowSystem manages IsOpen visibility.
    /// </summary>
    public override void Draw()
    {
        if (!IsPending)
        {
            IsOpen = false;
            return;
        }

        ImGui.OpenPopup("Welcome to Namazu Flippers");

        var popupOpen = true;
        if (ImGui.BeginPopupModal("Welcome to Namazu Flippers", ref popupOpen,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
        {
            ImGui.Text("Select your home world:");

            // Dropdown combo — guaranteed valid, no typo or validation needed
            var preview = selectedWorldIndex >= 0 && selectedWorldIndex < WorldData.KnownWorlds.Length
                ? WorldData.KnownWorlds[selectedWorldIndex]
                : "Choose a world...";

            if (ImGui.BeginCombo("##home-world-combo", preview))
            {
                for (int i = 0; i < WorldData.KnownWorlds.Length; i++)
                {
                    var isSelected = i == selectedWorldIndex;
                    if (ImGui.Selectable(WorldData.KnownWorlds[i], isSelected))
                        selectedWorldIndex = i;

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            // Confirm button — saves the selected world
            var canConfirm = selectedWorldIndex >= 0 && selectedWorldIndex < WorldData.KnownWorlds.Length;
            if (!canConfirm)
                ImGui.BeginDisabled();

            bool confirmPressed = ImGui.Button("Confirm", new Vector2(120, 0));

            if (!canConfirm)
                ImGui.EndDisabled();

            if (confirmPressed && canConfirm)
            {
                configuration.HomeWorld = WorldData.KnownWorlds[selectedWorldIndex];
                pluginInterface.SavePluginConfig(configuration);

                log.Information($"Home world set to: {configuration.HomeWorld}");

                ImGui.CloseCurrentPopup();
                IsOpen = false;
            }

            ImGui.EndPopup();
        }
    }
}
