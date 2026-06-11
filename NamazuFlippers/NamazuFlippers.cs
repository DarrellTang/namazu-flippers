using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NamazuFlippers.API;
using NamazuFlippers.Core;
using NamazuFlippers.Data;
using NamazuFlippers.UI;

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
    private readonly ScanCacheStore cacheStore;
    private readonly CancellationTokenSource scanCts = new();

    private readonly WindowSystem windowSystem = new("NamazuFlippers");
    private readonly FirstRunWindow firstRunWindow;
    private readonly DailyRouteWindow dailyRouteWindow;
    private readonly ConfigWindow configWindow;
    private int scanInProgress;

    /// <summary>
    /// Set when an API call fails. Rendered as an in-window error banner by Phase 4's OnDraw.
    /// Cleared on successful scan or user dismiss.
    /// </summary>
    public string? LastApiError { get; private set; }

    public ScanEngineResult? LatestScanResult { get; private set; }

    /// <summary>
    /// Latest SessionState read from the persisted scan-cache.json envelope.
    /// Populated after every scan (cache hit or API call) so DailyRouteWindow can
    /// hydrate its in-memory bought/listed dictionaries on first sight of a new
    /// ScanEngineResult (Phase 5 D-08). Null until the first scan completes.
    /// </summary>
    public SessionState? CurrentSessionState { get; private set; }

    public Configuration Configuration { get; set; }

    /// <summary>True while a scan is currently running. Used by DailyRouteWindow to disable Rescan.</summary>
    public bool ScanInProgress => Interlocked.CompareExchange(ref scanInProgress, 0, 0) == 1;

    /// <summary>Public wrapper around RunScanAsync(forceRefresh: true). Called by DailyRouteWindow's Rescan button (wired in 04-02).</summary>
    public Task RescanAsync(CancellationToken ct) => RunScanAsync(true, ct);

    /// <summary>Opens the ConfigWindow. Called by DailyRouteWindow's in-window Settings button (D-07).</summary>
    public void OpenConfigWindow() => configWindow.IsOpen = true;

    /// <summary>
    /// Queue a fire-and-forget save of the current bought/listed dictionaries to disk.
    /// Called from DailyRouteWindow checkbox handlers and Mark All buttons (D-04, D-05).
    /// </summary>
    public void QueueSessionSave(Dictionary<int, bool> bought, Dictionary<int, bool> listed)
    {
        // Snapshot the dictionaries off the UI thread so the background save sees a stable view.
        var snapshot = new SessionState
        {
            Bought = new Dictionary<int, bool>(bought),
            Listed = new Dictionary<int, bool>(listed),
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await cacheStore.SaveSessionAsync(snapshot, scanCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Plugin shutdown raced the save — fine, drop it.
            }
        }, scanCts.Token);
    }

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
        cacheStore = new ScanCacheStore(pluginInterface, Configuration, log);
        scanEngine = new ScanEngine(apiClient, Configuration, log, routeOptimizer, cacheStore);

        firstRunWindow = new FirstRunWindow(Configuration, pluginInterface, log);
        dailyRouteWindow = new DailyRouteWindow(this, log);
        configWindow = new ConfigWindow(this, pluginInterface, log);

        windowSystem.AddWindow(firstRunWindow);
        windowSystem.AddWindow(dailyRouteWindow);
        windowSystem.AddWindow(configWindow);

        // Show first-run popup on plugin load when home world is unset.
        if (string.IsNullOrEmpty(Configuration.HomeWorld))
            firstRunWindow.IsOpen = true;

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the Namazu Flippers daily arbitrage route window. Use /nflip scan to refresh the route."
        });

        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        clientState.Login += OnLogin;
        pluginInterface.UiBuilder.Draw += DrawWithDiagnostics;
        pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;

        if (clientState.IsLoggedIn)
            QueueAutoScan();

        log.Information("Namazu Flippers loaded. Use /nflip to get started.");
    }

    public void Dispose()
    {
        log.Information("Namazu Flippers Dispose starting.");
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        clientState.Login -= OnLogin;
        scanCts.Cancel();
        scanCts.Dispose();
        pluginInterface.UiBuilder.Draw -= DrawWithDiagnostics;
        pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        windowSystem.RemoveAllWindows();
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

        dailyRouteWindow.IsOpen = !dailyRouteWindow.IsOpen;
        log.Information(dailyRouteWindow.IsOpen
            ? "Namazu Flippers UI opened."
            : "Namazu Flippers UI closed.");

        if (dailyRouteWindow.IsOpen && string.IsNullOrEmpty(Configuration.HomeWorld))
        {
            firstRunWindow.IsOpen = true;
            log.Information("Set your home world in the popup to get started.");
        }
    }

    private void OnOpenConfigUi() => configWindow.IsOpen = true;

    private void OnLogin() => QueueAutoScan();

    private void QueueAutoScan()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), scanCts.Token).ConfigureAwait(false);
                await RunScanAsync(forceRefresh: false, scanCts.Token).ConfigureAwait(false);
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
            var result = await scanEngine.GetRouteAsync(forceRefresh, ct).ConfigureAwait(false);
            LatestScanResult = result;
            LastApiError = result.Status == ScanEngineStatus.Error ? result.UserMessage : null;

            // Phase 5 D-08: After every scan (cache hit OR API call), read the just-loaded/just-written
            // envelope back so DailyRouteWindow can hydrate its in-memory bought/listed dictionaries.
            // On a fresh API scan the envelope's SessionState is empty (clean slate); on a cache hit
            // the envelope's SessionState carries the previously-persisted clicks.
            var envelope = await cacheStore.LoadAnyAsync(ct);
            CurrentSessionState = envelope?.SessionState;

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
        catch (Exception ex)
        {
            log.Error(ex, "/nflip: scan failed with unexpected exception.");
        }
        finally
        {
            Interlocked.Exchange(ref scanInProgress, 0);
        }
    }

    private DateTime lastDrawHeartbeat = DateTime.MinValue;
    private static readonly TimeSpan DrawHeartbeatInterval = TimeSpan.FromSeconds(60);

    private void DrawWithDiagnostics()
    {
        try
        {
            var now = DateTime.UtcNow;
            if (now - lastDrawHeartbeat >= DrawHeartbeatInterval)
            {
                log.Information("/nflip: Draw heartbeat (tid={Tid}).", Environment.CurrentManagedThreadId);
                lastDrawHeartbeat = now;
            }
            windowSystem.Draw();
        }
        catch (Exception ex)
        {
            log.Error(ex, "/nflip: exception in Draw — rethrowing to Dalamud.");
            throw;
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        log.Error(e.Exception, "/nflip: unobserved task exception.");
        e.SetObserved();
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            log.Error(ex, "/nflip: AppDomain unhandled exception (terminating={Terminating}).", e.IsTerminating);
        else
            log.Error("/nflip: AppDomain unhandled non-Exception (terminating={Terminating}).", e.IsTerminating);
    }
}
