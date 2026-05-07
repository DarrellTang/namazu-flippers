using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NamazuFlippers.API;
using NamazuFlippers.Core;
using NamazuFlippers.Data;

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
    private readonly IClientState clientState;
    private readonly IPluginLog log;

    private readonly RateLimiter rateLimiter;
    private readonly SaddlebagClient apiClient;
    private readonly ScanEngine scanEngine;
    private readonly CancellationTokenSource scanCts = new();

    private readonly FirstRunWindow firstRunWindow;
    private int scanInProgress;
    private bool isVisible;

    /// <summary>
    /// Set when an API call fails. Rendered as an in-window error banner by Phase 4's OnDraw.
    /// Cleared on successful scan or user dismiss.
    /// </summary>
    public string? LastApiError { get; private set; }

    public ScanEngineResult? LatestScanResult { get; private set; }

    public Configuration Configuration { get; set; }

    public NamazuFlippers(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IClientState clientState,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.clientState = clientState;
        this.log = log;

        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        rateLimiter = new RateLimiter(TimeSpan.FromMilliseconds(1000));
        apiClient = new SaddlebagClient(Configuration, log, rateLimiter);
        var routeOptimizer = new RouteOptimizer();
        var cacheStore = new ScanCacheStore(pluginInterface, Configuration, log);
        scanEngine = new ScanEngine(apiClient, Configuration, log, routeOptimizer, cacheStore);

        firstRunWindow = new FirstRunWindow(Configuration, pluginInterface, log, () => isVisible);

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Namazu Flippers daily arbitrage route window. Use /nflip scan to refresh the route."
        });

        clientState.Login += OnLogin;
        pluginInterface.UiBuilder.Draw += OnDraw;

        if (clientState.IsLoggedIn)
            QueueAutoScan();

        log.Information("Namazu Flippers loaded. Use /nflip to get started.");
    }

    public void Dispose()
    {
        clientState.Login -= OnLogin;
        scanCts.Cancel();
        scanCts.Dispose();
        pluginInterface.UiBuilder.Draw -= OnDraw;
        commandManager.RemoveHandler(CommandName);
        log.Information("Namazu Flippers unloaded.");
    }

    private void OnCommand(string command, string arguments)
    {
        var subcommand = arguments.Trim();
        if (subcommand.Equals("scan", StringComparison.OrdinalIgnoreCase))
        {
            _ = RunScanAsync(forceRefresh: true, scanCts.Token);
            return;
        }

        if (!string.IsNullOrEmpty(subcommand))
        {
            log.Information("/nflip: Unknown command. Use /nflip or /nflip scan.");
            return;
        }

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

    private void OnLogin() => QueueAutoScan();

    private void QueueAutoScan()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), scanCts.Token);
                await RunScanAsync(forceRefresh: false, scanCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal plugin shutdown path.
            }
        }, scanCts.Token);
    }

    private async Task RunScanAsync(bool forceRefresh, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Configuration.HomeWorld))
        {
            log.Information("/nflip: set your home world before scanning.");
            return;
        }

        if (Interlocked.Exchange(ref scanInProgress, 1) == 1)
        {
            log.Information("/nflip: scan already running.");
            return;
        }

        try
        {
            var result = await scanEngine.GetRouteAsync(forceRefresh, ct);
            LatestScanResult = result;
            LastApiError = result.Status == ScanEngineStatus.Error ? result.UserMessage : null;

            log.Information(
                "/nflip: scan {Status}; {Stops} stops, {Items} items, {Profit:n0} expected daily profit.",
                result.Status,
                result.RouteStops.Count,
                result.Opportunities.Count,
                result.TotalExpectedDailyProfit);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            log.Information("/nflip: scan cancelled.");
        }
        finally
        {
            Interlocked.Exchange(ref scanInProgress, 0);
        }
    }
}
