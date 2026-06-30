# Rank opportunities by capital efficiency, not absolute profit

We rank flip opportunities by **capital efficiency** — `(profit per unit / purchase
price) × sales per day`, further multiplied by a 0–1 sell-confidence — instead of by
expected absolute daily profit (the prior `ProfitPerUnit × SalesPerDay` sort).

**Why:** the binding constraint is profit *per gil deployed per day*, not raw margin.
Absolute-profit ranking systematically favored high-margin, slow-moving items (the
observed housing-item skew) that lock up capital. Capital efficiency is the GMROI /
Kelly-aligned formulation that recycles gil fastest. The absolute `MinProfitAmount`
floor and `MaxItemsPerSession` cap remain, so we don't fill the route with penny-flips.

**Trade-off:** cheap fast-moving items now rank above fat-margin slow ones — intended,
but it reverses the prior visible ordering, so it's recorded here to stop a future
reader from "fixing" the sort back to absolute profit.
