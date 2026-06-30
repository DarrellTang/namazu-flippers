# Add Universalis as a second market data source (enrich-and-degrade)

Saddlebag Exchange remains the opportunity *discovery* engine (cross-server scan).
Universalis is added as a second source that *enriches* the top-N surviving
opportunities with data Saddlebag doesn't expose: home-world **listing depth**
(competition) and **recent sale history** (price corroboration). One batched call
per scan; if Universalis is slow or unavailable the pipeline degrades to Saddlebag-
only, velocity-based behavior.

**Why:** competition depth is the single biggest arbitrage risk and Saddlebag returns
no listing counts; recent-sale history is needed to flag inflated-average flukes. Both
require a different API. Enrich-only (not replace) keeps Saddlebag's discovery while
adding the missing signals at minimal call cost.

**Trade-off:** a second external dependency and its rate limits, accepted because the
graceful-degradation path means an outage only costs signal quality, never a broken
scan. Cross-DC / multi-world enrichment is intentionally out of scope for now (home
world only).
