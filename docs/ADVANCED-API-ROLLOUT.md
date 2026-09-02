# AliExpress Advanced API rollout

Status: integration foundation complete locally; scheduled Advanced discovery
remains disabled per shop pending the first supervised ingestion run.

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

## Activation controls

The account-level `AliExpress:AdvancedApiEnabled` flag is enabled because live
access is verified. Actual automated discovery is independently controlled for
each shop:

```json
{
  "HotProductDiscoveryEnabled": false,
  "SmartMatchDiscoveryEnabled": false,
  "AdvancedDiscoveryPagesPerQuery": 1
}
```

Leave both source flags off in the first release. Then activate in this order:

1. Enable hot-product discovery for one supervised cycle. This adds one bounded
   Advanced page per configured keyword after all standard pages.
2. Review relevance, quality flags, duplicates and type-2 link generation before
   leaving it scheduled.
3. Enable keyword Smart Match only after hot-product queue volume is understood.
   Product-seeded Smart Match should be introduced through a separate reviewed
   replacement/related-product workflow rather than visitor requests.

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
