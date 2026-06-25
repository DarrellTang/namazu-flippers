using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using NamazuFlippers.Data;
using System.Numerics;

namespace NamazuFlippers.UI;

public sealed class ProfitHistoryWindow : Window
{
    private readonly NamazuFlippers plugin;

    public ProfitHistoryWindow(NamazuFlippers plugin)
        : base("Namazu Flippers - Profit History", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        Size = new Vector2(560, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        var positions = plugin.LedgerPositions;
        var sales = positions
            .SelectMany(position => position.Sales.Select(sale => new SaleRow(position, sale)))
            .OrderByDescending(row => row.Sale.SoldAtUtc)
            .ToList();

        DrawRealizedSummary(sales);
        ImGui.Separator();

        if (ImGui.BeginTabBar("ProfitHistoryTabs##history"))
        {
            if (ImGui.BeginTabItem("Open"))
            {
                DrawOpenPositions(positions);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Sold"))
            {
                DrawSoldHistory(sales);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private static void DrawRealizedSummary(IReadOnlyList<SaleRow> sales)
    {
        var now = DateTimeOffset.Now;
        var today = now.Date;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        var todayProfit = sales
            .Where(row => row.Sale.SoldAtUtc.ToLocalTime().Date == today)
            .Sum(row => row.Sale.TotalRealizedProfit);
        var sevenDayProfit = sales
            .Where(row => row.Sale.SoldAtUtc >= sevenDaysAgo)
            .Sum(row => row.Sale.TotalRealizedProfit);
        var thirtyDayProfit = sales
            .Where(row => row.Sale.SoldAtUtc >= thirtyDaysAgo)
            .Sum(row => row.Sale.TotalRealizedProfit);

        ImGui.TextDisabled("Realized profit");
        ImGui.TextColored(UiColors.GilGold, $"Today: {todayProfit:n0} gil");
        ImGui.SameLine();
        ImGui.TextColored(UiColors.GilGold, $"7 days: {sevenDayProfit:n0} gil");
        ImGui.SameLine();
        ImGui.TextColored(UiColors.GilGold, $"30 days: {thirtyDayProfit:n0} gil");
    }

    private static void DrawOpenPositions(IReadOnlyList<FlipPosition> positions)
    {
        var openPositions = positions
            .Where(position => position.Status is not FlipPositionStatus.Sold and not FlipPositionStatus.Archived
                && position.RemainingQuantity > 0)
            .OrderBy(position => position.BuyTimestampUtc)
            .ToList();

        if (openPositions.Count == 0)
        {
            ImGui.TextDisabled("No open positions.");
            return;
        }

        foreach (var position in openPositions)
        {
            var projectedUnitProfit = (int)Math.Floor(position.ExpectedUnitSellPrice * 0.95)
                - ResolveUnitBuyPrice(position);
            var projectedRemainingProfit = projectedUnitProfit * position.RemainingQuantity;

            ImGui.TextUnformatted(position.ItemName);
            ImGui.TextDisabled(
                $"Bought {position.BuyTimestampUtc.ToLocalTime():MMM d HH:mm} from {position.SourceWorld} - {position.RemainingQuantity}/{position.BoughtQuantity} open");
            ImGui.TextDisabled(
                $"Projected/unit {projectedUnitProfit:n0} - projected remaining {projectedRemainingProfit:n0} gil");
            ImGui.Separator();
        }
    }

    private static void DrawSoldHistory(IReadOnlyList<SaleRow> sales)
    {
        if (sales.Count == 0)
        {
            ImGui.TextDisabled("No sold positions yet.");
            return;
        }

        var groups = sales
            .GroupBy(row => row.Position.BuyTimestampUtc.ToLocalTime().Date)
            .OrderByDescending(group => group.Key);

        foreach (var group in groups)
        {
            var realizedProfit = group.Sum(row => row.Sale.TotalRealizedProfit);
            var quantity = group.Sum(row => row.Sale.Quantity);
            var label = $"{group.Key:MMM d, yyyy} - {quantity} sold - realized {realizedProfit:n0} gil";
            if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen))
                continue;

            foreach (var row in group.OrderByDescending(row => row.Sale.SoldAtUtc))
            {
                ImGui.TextUnformatted(row.Position.ItemName);
                ImGui.TextDisabled(
                    $"Sold {row.Sale.SoldAtUtc.ToLocalTime():MMM d HH:mm} - qty {row.Sale.Quantity} - sale {row.Sale.ActualUnitSalePrice:n0} - net {row.Sale.NetUnitSalePrice:n0}");
                ImGui.TextColored(
                    UiColors.GilGold,
                    $"Realized/unit {row.Sale.RealizedUnitProfit:n0} - total {row.Sale.TotalRealizedProfit:n0} gil");
            }
        }
    }

    private static int ResolveUnitBuyPrice(FlipPosition position)
    {
        if (position.ActualUnitBuyPrice > 0)
            return position.ActualUnitBuyPrice;

        var plannedUnitBuyPrice = (int)Math.Floor(position.ExpectedUnitSellPrice * 0.95)
            - position.PlannedUnitProfit;
        return Math.Max(1, plannedUnitBuyPrice);
    }

    private sealed record SaleRow(FlipPosition Position, FlipSale Sale);
}
