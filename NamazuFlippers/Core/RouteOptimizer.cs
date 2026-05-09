using NamazuFlippers.Data;

namespace NamazuFlippers.Core;

public sealed class RouteOptimizer
{
    private const double FrictionTieBreakWindow = 0.20;

    public IReadOnlyList<RouteStop> Optimize(
        IReadOnlyList<RankedOpportunity> opportunities,
        Configuration configuration)
    {
        if (opportunities.Count == 0)
            return [];

        var stopLimit = Math.Max(1, configuration.MaxServersToVisit);
        var itemLimit = Math.Max(1, configuration.MaxItemsPerSession);
        var budget = configuration.MaxBudgetPerSession;

        // Apply the cumulative budget cap BEFORE grouping into stops: walk items in
        // profit-rank order and keep each one whose CheapestPrice fits the remaining
        // budget. Items above the remaining budget are skipped — keep filling with
        // cheaper-but-profitable items rather than stopping at the first overage.
        // Set MaxBudgetPerSession to 0 to disable the cap entirely.
        IReadOnlyList<RankedOpportunity> withinBudget;
        if (budget <= 0)
        {
            withinBudget = opportunities;
        }
        else
        {
            var kept = new List<RankedOpportunity>(opportunities.Count);
            long spent = 0;
            foreach (var item in opportunities)
            {
                var remaining = budget - spent;
                if (item.PurchasePrice <= remaining)
                {
                    kept.Add(item);
                    spent += item.PurchasePrice;
                }
            }
            withinBudget = kept;
        }

        var selectedStops = withinBudget
            .GroupBy(opportunity => opportunity.PurchaseSource, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateRouteStop(group, configuration.HomeWorld))
            .OrderBy(stop => stop, new RouteStopComparer())
            .Take(stopLimit)
            .ToList();

        return TrimItemsPreservingStopOrder(selectedStops, itemLimit);
    }

    private static RouteStop CreateRouteStop(
        IGrouping<string, RankedOpportunity> group,
        string homeWorld)
    {
        var orderedItems = group
            .OrderByDescending(item => item.ExpectedDailyProfit)
            .ThenByDescending(item => item.SalesPerDay)
            .ThenBy(item => item.PurchasePrice)
            .ToList();

        var purchaseSource = group.Key;
        var isVendorStop = orderedItems.Any(item => item.IsVendorSource) || WorldData.IsVendorSource(purchaseSource);

        return new RouteStop
        {
            PurchaseSource = purchaseSource,
            DataCenter = isVendorStop ? null : WorldData.GetDataCenter(purchaseSource),
            IsVendorStop = isVendorStop,
            TravelFriction = WorldData.GetTravelFriction(homeWorld, purchaseSource),
            TotalExpectedDailyProfit = orderedItems.Sum(item => item.ExpectedDailyProfit),
            Items = orderedItems,
        };
    }

    private static IReadOnlyList<RouteStop> TrimItemsPreservingStopOrder(
        IReadOnlyList<RouteStop> stops,
        int itemLimit)
    {
        var remaining = itemLimit;
        var trimmedStops = new List<RouteStop>();

        foreach (var stop in stops)
        {
            if (remaining <= 0)
                break;

            var items = stop.Items.Take(remaining).ToList();
            if (items.Count == 0)
                continue;

            remaining -= items.Count;
            trimmedStops.Add(new RouteStop
            {
                PurchaseSource = stop.PurchaseSource,
                DataCenter = stop.DataCenter,
                IsVendorStop = stop.IsVendorStop,
                TravelFriction = stop.TravelFriction,
                TotalExpectedDailyProfit = items.Sum(item => item.ExpectedDailyProfit),
                Items = items,
            });
        }

        return trimmedStops;
    }

    private sealed class RouteStopComparer : IComparer<RouteStop>
    {
        public int Compare(RouteStop? x, RouteStop? y)
        {
            if (ReferenceEquals(x, y))
                return 0;
            if (x == null)
                return 1;
            if (y == null)
                return -1;

            var higherValue = Math.Max(x.TotalExpectedDailyProfit, y.TotalExpectedDailyProfit);
            var lowerValue = Math.Min(x.TotalExpectedDailyProfit, y.TotalExpectedDailyProfit);

            if (IsWithinFrictionTieBreakWindow(higherValue, lowerValue))
            {
                var frictionCompare = x.TravelFriction.CompareTo(y.TravelFriction);
                if (frictionCompare != 0)
                    return frictionCompare;
            }

            var valueCompare = y.TotalExpectedDailyProfit.CompareTo(x.TotalExpectedDailyProfit);
            if (valueCompare != 0)
                return valueCompare;

            return string.Compare(x.PurchaseSource, y.PurchaseSource, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsWithinFrictionTieBreakWindow(int higherValue, int lowerValue)
    {
        if (higherValue <= 0)
            return true;

        return lowerValue >= higherValue * (1 - FrictionTieBreakWindow);
    }
}
