using NamazuFlippers.Data;

namespace NamazuFlippers.Core;

public sealed class RouteOptimizer
{
    public IReadOnlyList<RouteStop> Optimize(
        IReadOnlyList<RankedOpportunity> opportunities,
        Configuration configuration)
    {
        if (opportunities.Count == 0)
            return [];

        var stopLimit = Math.Max(1, configuration.MaxServersToVisit);
        var itemLimit = Math.Max(1, configuration.MaxItemsPerSession);

        // Kelly sizing (ScanEngine) now owns the budget; the route no longer re-applies a cap
        // (criterion 12). It just groups the sized (item, quantity) set into one stop per purchase
        // world and minimizes hops. Items Kelly sized to zero have nothing to buy, so they're
        // dropped before grouping.
        var sized = opportunities
            .Where(opportunity => opportunity.RecommendedQuantity > 0)
            .ToList();

        if (sized.Count == 0)
            return [];

        // World travel is treated as free (criterion 12) — stops are ordered purely by value, so
        // there is no travel-cost term in the selection.
        var selectedStops = sized
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
            .OrderByDescending(item => item.FinalRank)
            .ThenByDescending(item => item.ExpectedDailyProfit)
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

    // World travel is free (criterion 12), so stops are ordered purely by value with a stable
    // name tiebreak — no travel-friction term enters the selection.
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

            var valueCompare = y.TotalExpectedDailyProfit.CompareTo(x.TotalExpectedDailyProfit);
            if (valueCompare != 0)
                return valueCompare;

            return string.Compare(x.PurchaseSource, y.PurchaseSource, StringComparison.OrdinalIgnoreCase);
        }
    }
}
