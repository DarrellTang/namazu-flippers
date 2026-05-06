using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NamazuFlippers.API;

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

    private readonly RateLimiter rateLimiter;
    private readonly SaddlebagClient apiClient;

    private readonly FirstRunWindow firstRunWindow;
    private bool isVisible;

    /// <summary>
    /// Set when an API call fails. Rendered as an in-window error banner by Phase 4's OnDraw.
    /// Cleared on successful scan or user dismiss.
    /// </summary>
    public string? LastApiError { get; private set; }

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

        rateLimiter = new RateLimiter(TimeSpan.FromMilliseconds(1000));
        apiClient = new SaddlebagClient(Configuration, log, rateLimiter);

        firstRunWindow = new FirstRunWindow(Configuration, pluginInterface, log, () => isVisible);

        // Phase 2: SaddlebagClient is instantiated and ready.
        // Phase 3 ScanEngine will call apiClient.ScanAsync().
        // For now, a placeholder demonstrates the fire-and-forget error surfacing pattern:
        // _ = Task.Run(async () => {
        //     try { var result = await apiClient.ScanAsync(CancellationToken.None); LastApiError = null; }
        //     catch (ApiException ex) { log.Error($"/nflip: {ex.Message}"); LastApiError = ex.Message; }
        // });

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
