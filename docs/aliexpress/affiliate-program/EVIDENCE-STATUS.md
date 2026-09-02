# Affiliate conversion evidence status

Last assessed: 2 September 2026

This is the decision index for volatile commercial and operational evidence.
It does not replace the captured sources or their hashes. Account pages must be
rechecked before forecasting, S2S activation or a production release.

| Topic | Strongest evidence | Current conclusion | Production gate |
|---|---|---|---|
| Service Agreement | Complete 31 March 2022 agreement; Help Centre captured 30 August 2026 says a newer agreement took effect 1 April 2025 | The current complete binding agreement is not captured | Obtain, hash and compare the complete 2025 agreement |
| Programme Rules | Signed-in rules captured 30 August 2026, Part A effective 1 August 2025 | Commission categories, exclusions and overrides are evidenced, subject to the current agreement | Recheck effective date and preserve a fresh redacted capture |
| Account commission model | Signed-in Commission Rules capture from 30 August 2026 | It displayed Non-Transparent Channels: 7% Affiliate Products in other categories and 0% Non-Affiliate Products while verified-site reclassification was pending | Recheck after classification; record heading, rates and capture time before forecasts |
| Commission overrides | Programme Rules plus account Commission Rules | Store-specific and monthly Specific Product List rates can override the category rate | Review monthly and preserve the applicable order-returned rate |
| Order commission | Signed order API fields stored per sub-order | Paid/S2S values are estimates; Completed Settlement is authoritative; Invalid earns zero | Validate one legitimate event through terminal state |
| API quota | No affiliate-specific daily quota in captured official documentation or account console; live account returned `ApiCallLimit` during a rapid mixed-method sequence | The precise daily quota is unknown. Calls are process-wide serial and paced at 1,100 ms as a conservative observed limit, not a contractual allowance | Obtain written app-specific QPS/daily limits; keep pacing and monitor limit responses |
| Cache permission | Official guidance describes product pricing as real time but supplies no authoritative publisher cache TTL in the captured material | Local snapshots are operational evidence, not permission for indefinite caching | Obtain written TTL/refresh/deletion rules for product, image, price and commission data |
| Public API lifecycle | Official public Open Platform pages still expose affiliate product/order methods but their navigation labels the Affiliate API documentation deprecated | Existing methods worked in the live account; no public successor or shutdown date was established | Obtain written successor/lifetime guidance before treating the integration as durable |
| S2S authenticity | Official S2S guidance documents field mapping but no callback signature | HTTPS plus a 32–512 character fixed secret is the compensating control; push remains an estimate | Confirm Portals mapping, protect logs and complete a legitimate live event |
| Order retention | Captured Help Centre/API guidance | Live Order Tracking query retention is 180 days | Successful 180-day scan at least monthly plus durable local archive |
| Cookie duration / tie-break | Captured official material states cookie tracking but no duration or first/last-click rule | Unknown and must not be represented as a defined attribution window | Written AliExpress answer required for any attribution-window claim |

Public lifecycle spot-check on 2 September 2026: the official Open Platform
still rendered the
[`aliexpress.affiliate.order.list`](https://open.alitrip.com/docs/doc.htm?articleId=45802&docType=2&treeId=674)
and
[`aliexpress.affiliate.product.query`](https://open.alitrip.com/docs/api.htm?apiId=45803)
reference pages. The surrounding navigation labelled the Affiliate API
documentation deprecated. Those pages establish neither an app-specific quota
nor a successor/shutdown date, so their continued availability is not treated
as lifecycle assurance.

## Evidence capture procedure

For every volatile account or provider fact:

1. Capture from the signed-in official page or written AliExpress support reply.
2. Record UTC capture time, account/application identifier, page title, source
   URL, effective date and operator.
3. Redact AppKey, App Secret, email, phone, payee details, balances, callback
   secret, cookies and session identifiers.
4. Store the source under the ignored `sources/` or `archive/` directory.
5. Calculate SHA-256 and add it to `PROVENANCE.md`; do not commit the sensitive
   original.
6. Update this table with the conclusion and next review date. A screenshot of
   a heading alone is insufficient when tables, notes or effective dates affect
   the conclusion.

## Provider questions requiring written answers

Submit these without including credentials:

1. Which complete Affiliate Programme Service Agreement applies to this UK
   publisher account, and where can the version effective 1 April 2025 be
   downloaded?
2. What are application 6102's per-second, per-minute and daily limits for each
   enabled Affiliate API permission group, and how are 429/`ApiCallLimit`
   windows reset?
3. What cache duration, refresh and deletion rules apply separately to product
   facts, prices, commission rates, images and generated promotion links?
4. The public documentation labels the Affiliate API set deprecated. What is
   the supported successor and migration or shutdown timetable for the methods
   currently enabled on this application?
5. Which commission model is now assigned after verified-site classification,
   and which non-affiliate, store-specific and Specific Product List overrides
   currently apply?
6. Is a callback signature or source-verification mechanism available for S2S,
   beyond the publisher-defined fixed parameter?
7. What are the cookie duration and attribution tie-break rules, including a
   later click from another publisher and app hand-off?
