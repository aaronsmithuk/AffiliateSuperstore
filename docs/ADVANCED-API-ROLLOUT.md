# AliExpress Advanced API rollout

Status: integration foundation and supervised read-only preview complete locally;
scheduled Advanced discovery remains disabled per shop while relevance gates are
tightened.

## Verified account capabilities

A live smoke run on 2 September 2026 verified the following against the GB / GBP /
English application credentials:

| Capability | Result |
|---|---|
| `aliexpress.affiliate.hotproduct.query` | Success; product response parsed |
| `aliexpress.affiliate.hotproduct.download` | Success; product response parsed |
| `aliexpress.affiliate.product.smartmatch` | Success with a fixed application-scoped `device_id` |
| Type-2 hot-product link generation | Success |
| `aliexpress.affiliate.productdetail.get` after paced Advanced calls | Success |
| SKU Dimension API | Still pending; not enabled or assumed |
| Promotion/coupon information | Still returns `InsufficientPermission` |

Two undocumented operational constraints were observed:

- Smart Match rejected a product-and-keyword request without `device_id`, despite
  the public reference presenting it as optional. The application supplies the
  fixed non-personal value configured as `AliExpress:SmartMatchDeviceId`; it does
  not derive this value from a visitor, session or browser.
- A rapid diagnostic sequence received `ApiCallLimit` with a one-second ban.
  `AliExpress:MinimumRequestIntervalMilliseconds` therefore applies a
  process-wide serial request gate. The initial value is 1100 milliseconds.

## Implemented behaviour

Catalogue ingestion accepts three explicit sources:

- `StandardSearch` uses `aliexpress.affiliate.product.query` and type-0 links.
- `HotProductQuery` uses `aliexpress.affiliate.hotproduct.query` and type-2 links.
- `SmartMatch` uses `aliexpress.affiliate.product.smartmatch`, the fixed backend
  device identifier and type-0 links.

Every returned product continues through the existing minimum-data check,
deterministic quality assessment, immutable snapshot and human review state. A
new Advanced candidate is never approved merely because AliExpress calls it hot
or recommends it. Snapshot `SourceEndpoint` plus correlation ID and ingestion-job
checkpoint retain the source, keyword, page and optional seed product.

An active type-2 link is not downgraded when the same product is later found by a
standard search. Link renewal continues to regenerate the current link type.

Authenticated administrators can use `/admin/advanced-discovery` to inspect one
bounded live page without writing products, changing review state, publishing
content or generating links. The same read-only path is available to operators:

```text
--preview-hot "plush toy"
--preview-smart --seed=<approved-product-id> "plush toy"
```

The affiliate performance report now groups impressions, clicks, attributed
orders and commission by each product's earliest recorded `SourceEndpoint`.
This creates a stable standard-search, hot-product and Smart Match cohort view.

## Activation controls

The account-level `AliExpress:AdvancedApiEnabled` flag is enabled because live
access is verified. Actual automated discovery is independently controlled for
each shop:

```json
{
  "HotProductDiscoveryEnabled": false,
  "SmartMatchDiscoveryEnabled": false,
  "SmartMatchSeedProductIds": [],
  "AdvancedDiscoveryPagesPerQuery": 1
}
```

Leave both source flags off until the relevance changes described below are in
place. Then activate in this order:

1. Enable hot-product discovery for one supervised cycle. This adds one bounded
   Advanced page per configured keyword after all standard pages.
2. Review relevance, quality flags, duplicates and type-2 link generation before
   leaving it scheduled.
3. Do not enable unseeded or keyword-only Smart Match for catalogue discovery.
   Introduce product-seeded Smart Match through a separate reviewed
   replacement/related-product workflow using only approved backend seeds.

## Supervised preview findings

Read-only previews on 2 September 2026 produced this evidence:

| Source | Returned | Eligible | Existing | Quality-clear new | Finding |
|---|---:|---:|---:|---:|---|
| Hot-product query, `plush toy`, initial rules | 16 | 16 | 1 | 3 | The three nominally clear results still included an IP-risk puppet, vehicle cushions and a dog toy. |
| Hot-product query after relevance-rule update | 16 | 16 | 1 | 0 | Every unsafe or off-scope result was held for review; the batch had no useful new automatic candidate. |
| Smart Match, keyword only | 20 | 20 | 0 | 17 | Results were overwhelmingly unrelated electronics, household and beauty products. |
| Smart Match, approved plush seed | 2 | 2 | 1 | 1 | Both results were closely related fox plush products. |

Consequences for rollout:

- the write path now requires positive plush evidence and applies the expanded IP,
  pet-product and non-product-scope rules before a candidate is quality clear;
- the scheduler now refuses to plan Smart Match unless at least one approved
  backend product seed is configured;
- keep Hot Product disabled until a query/category strategy produces useful clean
  candidates rather than merely a safe empty cohort; and
- keep Smart Match disabled until approved seed management is connected to the
  reviewed product workflow.

## Pilot measurements

For every source cohort, measure:

- products read, minimally eligible, quality-flagged and approved;
- duplicate rate and manual rejection reasons;
- median recent sales, evaluation rate, base commission and hot commission;
- impressions, outbound clicks, attributed orders and settled commission;
- API success, throttling, latency and daily call volume.

Do not scale page counts until at least four weeks of review-only operation show
that Advanced candidates improve useful catalogue yield or commission without a
higher safety/IP rejection rate. Do not expose SKU, variant, stock or delivery
claims until SKU Dimension access is separately granted and verified.
