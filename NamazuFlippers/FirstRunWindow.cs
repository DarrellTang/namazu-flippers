using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace NamazuFlippers;

/// <summary>
/// First-run home world selection popup. Validates against all 85 known FFXIV worlds.
/// Appears automatically on first /nflip when no home world is configured.
/// Dismisses after a valid world is confirmed and saved.
/// </summary>
public class FirstRunWindow
{
    private readonly Configuration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;
    private readonly Func<bool> isVisible;

    private string pendingHomeWorld = "";

    /// <summary>
    /// All 85 FFXIV worlds as of Dawntrail 7.x. Used to validate home world input.
    /// A world picker dropdown will replace this validation in Phase 4 (ConfigWindow).
    /// </summary>
    private static readonly HashSet<string> KnownWorlds = new(StringComparer.OrdinalIgnoreCase)
    {
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
    };

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
            ImGui.Text("Enter your home world to get started:");

            // Input field for home world name (max 32 chars)
            pendingHomeWorld = pendingHomeWorld.Length > 32
                ? pendingHomeWorld[..32]
                : pendingHomeWorld;
            ImGui.InputText("##home-world-input", ref pendingHomeWorld, 32);

            ImGui.Spacing();

            // Confirm button — validates and saves home world
            bool confirmPressed = ImGui.Button("Confirm", new Vector2(120, 0));

            if (confirmPressed && !string.IsNullOrWhiteSpace(pendingHomeWorld))
            {
                var trimmed = pendingHomeWorld.Trim();
                if (!KnownWorlds.Contains(trimmed))
                {
                    ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1),
                        $"\"{trimmed}\" is not a recognized world.");
                }
                else
                {
                    configuration.HomeWorld = trimmed;
                    pluginInterface.SavePluginConfig(configuration);

                    log.Information($"Home world set to: {configuration.HomeWorld}");

                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }
    }
}
