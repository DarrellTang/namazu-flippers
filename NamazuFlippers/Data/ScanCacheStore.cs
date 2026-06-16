using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NamazuFlippers.API.Models;
using NamazuFlippers.Core;

namespace NamazuFlippers.Data;

public sealed class ScanCacheStore
{
    private const string CacheFileName = "scan-cache.json";

    private readonly Configuration configuration;
    private readonly IPluginLog log;
    private readonly string cachePath;
    private readonly SemaphoreSlim writeGate = new(1, 1);

    public ScanCacheStore(IDalamudPluginInterface pluginInterface, Configuration configuration, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        var configDirectory = pluginInterface.ConfigDirectory.FullName;
        Directory.CreateDirectory(configDirectory);
        cachePath = Path.Combine(configDirectory, CacheFileName);
    }

    public async Task<ScanCacheEnvelope?> LoadValidAsync(CancellationToken ct = default)
    {
        var envelope = await LoadAnyAsync(ct).ConfigureAwait(false);
        if (envelope == null)
            return null;

        return IsValid(envelope, CreateConfigFingerprint(), DateTimeOffset.UtcNow)
            ? envelope
            : null;
    }

    public async Task<ScanCacheEnvelope?> LoadAnyAsync(CancellationToken ct = default)
    {
        if (!File.Exists(cachePath))
            return null;

        try
        {
            await using var stream = File.OpenRead(cachePath);
            return await JsonSerializer.DeserializeAsync(
                stream,
                ApiJsonContext.Default.ScanCacheEnvelope,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            log.Warning("/nflip: could not load scan cache: {Message}", ex.Message);
            return null;
        }
    }

    public async Task SaveAsync(ScanResponse rawResponse, ScanEngineResult result, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var envelope = new ScanCacheEnvelope
        {
            SchemaVersion = ScanCacheEnvelope.CurrentSchemaVersion,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(Math.Max(1, configuration.CacheDurationHours)),
            ConfigFingerprint = CreateConfigFingerprint(),
            RawResponse = rawResponse,
            DerivedResult = result,
        };

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await WriteEnvelopeAsync(envelope, ct).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }

    public async Task SaveSessionAsync(SessionState sessionState, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessionState);

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var envelope = await LoadAnyAsync(ct);
            if (envelope == null)
                return;

            envelope.SessionState = sessionState;
            await WriteEnvelopeAsync(envelope, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            log.Warning("/nflip: could not save session state: {Message}", ex.Message);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task WriteEnvelopeAsync(ScanCacheEnvelope envelope, CancellationToken ct)
    {
        var tempPath = cachePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                envelope,
                ApiJsonContext.Default.ScanCacheEnvelope,
                ct).ConfigureAwait(false);
        }

        File.Move(tempPath, cachePath, overwrite: true);
    }

    public string CreateConfigFingerprint()
    {
        var categoryFilters = string.Join(",", configuration.CategoryFilters.Order());
        var fingerprintInput = string.Join("|",
            configuration.HomeWorld,
            configuration.PreferredRoi,
            configuration.MinProfitAmount,
            configuration.MinDesiredAvgPpu,
            configuration.MinSalesPerWeek,
            configuration.MinSalesPerDay,
            configuration.RegionWide,
            configuration.IncludeVendors,
            configuration.ShowOutOfStock,
            configuration.MaxItemsPerSession,
            configuration.MaxServersToVisit,
            categoryFilters);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
    }

    public static bool IsValid(ScanCacheEnvelope envelope, string expectedFingerprint, DateTimeOffset nowUtc) =>
        envelope.SchemaVersion == ScanCacheEnvelope.CurrentSchemaVersion &&
        envelope.ExpiresAtUtc > nowUtc &&
        envelope.ConfigFingerprint == expectedFingerprint;
}
