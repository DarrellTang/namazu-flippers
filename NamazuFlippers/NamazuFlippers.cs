using Dalamud.Game.Command;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace NamazuFlippers;

/// <summary>
/// Namazu Flippers — Daily cross-server arbitrage route plugin.
/// Entry point implementing the Dalamud plugin lifecycle.
/// </summary>
public class NamazuFlippers : IDalamudPlugin
{
    /// <summary>
    /// Chat command to toggle the plugin UI.
    /// </summary>
    private const string CommandName = "/nflip";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;

    /// <summary>
    /// Plugin configuration — all CONF-01 through CONF-09 settings.
    /// </summary>
    public Configuration Configuration { get; set; }

    /// <summary>
    /// Whether the plugin UI is currently visible.
    /// </summary>
    private bool isVisible;

    /// <summary>
    /// Whether this is the first run (home world not yet set).
    /// Controls the first-run home world popup.
    /// </summary>
    private bool isFirstRun = true;

    /// <summary>
    /// Buffer for the home world input field in the first-run popup.
    /// </summary>
    private string pendingHomeWorld = "";

    /// <summary>
    /// Initializes the plugin, loads config, and registers the /nflip command.
    /// </summary>
    public NamazuFlippers(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // If home world is already set in persisted config, skip the first-run popup.
        if (!string.IsNullOrEmpty(Configuration.HomeWorld))
        {
            isFirstRun = false;
        }

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Namazu Flippers daily arbitrage route window."
        });

        pluginInterface.UiBuilder.Draw += OnDraw;

        log.Information("Namazu Flippers loaded. Use /nflip to get started.");
    }

    /// <summary>
    /// Disposes plugin resources and unregisters the /nflip command.
    /// Must not throw — clean shutdown is required for Dalamud lifecycle.
    /// </summary>
    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= OnDraw;
        commandManager.RemoveHandler(CommandName);
        log.Information("Namazu Flippers unloaded.");
    }

    /// <summary>
    /// Handles the /nflip chat command. Toggles the plugin UI and shows
    /// the first-run home world popup if no home world has been set.
    /// </summary>
    private void OnCommand(string command, string arguments)
    {
        isVisible = !isVisible;

        // Show first-run popup if home world is not yet set.
        if (isVisible && isFirstRun && string.IsNullOrEmpty(Configuration.HomeWorld))
        {
            pendingHomeWorld = "";
        }
    }

    /// <summary>
    /// Main draw callback invoked each frame by Dalamud's UI builder.
    /// Renders the first-run popup when active. Full UI windows are built in Phase 4.
    /// </summary>
    private void OnDraw()
    {
        DrawFirstRunPopup();
    }

    /// <summary>
    /// Renders the first-run home world selection popup.
    /// After the user confirms, the home world is saved and the popup dismisses.
    /// </summary>
    private void DrawFirstRunPopup()
    {
        if (isFirstRun && string.IsNullOrEmpty(Configuration.HomeWorld) && isVisible)
        {
            ImGui.OpenPopup("Welcome to Namazu Flippers");

            if (ImGui.BeginPopupModal("Welcome to Namazu Flippers", ref isVisible,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
            {
                ImGui.Text("Enter your home world to get started:");

                // Input field for home world name (max 32 chars)
                pendingHomeWorld = pendingHomeWorld.Length > 32
                    ? pendingHomeWorld[..32]
                    : pendingHomeWorld;
                ImGui.InputText("##home-world-input", ref pendingHomeWorld, 32);

                ImGui.Spacing();

                // Confirm button — saves home world and dismisses popup
                bool confirmPressed = ImGui.Button("Confirm", new Vector2(120, 0));

                if (confirmPressed && !string.IsNullOrWhiteSpace(pendingHomeWorld))
                {
                    Configuration.HomeWorld = pendingHomeWorld.Trim();
                    pluginInterface.SavePluginConfig(Configuration);
                    isFirstRun = false;

                    log.Information($"Home world set to: {Configuration.HomeWorld}");

                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }
    }
}
