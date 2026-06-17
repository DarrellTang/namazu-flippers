using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using NamazuFlippers.Data;
using System.Numerics;

namespace NamazuFlippers.UI;

public sealed class PositionsWindow : Window
{
    private readonly NamazuFlippers plugin;
    private readonly IPluginLog log;
    private readonly Dictionary<string, int> quantityInputs = new();
    private readonly Dictionary<string, int> unitPriceInputs = new();
    private readonly Dictionary<string, int> soldQuantityInputs = new();
    private readonly Dictionary<string, int> salePriceInputs = new();

    private string? pendingDeleteId;
    private string pendingDeleteName = "";
    private bool openDeleteConfirmation;
    private FlipPosition? pendingSalePosition;
    private bool openSaleConfirmation;

    public PositionsWindow(NamazuFlippers plugin, IPluginLog log)
        : base("Namazu Flippers — Open Positions", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        this.log = log;
        Size = new Vector2(460, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 280),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        var positions = plugin.OpenPositions;
        if (positions.Count == 0)
        {
            ImGui.TextDisabled("No open bought lots yet.");
            return;
        }

        ImGui.TextDisabled($"{positions.Count} open bought lot(s)");
        ImGui.Separator();

        foreach (var position in positions)
            DrawPosition(position);

        DrawSoldPopup();
        DrawDeletePopup();
    }

    private void DrawPosition(FlipPosition position)
    {
        EnsureInputs(position);
        ImGui.PushID(position.Id);
        try
        {
            ImGui.TextUnformatted(position.ItemName);
            ImGui.TextDisabled(
                $"Bought {position.BuyTimestampUtc.ToLocalTime():MMM d HH:mm} from {position.SourceWorld} • {position.RemainingQuantity} remaining");

            var quantity = quantityInputs[position.Id];
            if (ImGui.InputInt("Qty", ref quantity))
                quantityInputs[position.Id] = Math.Max(1, quantity);

            var unitPrice = unitPriceInputs[position.Id];
            if (ImGui.InputInt("Unit buy", ref unitPrice))
                unitPriceInputs[position.Id] = Math.Max(1, unitPrice);

            var projectedUnitProfit = (int)Math.Floor(position.ExpectedUnitSellPrice * 0.95)
                - unitPriceInputs[position.Id];
            ImGui.TextDisabled(
                $"Expected list {position.ExpectedUnitSellPrice:n0} • planned/unit {projectedUnitProfit:n0} gil");

            if (ImGui.Button("Save", new Vector2(90, 0)))
            {
                plugin.QueueOpenPositionCorrection(
                    position.Id,
                    quantityInputs[position.Id],
                    unitPriceInputs[position.Id],
                    position.Notes);
            }

            ImGui.SameLine();
            if (ImGui.Button("Sold", new Vector2(90, 0)))
            {
                soldQuantityInputs[position.Id] = Math.Max(1, position.RemainingQuantity);
                salePriceInputs[position.Id] = Math.Max(1, position.ExpectedUnitSellPrice);
                pendingSalePosition = position;
                openSaleConfirmation = true;
            }

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, UiColors.ErrorRed);
            if (ImGui.Button("Delete", new Vector2(90, 0)))
            {
                pendingDeleteId = position.Id;
                pendingDeleteName = position.ItemName;
                openDeleteConfirmation = true;
            }
            ImGui.PopStyleColor();

            ImGui.Separator();
        }
        finally
        {
            ImGui.PopID();
        }
    }

    private void DrawSoldPopup()
    {
        if (openSaleConfirmation)
        {
            ImGui.OpenPopup("RecordSoldLot##positions");
            openSaleConfirmation = false;
        }

        var saleOpen = true;
        if (ImGui.BeginPopupModal("RecordSoldLot##positions", ref saleOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (pendingSalePosition == null)
            {
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            var position = pendingSalePosition;
            ImGui.TextUnformatted(position.ItemName);
            ImGui.TextDisabled($"Bought {position.BuyTimestampUtc.ToLocalTime():MMM d} • {position.RemainingQuantity} remaining");
            ImGui.Spacing();

            var soldQuantity = soldQuantityInputs[position.Id];
            if (ImGui.InputInt("Sold qty", ref soldQuantity))
                soldQuantityInputs[position.Id] = Math.Clamp(soldQuantity, 1, Math.Max(1, position.RemainingQuantity));

            var salePrice = salePriceInputs[position.Id];
            if (ImGui.InputInt("Unit sale", ref salePrice))
                salePriceInputs[position.Id] = Math.Max(1, salePrice);

            var netUnit = (int)Math.Floor(salePriceInputs[position.Id] * 0.95);
            var unitProfit = netUnit - ResolveUnitBuyPrice(position);
            var totalProfit = unitProfit * soldQuantityInputs[position.Id];
            ImGui.TextDisabled($"After tax {netUnit:n0} • realized/unit {unitProfit:n0} • total {totalProfit:n0} gil");
            ImGui.Spacing();

            if (ImGui.Button("Record Sale", new Vector2(120, 0)))
            {
                plugin.QueuePositionSold(
                    position.Id,
                    soldQuantityInputs[position.Id],
                    salePriceInputs[position.Id],
                    notes: "");
                pendingSalePosition = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                pendingSalePosition = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void DrawDeletePopup()
    {
        if (openDeleteConfirmation)
        {
            ImGui.OpenPopup("DeletePosition##positions");
            openDeleteConfirmation = false;
        }

        var deleteOpen = true;
        if (ImGui.BeginPopupModal("DeletePosition##positions", ref deleteOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped($"Delete the open lot for {pendingDeleteName}?");
            ImGui.Spacing();
            if (ImGui.Button("Delete", new Vector2(120, 0)) && pendingDeleteId != null)
            {
                log.Information("/nflip: deleting open lot {PositionId}.", pendingDeleteId);
                plugin.QueueOpenPositionDelete(pendingDeleteId);
                pendingDeleteId = null;
                pendingDeleteName = "";
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                pendingDeleteId = null;
                pendingDeleteName = "";
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private void EnsureInputs(FlipPosition position)
    {
        if (!quantityInputs.ContainsKey(position.Id))
            quantityInputs[position.Id] = Math.Max(1, position.BoughtQuantity);
        if (!unitPriceInputs.ContainsKey(position.Id))
            unitPriceInputs[position.Id] = Math.Max(1, position.ActualUnitBuyPrice);
        if (!soldQuantityInputs.ContainsKey(position.Id))
            soldQuantityInputs[position.Id] = Math.Max(1, position.RemainingQuantity);
        if (!salePriceInputs.ContainsKey(position.Id))
            salePriceInputs[position.Id] = Math.Max(1, position.ExpectedUnitSellPrice);
    }

    private static int ResolveUnitBuyPrice(FlipPosition position)
    {
        if (position.ActualUnitBuyPrice > 0)
            return position.ActualUnitBuyPrice;

        var plannedUnitBuyPrice = (int)Math.Floor(position.ExpectedUnitSellPrice * 0.95)
            - position.PlannedUnitProfit;
        return Math.Max(1, plannedUnitBuyPrice);
    }
}
