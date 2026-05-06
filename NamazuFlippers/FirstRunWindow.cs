using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace NamazuFlippers;

/// <summary>
/// First-run home world selection popup. Offers an alphabetical dropdown of all 85 FFXIV worlds.
/// Appears automatically on first /nflip when no home world is configured.
/// Dismisses after a world is selected and confirmed.
/// </summary>
public class FirstRunWindow
{
    private readonly Configuration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly Func<bool> isVisible;

    private int selectedWorldIndex = -1;

    /// <summary>
    /// All 85 FFXIV worlds as of Dawntrail 7.x, sorted alphabetically for the dropdown picker.
    /// </summary>
    private static readonly string[] KnownWorlds =
    [
        "Adamantoise", "Aegis", "Alexander", "Alpha", "Anima", "Asura", "Atomos",
        "Bahamut", "Balmung", "Behemoth", "Belias", "Bismarck", "Brynhildr",
        "Cactuar", "Carbuncle", "Cerberus", "Chocobo", "Coeurl", "Cuchulainn",
        "Diabolos", "Durandal",
        "Excalibur", "Exodus",
        "Faerie", "Famfrit", "Fenrir",
        "Garuda", "Gilgamesh", "Goblin", "Golem", "Gungnir",
        "Hades", "Halicarnassus", "Hyperion",
        "Ifrit", "Ixion",
        "Jenova",
        "Kraken", "Kujata",
        "Lamia", "Leviathan", "Lich", "Louisoix",
        "Maduin", "Malboro", "Mandragora", "Marilith", "Masamune", "Mateus",
        "Midgardsormr", "Moogle",
        "Odin", "Omega",
        "Pandaemonium", "Phantom", "Phoenix",
        "Rafflesia", "Ragnarok", "Raiden", "Ramuh", "Ravana", "Ridill",
        "Sagittarius", "Sargatanas", "Sephirot", "Seraph", "Shinryu", "Shiva",
        "Siren", "Sophia", "Spriggan",
        "Tiamat", "Titan", "Tonberry", "Twintania", "Typhon",
        "Ultima", "Ultros", "Unicorn",
        "Valefor",
        "Yojimbo",
        "Zalera", "Zeromus", "Zodiark", "Zurvan",
    ];

    public FirstRunWindow(
        Configuration configuration,
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        Func<bool> isVisible)
    {
        this.configuration = configuration;
        this.pluginInterface = pluginInterface;
        this.log = log;
        this.isVisible = isVisible;
    }

    /// <summary>
    /// Whether the first-run popup is still pending (home world not yet set).
    /// </summary>
    public bool IsPending => string.IsNullOrEmpty(configuration.HomeWorld);

    /// <summary>
    /// Renders the first-run popup if pending and the plugin UI is visible.
    /// Called each frame from the plugin's draw callback.
    /// </summary>
    public void Draw()
    {
        if (!IsPending || !isVisible())
            return;

        ImGui.OpenPopup("Welcome to Namazu Flippers");

        var popupOpen = true;
        if (ImGui.BeginPopupModal("Welcome to Namazu Flippers", ref popupOpen,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
        {
            ImGui.Text("Select your home world:");

            // Dropdown combo — guaranteed valid, no typo or validation needed
            var preview = selectedWorldIndex >= 0 && selectedWorldIndex < KnownWorlds.Length
                ? KnownWorlds[selectedWorldIndex]
                : "Choose a world...";

            if (ImGui.BeginCombo("##home-world-combo", preview))
            {
                for (int i = 0; i < KnownWorlds.Length; i++)
                {
                    var isSelected = i == selectedWorldIndex;
                    if (ImGui.Selectable(KnownWorlds[i], isSelected))
                        selectedWorldIndex = i;

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();

            // Confirm button — saves the selected world
            var canConfirm = selectedWorldIndex >= 0 && selectedWorldIndex < KnownWorlds.Length;
            if (!canConfirm)
                ImGui.BeginDisabled();

            bool confirmPressed = ImGui.Button("Confirm", new Vector2(120, 0));

            if (!canConfirm)
                ImGui.EndDisabled();

            if (confirmPressed && canConfirm)
            {
                configuration.HomeWorld = KnownWorlds[selectedWorldIndex];
                pluginInterface.SavePluginConfig(configuration);

                log.Information($"Home world set to: {configuration.HomeWorld}");

                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }
}
