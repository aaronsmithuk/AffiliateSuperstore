# Product structured-data offer decision

Status: `Offer` markup intentionally withheld

Reviewed: 1 September 2026

## Decision

Wonder Aisle publishes truthful `Product` identity markup but does not currently
publish an `Offer`, `AggregateOffer`, `Review` or `AggregateRating` object.

The AliExpress projection exposes a current observed price, but the application
has not yet established whether every value is the selected option's price, the
minimum price across variants, a promotion-dependent price, or a value with
shipping, tax and availability conditions that differ by visitor. Rendering that
value as a single public `Offer` would overstate what Wonder Aisle knows.

AliExpress feedback is also not a Wonder Aisle review corpus, so it must not be
emitted as `AggregateRating` for the site.

## Release gate

`Offer` markup may be reconsidered only after all of the following are true:

1. the source contract identifies the price meaning for each product and option;
2. currency, availability, item condition and destination URL are source-backed;
3. variant-dependent and promotion-dependent prices are represented accurately;
4. stale or unavailable offers are removed within an agreed service level;
5. automated tests cover price changes, missing values and multi-option listings;
6. live output passes Google's product-snippet validation without inventing
   ratings, shipping, returns or stock information.

Until that gate is met, visible pages continue to qualify prices as observed
marketplace data and direct visitors to AliExpress to confirm the selected option
and final total.
