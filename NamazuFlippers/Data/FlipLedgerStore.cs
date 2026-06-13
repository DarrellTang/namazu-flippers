using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NamazuFlippers.API.Models;

namespace NamazuFlippers.Data;

public sealed class FlipLedgerStore
{
    private const string LedgerFileName = "flip-ledger.json";
    private const double MarketTaxRate = 0.95;

    private readonly IPluginLog log;
    private readonly string ledgerPath;
    private readonly string backupPath;
    private readonly SemaphoreSlim writeGate = new(1, 1);

    public FlipLedgerStore(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        var configDirectory = pluginInterface.ConfigDirectory.FullName;
        Directory.CreateDirectory(configDirectory);
        ledgerPath = Path.Combine(configDirectory, LedgerFileName);
        backupPath = ledgerPath + ".bak";
    }

    public async Task<FlipLedgerEnvelope> LoadAsync(CancellationToken ct = default)
    {
        var envelope = await LoadFromPathAsync(ledgerPath, ct).ConfigureAwait(false);
        if (envelope != null)
            return envelope;

        var backup = await LoadFromPathAsync(backupPath, ct).ConfigureAwait(false);
        if (backup != null)
        {
            log.Warning("/nflip: loaded flip ledger backup after primary ledger failed.");
            return backup;
        }

        return new FlipLedgerEnvelope();
    }

    public async Task<IReadOnlyList<FlipPosition>> LoadOpenPositionsAsync(CancellationToken ct = default)
    {
        var envelope = await LoadAsync(ct).ConfigureAwait(false);
        return envelope.Positions
            .Where(position => position.Status is not FlipPositionStatus.Sold and not FlipPositionStatus.Archived
                && position.RemainingQuantity > 0)
            .OrderBy(position => position.BuyTimestampUtc)
            .ToList();
    }

    public async Task<FlipPosition> AddPositionAsync(FlipPosition position, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(position);

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var envelope = await LoadAsync(ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;

            position.Id = string.IsNullOrWhiteSpace(position.Id) ? Guid.NewGuid().ToString("N") : position.Id;
            position.CreatedAtUtc = position.CreatedAtUtc == default ? now : position.CreatedAtUtc;
            position.UpdatedAtUtc = now;
            position.BoughtQuantity = Math.Max(1, position.BoughtQuantity);
            position.SoldQuantity = Math.Clamp(position.SoldQuantity, 0, position.BoughtQuantity);
            position.ListedQuantity = Math.Clamp(position.ListedQuantity, 0, position.BoughtQuantity);
            position.RemainingQuantity = Math.Max(0, position.BoughtQuantity - position.SoldQuantity);
            position.Status = position.RemainingQuantity > 0 ? position.Status : FlipPositionStatus.Sold;

            envelope.Positions.Add(position);
            await WriteEnvelopeAsync(envelope, ct).ConfigureAwait(false);
            return position;
        }
        finally
        {
            writeGate.Release();
        }
    }

    public async Task<bool> UpdateOpenPositionAsync(
        string positionId,
        int boughtQuantity,
        int actualUnitBuyPrice,
        string notes,
        CancellationToken ct = default)
    {
        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var envelope = await LoadAsync(ct).ConfigureAwait(false);
            var position = envelope.Positions.FirstOrDefault(p => p.Id == positionId);
            if (position == null)
                return false;

            position.BoughtQuantity = Math.Max(1, boughtQuantity);
            position.ActualUnitBuyPrice = Math.Max(1, actualUnitBuyPrice);
            position.SoldQuantity = Math.Clamp(position.SoldQuantity, 0, position.BoughtQuantity);
            position.ListedQuantity = Math.Clamp(position.ListedQuantity, 0, position.BoughtQuantity);
            position.RemainingQuantity = Math.Max(0, position.BoughtQuantity - position.SoldQuantity);
            position.PlannedUnitProfit =
                (int)Math.Floor(position.ExpectedUnitSellPrice * 0.95) - position.ActualUnitBuyPrice;
            position.Notes = notes.Trim();
            position.UpdatedAtUtc = DateTimeOffset.UtcNow;
            position.Status = position.RemainingQuantity > 0
                ? position.ListedQuantity > 0 ? FlipPositionStatus.Listed : FlipPositionStatus.Open
                : FlipPositionStatus.Sold;

            await WriteEnvelopeAsync(envelope, ct).ConfigureAwait(false);
            return true;
        }
        finally
        {
            writeGate.Release();
        }
    }

    public async Task<bool> DeletePositionAsync(string positionId, CancellationToken ct = default)
    {
        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var envelope = await LoadAsync(ct).ConfigureAwait(false);
            var removed = envelope.Positions.RemoveAll(position => position.Id == positionId) > 0;
            if (!removed)
                return false;

            await WriteEnvelopeAsync(envelope, ct).ConfigureAwait(false);
            return true;
        }
        finally
        {
            writeGate.Release();
        }
    }

    public async Task<FlipSale?> RecordSaleAsync(
        string positionId,
        int quantity,
        int actualUnitSalePrice,
        string notes,
        CancellationToken ct = default)
    {
        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var envelope = await LoadAsync(ct).ConfigureAwait(false);
            var position = envelope.Positions.FirstOrDefault(p => p.Id == positionId);
            if (position == null || position.RemainingQuantity <= 0)
                return null;

            var soldQuantity = Math.Clamp(quantity, 1, position.RemainingQuantity);
            var unitSalePrice = Math.Max(1, actualUnitSalePrice);
            var netUnitSalePrice = (int)Math.Floor(unitSalePrice * MarketTaxRate);
            var unitBuyPrice = ResolveUnitBuyPrice(position);
            var realizedUnitProfit = netUnitSalePrice - unitBuyPrice;
            position.Sales ??= [];
            var sale = new FlipSale
            {
                Id = Guid.NewGuid().ToString("N"),
                SoldAtUtc = DateTimeOffset.UtcNow,
                Quantity = soldQuantity,
                ActualUnitSalePrice = unitSalePrice,
                NetUnitSalePrice = netUnitSalePrice,
                UnitBuyPrice = unitBuyPrice,
                RealizedUnitProfit = realizedUnitProfit,
                TotalRealizedProfit = realizedUnitProfit * soldQuantity,
                Notes = notes.Trim(),
            };

            position.Sales.Add(sale);
            position.SoldQuantity = Math.Clamp(position.SoldQuantity + soldQuantity, 0, position.BoughtQuantity);
            position.RemainingQuantity = Math.Max(0, position.BoughtQuantity - position.SoldQuantity);
            position.ListedQuantity = Math.Clamp(
                Math.Max(position.ListedQuantity, position.SoldQuantity),
                0,
                position.BoughtQuantity);
            position.LastSoldAtUtc = sale.SoldAtUtc;
            position.TotalRealizedProfit = position.Sales.Sum(s => s.TotalRealizedProfit);
            position.UpdatedAtUtc = sale.SoldAtUtc;
            position.Status = position.RemainingQuantity > 0
                ? FlipPositionStatus.Listed
                : FlipPositionStatus.Sold;

            await WriteEnvelopeAsync(envelope, ct).ConfigureAwait(false);
            return sale;
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task<FlipLedgerEnvelope?> LoadFromPathAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);
            var envelope = await JsonSerializer.DeserializeAsync(
                stream,
                ApiJsonContext.Default.FlipLedgerEnvelope,
                ct).ConfigureAwait(false);

            return envelope?.SchemaVersion == FlipLedgerEnvelope.CurrentSchemaVersion
                ? envelope
                : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            log.Warning("/nflip: could not load flip ledger {Path}: {Message}", Path.GetFileName(path), ex.Message);
            return null;
        }
    }

    private async Task WriteEnvelopeAsync(FlipLedgerEnvelope envelope, CancellationToken ct)
    {
        envelope.SchemaVersion = FlipLedgerEnvelope.CurrentSchemaVersion;
        envelope.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (File.Exists(ledgerPath))
            File.Copy(ledgerPath, backupPath, overwrite: true);

        var tempPath = ledgerPath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                envelope,
                ApiJsonContext.Default.FlipLedgerEnvelope,
                ct).ConfigureAwait(false);
        }

        File.Move(tempPath, ledgerPath, overwrite: true);
    }

    private static int ResolveUnitBuyPrice(FlipPosition position)
    {
        if (position.ActualUnitBuyPrice > 0)
            return position.ActualUnitBuyPrice;

        var plannedUnitBuyPrice = (int)Math.Floor(position.ExpectedUnitSellPrice * MarketTaxRate)
            - position.PlannedUnitProfit;
        return Math.Max(1, plannedUnitBuyPrice);
    }
}
