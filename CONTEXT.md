# Namazu Flippers

A Dalamud plugin that finds daily cross-server market-board arbitrage opportunities
in FFXIV, presents them as a route-optimized buy/sell list, and tracks realized profit.

## Language

**Flip**:
Buying an item cheaply on one world and reselling it on the player's home world for
profit. The atomic unit of the strategy.
_Avoid_: trade, deal, arbitrage (use "flip" for the action, "opportunity" for a candidate)

**Opportunity**:
A single candidate flip surfaced by a scan — one item, its cheapest purchase world,
and its expected profit. Becomes a position once acted on.
_Avoid_: item (an item is the game object; an opportunity is the flip candidate), lead

**Capital Efficiency**:
The daily return on the gil deployed in a flip — `(profit per unit / purchase price)
× sales per day`. The primary signal for ranking opportunities, because the binding
constraint is profit per gil, not raw margin.
_Avoid_: ROI (ambiguous — ROI is the per-flip ratio; capital efficiency is per-gil-per-day), score

**Home World**:
The player's own world, where flipped items are listed for sale and price comparisons
are anchored.
_Avoid_: home server (the game calls them "worlds"; reserve "server" for the route's
purchase stops only where unavoidable), local world

**Out-of-Stock (OOS)**:
An item with zero current listings on the home world — a priority signal, since there's
no local supply competing with the player's sale.
_Avoid_: sold out, empty

**Route**:
The ordered set of purchase-world stops and the items to buy at each, plus the home
stop's items to list, for a single play session.
_Avoid_: path, plan, itinerary

**Absorption**:
How much capital the home-world market can clear at acceptable velocity before the
player's gil is stuck behind unsold listings — the FFXIV analog of an average-daily-
volume (ADV) participation limit. Caps per-opportunity position size.
_Avoid_: liquidity (too broad), demand

**Holding Window**:
The number of days the player is willing to let gil sit in unsold inventory before a
flip is considered stuck. Sets the absorption ceiling (`sales per day × holding window`)
and the window over which a sale is expected.
_Avoid_: hold time, sell-by, expiry

**Sell Confidence**:
A 0–1 estimate that a listed unit clears within the holding window, derived from expected
demand versus competing home-world listings. Discounts both an opportunity's rank and its
position size. Defaults to 1 (no penalty) when listing data is unavailable.
_Avoid_: probability, win rate, score
