using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace NamazuFlippers;

/// <summary>
/// Namazu Flippers — Daily cross-server arbitrage route plugin.
/// Plugin entry point. Keeps lifecycle thin; delegates UI to dedicated window classes.
/// </summary>
public class NamazuFlippers : IDalamudPlugin
{
    private const string CommandName = "/nflip";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;

    private readonly FirstRunWindow firstRunWindow;
    private bool isVisible;

    public Configuration Configuration { get; set; }

    public NamazuFlippers(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        firstRunWindow = new FirstRunWindow(Configuration, pluginInterface, log, () => isVisible);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Namazu Flippers daily arbitrage route window."
        });

        pluginInterface.UiBuilder.Draw += OnDraw;

        log.Information("Namazu Flippers loaded. Use /nflip to get started.");
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= OnDraw;
        commandManager.RemoveHandler(CommandName);
        log.Information("Namazu Flippers unloaded.");
    }

    private void OnCommand(string command, string arguments)
    {
        isVisible = !isVisible;
        log.Information(isVisible
            ? "Namazu Flippers UI opened."
            : "Namazu Flippers UI closed.");

        // Placeholder — DailyRouteWindow and ConfigWindow are built in Phase 4.
        if (isVisible && string.IsNullOrEmpty(Configuration.HomeWorld))
            log.Information("Set your home world in the popup to get started.");
    }

    private void OnDraw()
    {
        firstRunWindow.Draw();

        // Future: routeWindow.Draw(), configWindow.Draw(), etc.
    }
}
