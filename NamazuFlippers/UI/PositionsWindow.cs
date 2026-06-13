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

    private string? pendingDeleteId;
    private string pendingDeleteName = "";
    private bool openDeleteConfirmation;

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
    }
}
