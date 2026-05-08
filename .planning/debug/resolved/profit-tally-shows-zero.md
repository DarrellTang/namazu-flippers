---
status: resolved
trigger: "profit tally shows zero even though listed checkboxes are checked"
created: 2026-05-07T00:00:00Z
updated: 2026-05-08T01:00:00Z
symptoms_prefilled: true
goal: find_root_cause_only
---

## Current Focus

hypothesis: "The listed-checkbox column never renders because isHomeStop resolves to false: RouteStop.PurchaseSource is set to RankedOpportunity.PurchaseSource (the server where items are cheapest, i.e. the away server), never to HomeWorld. So no RouteStop ever has PurchaseSource == HomeWorld, DrawItems is never called with isHomeStop=true, no ##listed-{itemId} checkboxes appear, listedState stays empty, and listedProfit = 0 every frame."
test: "Traced RouteOptimizer.CreateRouteStop: PurchaseSource = group.Key = opportunity.PurchaseSource = CheapestServer (away server). Traced DailyRouteWindow.DrawRouteStop: isHomeStop = stop.PurchaseSource.Equals(plugin.Configuration.HomeWorld). These will never match for a cross-world arbitrage workflow where cheap servers are always different from home."
expecting: "Confirmed — no home stop exists in RouteStops, so isHomeStop is always false, so ##listed checkboxes are never rendered."
next_action: "DIAGNOSED — return ROOT CAUSE FOUND"

## Symptoms

expected: "Profit tally displays sum of ExpectedDailyProfit for listed items in GilGold and updates each frame as listed checkboxes are toggled"
actual: "Profit still shows zero even though user has checked some of the boxes. Listing price is much higher than what the profit suggests."
errors: "None reported"
reproduction: "Open DailyRouteWindow with fresh scan, expand any stop, check listed-checkboxes (if visible), observe profit tally stays 0"
started: "Discovered during Phase 4 UAT"

## Eliminated

- hypothesis: "listedState wiring is broken — clicking ##listed-{itemId} does not flip listedState[itemId]"
  evidence: "Code at DailyRouteWindow.cs:249-250 is correct: var listed = listedState.GetValueOrDefault(item.ItemId); if (ImGui.Checkbox(...)) listedState[item.ItemId] = listed; The wiring is identical to the bought pattern at lines 209-211 which user confirmed works."
  timestamp: 2026-05-07T00:00:00Z

- hypothesis: "LINQ profit formula is wrong — Where/Sum executes incorrectly"
  evidence: "Lines 115-117 of DailyRouteWindow.cs are syntactically correct. The null-coalescing chain and LINQ are standard. If listedState were populated, the formula would compute correctly."
  timestamp: 2026-05-07T00:00:00Z

- hypothesis: "User was checking bought-checkboxes (not listed-checkboxes) — bought state does not feed profit tally"
  evidence: "Partially true but not the root cause. The deeper issue is that listed-checkboxes are never rendered at all (see root cause below), so the user could not check listed boxes even if they wanted to."
  timestamp: 2026-05-07T00:00:00Z

## Evidence

- timestamp: 2026-05-07T00:00:00Z
  checked: "DailyRouteWindow.cs DrawRouteStop isHomeStop determination (line 196-197)"
  found: "isHomeStop = !stop.IsVendorStop && stop.PurchaseSource.Equals(plugin.Configuration.HomeWorld, StringComparison.OrdinalIgnoreCase)"
  implication: "Home stop identification depends entirely on RouteStop.PurchaseSource matching Configuration.HomeWorld."

- timestamp: 2026-05-07T00:00:00Z
  checked: "RankedOpportunity.PurchaseSource (RankedOpportunity.cs) and ScanEngine.ToOpportunity (ScanEngine.cs line 200)"
  found: "RankedOpportunity.PurchaseSource = item.CheapestServer — this is the away/cheap server, never HomeWorld"
  implication: "Every opportunity's PurchaseSource is a non-home server."

- timestamp: 2026-05-07T00:00:00Z
  checked: "RouteOptimizer.CreateRouteStop (RouteOptimizer.cs lines 39-50)"
  found: "RouteStop.PurchaseSource = group.Key = opportunity.PurchaseSource = CheapestServer. The home world is only used to compute TravelFriction; it is never assigned to PurchaseSource."
  implication: "No RouteStop ever has PurchaseSource == HomeWorld. The home world never appears as a stop's PurchaseSource."

- timestamp: 2026-05-07T00:00:00Z
  checked: "RouteStop model (RouteStop.cs) — IsHome property"
  found: "RouteStop has no IsHome, IsHomeStop, or HomeStop property. The only stop-classification flags are IsVendorStop and PurchaseSource string."
  implication: "There is no data-layer signal for 'this is where you list items.' The UI is trying to derive that from a name match that will never succeed."

- timestamp: 2026-05-07T00:00:00Z
  checked: "DailyRouteWindow.DrawItems (lines 245-253) — when is isHomeStop=true reached"
  found: "isHomeStop guard controls the entire listed-checkbox block. If isHomeStop is always false, the ##listed-{itemId} Checkbox call at line 249 is never executed, listedState is never written, listedProfit remains 0."
  implication: "Root cause confirmed: isHomeStop is structurally always false → listed-checkboxes never appear → listedState always empty → profit tally always 0."

- timestamp: 2026-05-07T00:00:00Z
  checked: "User report: 'listing price is much higher than what the profit suggests'"
  found: "User is likely checking bought-checkboxes on all stops (which work correctly) and expecting those to affect the profit tally. The listed column for home items never appears. The semantic mismatch (ExpectedDailyProfit vs per-flip margin) is secondary — the primary issue is the column never renders."
  implication: "Both user observations (zero profit, listing price discrepancy) trace to the same root cause: no listed-checkboxes ever rendered."

## Resolution

root_cause: "RouteStop.PurchaseSource is set to RankedOpportunity.PurchaseSource (the cheap away server), which can never equal Configuration.HomeWorld. DailyRouteWindow.DrawRouteStop computes isHomeStop as stop.PurchaseSource.Equals(HomeWorld), which is always false. Therefore DrawItems is never called with isHomeStop=true, the ##listed-{itemId} checkbox column is never rendered, listedState stays permanently empty, and listedProfit = 0 every frame."
fix: ""
verification: ""
files_changed: []
