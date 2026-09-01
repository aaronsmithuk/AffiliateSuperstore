# AI-assisted catalogue and content automation plan

Status: implementation in progress; production automation remains disabled

Prepared: 30 August 2026

Scope: catalogue freshness, product identity, editorial repair and reviewed content creation

Implementation status (1 September 2026): the MVP observation/lifecycle foundation
is complete locally. Product observations now carry raw/content hashes and source
correlation, unchanged content creates no duplicate snapshot/change event,
direct-detail misses require repeated evidence spanning at least 24 hours before
withdrawal, positive evidence restores availability, and discovery-query absence
does not count as lifecycle evidence. The admin catalogue exposes lifecycle
totals, filters and evidence counts. The durable automation slice is also complete
locally: independent work types use unique idempotency keys, recoverable SQL
leases/checkpoints, bounded exponential retry/dead-letter handling and a bounded
parameter-free `/health/wake` signal. The deterministic identity core is also
complete locally: versioned NFKC/GTIN/model/pack/size/colour/material profiles,
bounded category blocks, hard-conflict classification, current/superseded
evidence, non-destructive canonical membership and a filtered 25-row admin
review queue now run as a fourth durable work type. A live 230-offer SQL run
reduced an over-broad first pass from 1,753 variant suggestions to four after
title-evidence and multi-size safeguards; all older matcher evidence remained
auditable but was superseded. Production automation remains disabled. Exact
main-image byte hashing is now implemented as bounded, CDN-allow-listed,
versioned review evidence. The AI-3 calibration workflow now stores immutable
reviewer labels and rationale in protected tuning, threshold-selection and
final-test slices, preserves disagreements for adjudication, and reports queue
precision, relationship accuracy, false merges and Wilson lower confidence
bounds at fixed thresholds. Populating the 500-pair set remains human review
work before any automatic canonical linking is considered. The AI-4 editorial
quality core is now complete locally: edits are immutable named revisions,
unsupported authenticity/delivery/price/safety/rating/superlative, numerical
and material claims are blocked mechanically, warnings prevent approval, and
the admin exposes field changes, evidence and restore-as-new-revision history.
The current projection remains backward-compatible for public reads.
AI-5 groundwork is now implemented locally through the first provider adapter.
The provider-neutral structured product-copy contract builds stable source
packets, validates every returned draft and feeds an authenticated admin preview.
An additive `AiInvocations` ledger reserves spend in a serializable transaction,
records provider/model/prompt/hash/token/cost/latency/validation outcomes, blocks
before the $1 monthly application cap can be exceeded and reuses successful
unchanged responses at zero cost. The OpenAI Responses adapter requests strict
JSON Schema output with `store: false`; global configuration remains disabled,
development enables only the feature switches, and no API key is stored in the
repository. The owner has added a local User Secrets key and funded the API
account. A first review-only Luna request against a real Highland Cow product
completed locally on 1 September 2026 using 411 input and 281 output tokens at
an estimated USD 0.0004194; it made no catalogue changes. That result exposed
promotional merchant wording and source-title narration, so prompt
`product-editorial-v2` and validator `1.1` now block those patterns and preserve
the exact result as an offline regression case. The protected admin workflow can
now run a sequential sample capped at ten eligible products, showing per-item
validation, cache and estimated-cost evidence. The first ten-product live sample
completed locally on 1 September 2026: eight drafts reached review, two were
blocked, none failed, and none were cache hits. It used 4,692 input and 3,763
output tokens at an estimated total cost of USD 0.005454. No catalogue copy was
saved, approved or published. The sample identified two follow-up eval cases:
equivalent compound dimensions such as `50x50cm` and `50 × 50 cm` must normalise
without a false unsupported-number block, and indirect phrases such as "listed
in" or "the supplied information" should be removed from consumer-facing copy.
The first approval-queue action is now implemented locally as a separate,
administrator-triggered batch capped at ten items. It considers only active,
available, collection-assigned products with an active affiliate link, no
existing editorial copy, a pending/review status and a clear deterministic
quality assessment. Strictly validated suggestions are saved as immutable
editorial drafts with the provider, model, prompt version and invocation in the
audit note; warnings, blocked output and provider failures are left unsaved.
The action never changes approval status or publishes content, rejects a
concurrent run and reports item-level outcomes, token usage, cache use and
estimated cost. Deployment and the first production draft run remain gated on
owner confirmation and production API-key configuration.

## Executive decision

Build this as an extension of the existing .NET/SQL catalogue pipeline, not as a
separate AI platform. The first release should be useful with **no model API at
all**: direct AliExpress refreshes, SQL rules, hashes, normalized attributes,
link regeneration, lifecycle state and an explainable review queue do most of
the work. Add local or hosted embeddings only to generate candidates that rules
cannot resolve. Use an LLM or vision model only for the small ambiguous tail and
for editorial suggestions; never let a model invent price, stock, pack size,
brand, delivery, safety or licensing claims.

The recommended deployment shape is:

1. Keep one ASP.NET Core application and SQL Server database.
2. Add durable, leased work items and idempotency keys in SQL.
3. Let a SmarterASP scheduled URL request wake the site every 15 minutes; the
   request itself does not mutate catalogue data. The existing worker claims due
   SQL work in small, restart-safe batches.
4. Refresh changed/high-value offers daily, the long tail every 3–7 days, and
   re-check currently unavailable offers on a short backoff before hiding them.
5. Preserve every merchant/network offer separately. Link offers to a canonical
   product or variant; do not destructively merge price, SKU or source records.
6. Keep all generative product edits and all blog drafts review-only for the
   initial phases. Fully automate only deterministic cleanup, safety demotion,
   freshness state and very-high-confidence identity links.

Expected external AI spend is deliberately tiny: approximately **$0.48/month at
1,000 offers, $0.85 at 10,000, and $3.49 at 100,000** under the documented
assumptions, including a vision reserve and 25% retry contingency. These are
planning estimates, not vendor quotes; human review time and hosting are not
included. Hard monthly caps should initially be $1, $2 and $5 respectively.

## Scope and non-goals

This plan covers suggestions and workflow changes only. It does not authorize:

- deployment, indexing, publication or modification of live catalogue data;
- a paid model, vector database, queue or SEO subscription;
- scraping AliExpress product pages or Google result pages;
- automatic publication of generated product copy or articles; or
- applying for additional AliExpress permissions without an owner decision.

## Repository-aware current state

### What already exists and should be reused

| Finding | Evidence | Consequence |
|---|---|---|
| ASP.NET Core 10, Razor Pages public site, Blazor Server admin and SQL Server/EF Core are the chosen stack. | [`README.md`](../README.md), [`docs/PROJECT-PLAN.md`](PROJECT-PLAN.md#L31) | Add application services, EF entities/migrations and admin components; do not introduce a second runtime for the MVP. |
| Products are keyed by AliExpress product ID; shop-specific status already supports editorial title/description, approval state and automated flags. | [`CatalogRecords.cs`](../src/AffiliateSuperstore.Persistence/Entities/CatalogRecords.cs#L28) | The present `ProductRecord` is really a source offer. Introduce a canonical-product layer without changing source IDs or overwriting provenance. |
| Immutable price/commission snapshots exist and are uniquely indexed by product/time. | [`CatalogRecords.cs`](../src/AffiliateSuperstore.Persistence/Entities/CatalogRecords.cs#L83), [`AffiliateSuperstoreDbContext.cs`](../src/AffiliateSuperstore.Persistence/AffiliateSuperstoreDbContext.cs#L94) | Extend observations with hashes and lifecycle evidence rather than adding another history store. |
| Ingestion searches AliExpress, rejects minimally invalid rows, creates a snapshot, updates the current record, generates a link and runs deterministic quality rules. | [`CatalogueIngestionService.cs`](../src/AffiliateSuperstore.Application/Catalogue/CatalogueIngestionService.cs#L69) | Split fetch/normalize/evaluate/persist into restart-safe item stages, retaining the existing source adapter. |
| Every ingested snapshot currently sets `IsAvailable = true`; disappearance from a discovery query is not processed. | [`CatalogueIngestionService.cs`](../src/AffiliateSuperstore.Application/Catalogue/CatalogueIngestionService.cs#L258) | Availability and expiry need explicit evidence and grace periods. A search miss must never be treated as proof of unavailability. |
| The worker polls persisted job history, runs configured discovery requests sequentially, stops after a failed request, and then renews links. | [`CatalogueAutomationWorker.cs`](../src/AffiliateSuperstore.Web/Services/CatalogueAutomationWorker.cs#L37) | Preserve due-state semantics, but add leases, per-item checkpoints, backoff and independent job types so one query cannot block all maintenance. |
| Development is 24-hour refresh / 15-minute poll / 60-minute failure retry / two-hour stale recovery / 120-hour link revalidation; production automation is disabled. | [`appsettings.json`](../src/AffiliateSuperstore.Web/appsettings.json#L49), [`appsettings.Development.json`](../src/AffiliateSuperstore.Web/appsettings.Development.json#L6) | These are safe starting defaults, not a complete freshness policy. Production remains off until authentication, wake scheduling and migration/rollback are proven. |
| Link renewal batches at 50, asks the official API to regenerate links, records missing responses and expires replaced links. | [`AffiliateLinkRenewalService.cs`](../src/AffiliateSuperstore.Application/Catalogue/AffiliateLinkRenewalService.cs#L21) | Use this as the broken-link authority. Do not crawl or probe AliExpress pages as a substitute for the affiliate API. |
| Rules already identify off-scope, safety, IP, ambiguous quantity and excessive-title issues; flagged approved items are demoted but clean items are never auto-approved. | [`ProductQualityAssessmentService.cs`](../src/AffiliateSuperstore.Application/Catalogue/ProductQualityAssessmentService.cs#L23) | This is the right safety pattern. Add content-quality rules and model suggestions behind the same one-way demotion/human approval gate. |
| Admin has a review queue with Approve/Needs review/Reject actions, but only loads 50 active plushies records and cannot edit/version copy or inspect match evidence. | [`Catalogue.razor`](../src/AffiliateSuperstore.Web/Components/Pages/Catalogue.razor#L65) | Extend this screen into work-item queues with filters, diffs, evidence, bulk-safe actions and optimistic concurrency. |
| Public catalogue and product pages require active, eligible, approved products with active affiliate links. | [`Shop.cshtml.cs`](../src/AffiliateSuperstore.Web/Pages/Shop.cshtml.cs#L50), [`Product.cshtml.cs`](../src/AffiliateSuperstore.Web/Pages/Product.cshtml.cs#L28) | Lifecycle changes can safely hide an offer by changing eligibility/activity or link state; no generated copy bypasses approval. |
| Admin routes and exports now require the owner-only Identity Administrator role; migrations still run automatically only in development. | [`README.md`](../README.md), [`Program.cs`](../src/AffiliateSuperstore.Web/Program.cs) | Keep mutation/review endpoints behind the policy. Production schema changes require a separately applied migration and rollback plan. |
| CI restores, builds and tests on .NET 10. | [`.github/workflows/build.yml`](../.github/workflows/build.yml) | Add deterministic unit/integration tests and offline eval fixtures to the same build; never call paid or live APIs in CI. |

### Hosting and affiliate constraints

- The selected target is SmarterASP-compatible ASP.NET Core plus SQL Server, but
  final .NET 10 and SQL Server feature support still needs verification
  ([project plan](PROJECT-PLAN.md#L39)). Do not require SQL Server 2025 vector
  types in the MVP.
- SmarterASP documents URL-based scheduled tasks on Premium+ plans, with a
  minimum 15-minute interval and a default quota of three. The task issues an
  HTTP GET. Use it to wake the application, not to expose a GET endpoint that
  directly runs a refresh. [SmarterASP scheduled-task documentation](https://www.smarterasp.net/support/kb/a2018/set-schedule-tasks-on-your-own-purpose_.aspx)
- The current app has Standard Publisher and System Tool permissions; Advanced
  and SKU Dimension permissions are inactive. Therefore delivery, SKU/variant,
  smart-match and some hot-product fields cannot be assumed
  ([local evidence](aliexpress/affiliate-program/aliexpress-affiliate-rules-research.md#L23)).
- AliExpress documents product-query pages of at most 50 and the repository
  research found no published quota for this app. Concurrency must be low and
  configurable, with exponential backoff and a global circuit breaker
  ([local evidence](aliexpress/affiliate-program/aliexpress-affiliate-rules-research.md#L349)).
- Scheduled API pulls appear permitted by the documented pull model, but no
  explicit scheduling, cache-duration or product-retention promise was found.
  The 2025 full agreement is still an evidence gap. Resolve it before production
  scale-up ([local evidence](aliexpress/affiliate-program/aliexpress-affiliate-rules-research.md#L418)).
- Tracking short keys may be invalidated after one year or after six months
  without clicks. The existing five-day renewal cadence is appropriately
  conservative; regeneration, not browser probing, should remain authoritative
  ([local evidence](aliexpress/affiliate-program/aliexpress-affiliate-rules-research.md#L425)).

## Automatic versus review-only policy

| Action | Initial policy | Promotion requirement |
|---|---|---|
| Normalize Unicode, whitespace, casing for comparison; compute hashes; parse units/pack counts | Fully automatic | Deterministic tests and reversible derived fields. |
| Create immutable source observation; record price/stock/link change | Fully automatic | Idempotency key and source timestamp present. |
| Hide an offer | Automatic only after direct API unavailable/missing evidence on two attempts at least 24 hours apart, or an explicit ineligible response; immediate safety demotion remains allowed | Expiry gold-set precision >=99%, false-hide rate <0.5%, rollback tested. |
| Restore a hidden offer after a valid direct response | Automatic, but never restore editorial approval or clear safety flags | Direct current observation and valid link. |
| Link offers to one canonical product | Automatic only for deterministic confidence >=0.985 with no hard conflicts; links remain reversible and offers remain separate | Duplicate precision lower 95% confidence bound >=99.5%. |
| Mark bundle/variant/translation/suspected duplicate | Automatic classification plus review queue; no destructive merge | Reviewer can inspect evidence and undo. |
| Suggest replacement products | Review-only | Top-3 suitable-replacement recall >=80%, zero prohibited/off-scope suggestions in gold set, then consider auto-select only for unpublished slots. |
| Fix encoding, duplicate whitespace/punctuation or repeated adjacent tokens | Automatic on unpublished derived text; retain source and diff | 100% fixture pass, no entity/number changes. |
| Rewrite/translate title or description, infer missing attributes | Suggestion only | Factual validation passes and human acceptance >=90% over 500 suggestions before considering auto-apply to *pending, unpublished* records. |
| Change published editorial content | Review-only | Always, because meaning, compliance and search impact can change. |
| Generate blog topics, briefs, drafts and links | Review-only | Always; no automatic publishing. |
| Update price, availability, SKU, pack size or delivery from AI output | Forbidden | Only source API data can set these facts. |

## Proposed pipeline

```text
SmarterASP wake GET -> due-work planner -> SQL work items / leases
                                      |
AliExpress adapter -> raw observation -> hash/change gate -> normalize/validate
                                                      |-> lifecycle + link rules
                                                      |-> deterministic identity
                                                      |-> candidate queue
                                                              |-> embedding shortlist
                                                              |-> LLM/vision ambiguity
                                                      |-> editorial suggestions
All decisions/evidence -> review queue -> approved version -> current public projection
```

Every stage consumes a durable item, writes an immutable result and can be
replayed. The public site continues to read a simple current projection, so AI
latency or outage never enters a visitor request.

### Stage 1: discovery and targeted refresh

1. Keep configured discovery queries for recall, but use them to find offers,
   not to decide availability.
2. Create targeted refresh work for known product IDs. Use
   `productdetail.get` in documented batches and low concurrency where permitted.
3. Prioritize by public/featured status, click activity, recent price volatility,
   time since verification and previous failure count.
4. Store response received time, source endpoint, request parameters excluding
   secrets, response hash and raw payload/selected fields. If terms later limit
   raw retention, a configuration-controlled purge can remove payloads while
   keeping field provenance and hashes.
5. Compute a canonical content hash over normalized factual fields. If it is
   unchanged, update `LastCheckedUtc` only and skip snapshots, embeddings and AI.

Recommended cadence:

| Cohort | Refresh | Notes |
|---|---:|---|
| Approved/featured or clicked in 30 days | Daily | Re-check before article/product publication and before replacement selection. |
| New/pending | Daily for first 7 days | Stabilizes identity and catches short-lived listings. |
| Normal active long tail | Every 3 days, moving to 7 after 30 unchanged days | Jitter work across days. |
| Price-volatile (>10% twice in 14 days) | Every 12 hours, only if quota permits | No claim that prices are guaranteed between checks. |
| First unavailable/missing direct response | Retry after 6 hours, then 24 hours | Keep public record until confirmation unless API explicitly declares ineligible. |
| Confirmed unavailable | Every 7 days for 30 days, then monthly | Restore automatically only from source evidence. |
| Affiliate links | Existing 120-hour validation; force before 150 days without click and well before one year | API regeneration only. |

### Stage 2: feed health, expiry and changes

Track health per endpoint/query/merchant and per offer:

- expected versus returned row count, response/error class, duration and retry;
- time since last successful discovery and direct refresh;
- percentage changed, newly seen, unavailable and missing;
- percentage lacking image/GBP price/link/required identity fields;
- price deltas (absolute and percentage), currency change, seller change, title
  drift, image drift and attribute drift;
- consecutive missing/unavailable checks and evidence source;
- link validation age, regeneration outcome and `/go` internal failures.

Alert on trends, not one-off noise: zero-result query after a normal baseline,
>30% row-count drop, >10% direct-refresh failures, no successful refresh inside
2x the intended cadence, >5% missing links, or budget/circuit-breaker state.
Never mark a product expired solely because it fell out of a keyword page.

### Stage 3: suggested replacements

Generate candidates only after confirmed unavailability or an editor request.
Filter first by active/eligible/approved/link-valid, same shop and product class,
then reject hard conflicts (pack, intended audience/safety class, licensed IP,
material/size range where material). Rank with:

- canonical/variant relationship and category match (30%);
- normalized attributes and intended use (25%);
- price within +/-25% of the last verified offer (15%);
- image/text similarity (15%);
- rating/sales evidence and freshness (10%); and
- commission (maximum 5%, never allowed to override suitability).

Show the top three with reasons, conflicts, last-checked time and price delta.
Replacing a public item is review-only in MVP. Never redirect an old product URL
straight to a different product; retain an unavailable page or explicit
editorial replacement link to avoid misleading users and search engines.

## Product identity and duplicate detection

### Model the distinction correctly

An offer is not a product. Different merchants can sell the same physical item;
one merchant can sell several colours/sizes; a pack of two is not the same offer
as a single item. The safe operations are `link to canonical product`, `link to
variant family`, `mark bundle`, `mark translation`, or `not related`. Source
offers and their snapshots are never deleted by a match decision.

### Deterministic normalization and blocking

For every source offer derive, with versioned code:

- Unicode NFKC/case-folded title, HTML/entity cleanup and language guess;
- normalized brand, model/MPN, GTIN/EAN/UPC, seller ID and seller SKU when
  available;
- units converted to canonical dimensions/weight, colour/material vocabulary,
  pack count and audience/use;
- canonical source URL and source product ID;
- original-image URL, downloaded-byte SHA-256 only where image terms permit,
  and perceptual hash from a cached thumbnail; and
- token shingles with stopwords/marketing phrases removed.

Generate candidates inside blocks instead of comparing all pairs: exact
identity keys; category + brand/model; category + rare title token + size;
image hash/pHash bucket; or embedding nearest neighbours. Cap each offer to 50
candidates and persist why the pair was generated.

### Match ladder and confidence

1. **Exact identity:** same network/product ID means the same source offer;
   exact valid GTIN, or exact normalized brand+MPN, is canonical-product evidence.
2. **Exact media/attributes:** identical image bytes plus matching class, pack
   and material dimensions is strong cross-merchant evidence. A CDN URL alone
   is not an identity key.
3. **Text/attribute rules:** weighted token/shingle similarity plus exact
   normalized size, colour, material and pack count.
4. **Local or hosted text embeddings:** shortlist semantically similar titles
   across translations/word order. Embeddings are candidate generators, not
   merge authority.
5. **Vision:** only for near-image candidates whose text is insufficient;
   compare product shape/details, while treating background, watermark and
   cropping as irrelevant.
6. **LLM adjudication:** provide only the two normalized source records and
   extracted evidence; require structured output containing relationship,
   confidence, supporting fields, conflicts and `insufficient_evidence`.

Hard conflicts override positive similarity: differing pack count is
`bundle`; meaningful size/colour/model differences are `variant`; incompatible
category/use is `not_match`. Prices are weak evidence because merchants differ.
SKU equality across different merchants is not trusted unless brand+MPN or GTIN
also agrees.

Suggested score bands after calibration:

| Confidence/evidence | Action |
|---|---|
| >=0.985 and exact trusted identifier or exact image + all material attributes, no conflict | Automatically add reversible canonical membership. |
| 0.90–0.984 | Reviewer sees suggested duplicate/translation/variant with full evidence. |
| 0.75–0.899 | Embedding/vision/LLM escalation if budget remains, otherwise review or defer. |
| <0.75 or any unresolved hard conflict | No duplicate action; optional related-product signal only. |

Store both the raw component scores and a plain-language explanation such as
“exact source-image digest; same 40 cm size and pack of one; different seller;
price ignored.” A later threshold/model change can be replayed without losing
the original decision.

The offline proof in
[`tools/AffiliateSuperstore.CatalogueQualityPoc`](../tools/AffiliateSuperstore.CatalogueQualityPoc)
exercises exact cross-merchant, translated, bundle, size-variant and unrelated
examples without external packages or network access.

## Content-quality repair

### Rules before generation

Add deterministic flags for encoding artifacts, repeated tokens/phrases,
uppercase ratio, punctuation runs, title length, keyword density, unit/pack
ambiguity, contradictory quantities, unsupported superlatives, seller contact
text, marketplace boilerplate and source/title language mismatch. Parse facts
before rewriting. The cheapest good edit is often deleting seller SEO phrases,
not generating a new description.

### Provenance-safe editorial workflow

1. Preserve `SourceTitle`, `SourceDescription` and every source field/version.
2. Build a factual packet from source-backed fields, each with
   `{field, value, sourceObservationId}`.
3. Ask a model, if needed, for strict structured output:
   `suggestedTitle`, `suggestedDescription`, `claims[]`, `removedNoise[]`,
   `uncertainties[]` and `language`.
4. Deterministically compare every named entity, number, unit, colour, material,
   age/audience and pack claim against the packet. Reject unsupported claims,
   changed numbers and omitted material qualifiers.
5. Run prohibited/off-scope/IP rules, duplicate-copy checks and length/readability
   checks. Store prompt template version, model snapshot/alias, input hash,
   response hash, token use, estimated cost and validator version.
6. Present a field diff and source evidence to the editor. Every saved edit
   creates an immutable content version; approval revalidates the current copy,
   and rollback creates another reviewed version rather than mutating history.

Titles should lead with what the item is, then material attributes useful to a
shopper, without repeating synonyms. Descriptions should be concise and admit
unknowns. Never infer authenticity, safety certification, age suitability,
delivery time, warranty or “best”/“official” status. Price and stock stay live
source projections, never prose facts.

SEO is an outcome of clarity, not a separate keyword-stuffing pass. Generate
meta copy only from the approved version, prevent near-identical metadata across
canonical products, keep variant/offer pages canonicalized appropriately, and
retain `noindex` until the existing quality gate and content threshold are met.

## Responsible blog/content generation

The first content goal is a small number of useful editorial pages that answer
real catalogue questions, not a page for every keyword/product/variant.

### Topic discovery

Use first-party signals in this order:

1. internal shop searches with result count and reformulation (aggregated and
   privacy-minimized);
2. catalogue gaps, attribute clusters, price/change patterns and approved
   product depth;
3. outbound-click and conversion aggregates once reconciliation exists;
4. manually imported Search Console query/page data or its API after explicit
   authorization; and
5. editorial/customer-support questions.

Do not scrape Google results or use model memory as “search demand.” Score topic
candidates for demand evidence, product coverage, distinct intent, freshness
need, editorial value and overlap with existing pages.

### Brief-to-publication gate

1. **Cannibalisation preflight:** normalized keyword/intent overlap, title
   shingles and embedding similarity against all live/draft pages. Prefer
   updating an existing page when similarity is high.
2. **Brief:** user question, unique value, audience, evidence packet, products
   verified within 24 hours, required disclosure, outline, facts/sources,
   internal-link targets and explicit claims not to make.
3. **Draft:** model may organize and phrase supplied evidence; it may not browse
   silently or add uncited facts. Product modules render current SQL facts rather
   than baking price/stock into prose.
4. **Validation:** sentence/claim-to-source mapping, duplicate paragraph scan,
   internal-link status, disclosure placement, title/meta checks, product
   freshness and prohibited-claim rules.
5. **Human approval:** editor verifies helpfulness, originality, facts,
   disclosure and tone. Publication stays a separate explicit action.
6. **Freshness:** revalidate product modules daily; review prose at 30/90/180-day
   intervals based on volatility. Withdraw or update when the premise disappears.

Google says generative AI can help research and structure content but generating
many pages without user value can violate scaled-content policy; it emphasizes
accuracy, quality, relevance and appropriate process context
([Google Search guidance](https://developers.google.com/search/docs/fundamentals/using-gen-ai-content)).
The release policy should therefore cap new AI-assisted articles at four per
month initially and require a documented piece of original value (comparison,
tested taxonomy, curated data analysis, or expert/editorial judgment).

Affiliate relationships must be obvious. Put a plain-language affiliate
disclosure before the first affiliate link/product module and retain the
existing site-wide hand-off messaging. ASA/CAP guidance treats content that can
earn click/sale revenue as affiliate advertising and provides disclosure
guidance; legal wording should be reviewed before launch
([ASA/CAP guidance](https://www.asa.org.uk/resource/influencers-guide.html)).
Where substantial automation would reasonably matter to a reader, add a short
“how this was produced and checked” note. This is operational guidance, not
legal advice.

## Data model

Prefer additive migrations and a compatibility projection. In phase 1,
`ProductRecord` can remain the source offer while new canonical tables are
introduced; rename only after data is safely backfilled.

| Entity | Key fields and purpose |
|---|---|
| `SourceOffer` (eventual rename/projection of `ProductRecord`) | Network, source product ID, merchant/seller, seller SKU, locale, source URLs, first/last seen, last checked, availability state/reason, consecutive miss count, current observation/content hashes. Unique `(Network, SourceProductId, Locale)`. |
| `OfferObservation` (extend snapshot concept) | Offer, fetched time, endpoint, request/correlation ID, normalized facts, price/availability, raw hash, optional compressed raw payload, parser version. Unique idempotency key `(OfferId, SourceObservedAtOrHash)`. |
| `CanonicalProduct` | Internal ID, class, brand/model/GTIN where verified, canonical factual attributes, status and current approved content version. Contains no merchant price. |
| `ProductVariant` | Canonical product, normalized variant axes (size/colour/material/model), variant fingerprint. |
| `OfferMembership` | Offer -> canonical/variant, relationship, confidence, state, algorithm/version, created/reviewed by, timestamps. Never deletes the offer. |
| `MatchCandidate` / `MatchEvidence` | Pair key, block reason, component scores, hard conflicts, explanation JSON, decision, decision version and reviewer. Unique unordered pair + algorithm version. |
| `DerivedIdentity` | Offer, normalizer version, normalized identifiers/attributes, title shingles, image digest/pHash, embedding reference/hash. Rebuildable. |
| `ContentVersion` | Owner type/ID, source facts hash, title/description/meta, language, status, author type, prompt/model/validator versions, created/approved/reverted times. Immutable. |
| `ContentClaimEvidence` | Content version, claim pointer, source observation/field, validation result. |
| `ReviewWorkItem` | Type, subject, priority, state, evidence JSON, lease/concurrency token, reviewer, due/resolution. Supports duplicate, content, expiry, replacement and blog queues. |
| `AutomationRun` / `AutomationItem` | Job type, deterministic idempotency key, lease owner/expiry, attempts, next attempt, checkpoint, result counts, error class. Existing `IngestionJobRecord` can be evolved rather than replaced. |
| `AiInvocation` | Purpose, provider/model snapshot, prompt version, input/output hashes, tokens, cost estimate, latency, result status and linked review item. Exclude secrets and unnecessary raw personal data. |
| `Article` / `ArticleVersion` / `ArticleEvidence` | Topic intent, brief/draft/review state, content hash/embedding, citations, internal links, freshness policy, affiliate and automation disclosure checks. |
| `DemandAggregate` | Day/week, shop, normalized query/intent, searches, zero-result count, click/conversion aggregates; no raw user identifier. |

If the host supplies SQL Server 2025, native `VECTOR` storage is available, but
that must be verified before use. The MVP should store a compressed `float[]` in
`varbinary` (or keep embeddings in the worker process and persist only model/hash)
and compute similarity inside small deterministic blocks. This avoids coupling
the release to a database feature the selected host may not provide
([Microsoft SQL Server vector documentation](https://learn.microsoft.com/en-us/sql/t-sql/data-types/vector-data-type?view=sql-server-ver17)).

## Architecture and operations

### Scheduling and work execution

**Recommended MVP:** retain `BackgroundService`, add an innocuous `/health/wake`
GET, and configure one SmarterASP scheduled task every 15 minutes. The GET
returns health/no-op data; starting the app causes the worker to plan due items
from SQL. Any request may wake the app, but only authenticated/configured
server-side schedules can create due work. Do not put a reusable mutation secret
in a query string.

At each tick, acquire a SQL application lock or insert/claim a unique planner
lease. Claim work with an atomic state transition and lease expiry; process at
most N items or T minutes; checkpoint every source batch; release or let the
lease expire on shutdown. Default AliExpress concurrency is one. Add jitter and
exponential backoff for 429/5xx/network failures, with non-retryable validation
errors routed to review.

Alternatives:

| Option | Decision |
|---|---|
| Current in-process timer only | Keep as executor, not sole clock; shared IIS can recycle/sleep. |
| SmarterASP URL schedule + SQL leases | **MVP choice:** compatible, cheap, minimum 15 minutes, no new service. |
| GitHub Actions cron | Do not use for production catalogue mutation: credentials/network coupling, timing variability and awkward rollback/audit ownership. Keep CI offline. |
| Hangfire/Quartz | Reconsider only if scheduling/admin complexity grows. They do not remove the need for an awake host and add persistence/operations surface. |
| Azure Functions/Container Apps job or another managed scheduler | Later choice for >100k offers, sub-15-minute SLAs or isolation; introduces paid infrastructure and secret/network setup. |

### Idempotency, retries and rollback

- Deterministic work key: `jobType:shop:subject:sourceVersion:algorithmVersion`.
- One active lease per key; retry updates the same item rather than inserting
  duplicate snapshots/suggestions.
- Classify errors: transient source/provider, quota/budget, invalid input,
  policy block, validator failure, human rejection and software defect.
- Backoff with jitter, maximum attempts and dead-letter review. A failed AI call
  must never fail source ingestion.
- All source facts and content/match decisions are versioned. Rollback is a
  pointer/state change, not deletion; record actor, reason and previous value.
- Use EF row versions already present for concurrent review. Bulk actions require
  an explicit preview and per-row outcome.

### Observability and controls

Dashboard/alerts should include queue age/depth by type, run success/duration,
API calls/errors, offers checked/changed/hidden/restored, snapshot dedupe rate,
match-band distribution, reviewer acceptance/reversal, content validator
failures, model tokens/cost, and spend versus cap. Use structured logs with job,
item, shop and provider correlation IDs; never log App Secret, prompts containing
unnecessary raw data, signed requests or affiliate tokens.

Controls:

- global automation on/off (existing), plus independent source-refresh,
  dedupe-AI, content-AI, vision and blog switches;
- daily/monthly dollar and token limits by purpose, maximum calls/run and maximum
  ambiguity percentage;
- source API circuit breaker and provider circuit breaker;
- kill switch that stops new AI work while deterministic freshness continues;
- allowlisted model/provider and pinned prompt/normalizer/validator versions;
- 5% shadow/sample mode before any new rule changes state; and
- retention controls for raw source payloads, model inputs and audit data.

### Security and privacy

The owner-only admin is authenticated and authorized; preserve that policy for
every future review, mutation, export and AI-control route.
Use host environment configuration/User Secrets for API keys, separate service
credentials by provider, least-privilege database access, outbound-domain
allowlists, SSRF-safe image fetching (HTTPS, host allowlist, size/type/time caps),
malware-safe decoding and no user-supplied URLs. Treat merchant text as untrusted
data, not model instructions. Strip HTML/scripts and delimit source fields in
prompts. Do not send click/session identifiers, IPs, cookies or personal data to
models. Aggregate internal search demand before storage. Document provider data
handling and regional-processing choices before enabling hosted AI.

## Very-low-cost model strategy

### Rules and local components first

- SQL indexes/queries, normalized identity keys, hashes and change detection are
  effectively free and explainable.
- A local text embedding model is operationally sensible for English candidate
  generation if the shared host can run it within memory/CPU limits. A candidate
  such as `sentence-transformers/all-MiniLM-L6-v2` is Apache-2.0 and intended for
  sentence similarity, but its English focus and 256-word-piece truncation mean
  it must be evaluated on merchant titles/translations rather than assumed fit
  ([model card](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)).
- Run local ONNX embedding generation offline/in the bounded worker, not on web
  requests. If SmarterASP memory/CPU or native-runtime support is poor, hosted
  embeddings are simpler and still cost pennies.
- Cache by `(normalizedInputHash, modelVersion)`. Re-embed only changed text.
- Cache LLM decisions by unordered pair + evidence hash + prompt/model version.
- Sample rejected/low-score pairs for eval; never LLM-score the full Cartesian
  product.

### Hosted OpenAI option (not mandatory)

As of 30 August 2026, official OpenAI documentation lists GPT-5.6 Luna as a
cost-sensitive high-volume model with text/image input, structured outputs and
standard token prices of $0.20/M input and $1.20/M output
([model page](https://developers.openai.com/api/docs/models/gpt-5.6-luna)). The
Batch API offers a 50% discount and completes within 24 hours, and supports both
Responses and embeddings endpoints
([Batch documentation](https://developers.openai.com/api/docs/guides/batch)).
Therefore the planning rates used below are $0.10/M input and $0.60/M output for
Luna Batch. `text-embedding-3-small` is listed at $0.02/M input, so the Batch
planning rate is $0.01/M
([embedding model page](https://developers.openai.com/api/docs/models/text-embedding-3-small)).
Verify pricing, availability and account limits again at implementation time.

Use Luna Batch for structured duplicate/content ambiguity. Use GPT-5.6 Terra
Batch only for the four monthly editorial drafts if the gold set shows a
material quality gain; the current Batch table lists $1/M input and $6/M output
([official pricing](https://developers.openai.com/api/docs/pricing)). No
fine-tuning is proposed. Pin a model snapshot when one is published/available;
otherwise record the alias and response metadata and rerun evals before upgrades.

Alternatives include a local ONNX embedding model plus rules with no generative
model, another hosted embedding/LLM provider that meets the same structured
output/data-handling/eval contract, or manual adjudication. Provider adapters
should implement internal `IEmbeddingProvider` and `IStructuredSuggestionProvider`
interfaces; business decisions must not depend on provider-specific response
objects.

### Monthly cost model

Assumptions: 10% of offers have changed text and are embedded each month at 120
tokens; 2% of all offers reach LLM duplicate adjudication at 900 input/180 output
tokens; 1% receive a content suggestion at 1,200 input/250 output tokens; four
Terra Batch blog drafts use 12k input/2.5k output each; vision is exceptional and
represented as a conservative reserve; then 25% is added for retry/eval overhead.
Prompt caching savings, free local inference and currency conversion are ignored.

| Active offers | Embeddings | Luna match adjudication | Luna content suggestions | 4 Terra drafts | Vision reserve | Subtotal | +25% contingency |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1,000 | $0.0001 | $0.0040 | $0.0027 | $0.1080 | $0.25 | $0.3648 | **$0.46** (round operating cap: $1) |
| 10,000 | $0.0012 | $0.0396 | $0.0270 | $0.1080 | $0.50 | $0.6758 | **$0.85** (cap: $2) |
| 100,000 | $0.0120 | $0.3960 | $0.2700 | $0.1080 | $2.00 | $2.7860 | **$3.49** (cap: $5) |

The primary cost risk is a broken candidate gate, not token price. Enforce the
2%/1% escalation ceilings; when exceeded, defer to review instead of spending.
Any web-search tool calls, taxes, regional-processing uplift, exchange rate,
hosting and human review are additional. The first production month should be
shadow mode with a $1 absolute AI cap regardless of catalogue size.

## Build versus buy / hosted versus local

| Capability | Build/local | Hosted/buy | Recommendation |
|---|---|---|---|
| Scheduling/queue | SQL leases + existing worker | Hangfire/cloud queue | Build for MVP; current stack already has jobs and restart logic. |
| Deterministic identity | C# normalizers, SQL blocks, pHash | Product matching SaaS | Build; domain-specific evidence and auditability matter, catalogue starts small. |
| Text embeddings | ONNX MiniLM-class model | OpenAI/other embedding API | Benchmark both on the same gold set. Prefer local if host supports it reliably; hosted cost is negligible and operationally simpler. |
| Vector search | Blocked in-memory comparisons/SQL candidate tables | Vector DB | Do not buy. Revisit only when blocked candidates exceed practical batch processing. |
| LLM adjudication/repair | Small local instruct model | Hosted structured-output model | Hosted small model first if enabled; shared Windows hosting is a poor place for a larger generative model. Keep provider abstraction. |
| Vision | Local pHash first | Hosted multimodal model | pHash fully automatic; hosted vision only for <0.1% ambiguous pairs and only after image-use/data review. |
| SEO/demand | Internal aggregates + Search Console import/API | SEO content suite | Build/import first. Do not buy until content has demonstrated value. |
| Review workflow | Extend Blazor admin | External DAM/PIM | Build now; existing approval and row-version model are strong foundations. |

## Evaluation and rollout

### Gold sets

Create versioned, reviewer-labelled fixtures before model integration:

- **Identity:** at least 500 candidate pairs, stratified across exact duplicates,
  different merchants/prices/SKUs, translations, same image/different pack,
  variants, accessories and hard negatives. Two reviewers; adjudicate disagreement.
- **Lifecycle:** at least 200 offer histories containing temporary query misses,
  transient errors, delisting, currency/price changes, restoration and link decay.
- **Replacement:** at least 100 unavailable offers with acceptable and unsafe
  alternatives, including “no suitable replacement.”
- **Product copy:** at least 200 source packets and editor-approved/rejected
  suggestions, deliberately rich in quantities, units, brands, licensed terms,
  awkward translations and missing fields.
- **Articles:** 20 briefs/drafts including high-overlap/cannibalisation, stale
  products, weak evidence, disclosure placement and unsupported claims.

Keep train/tuning, threshold-selection and final test slices separate. Store
labels, rationale, reviewer and fixture license/provenance. Do not use production
personal data.

### Release thresholds

| Decision | Minimum gate |
|---|---|
| Automatic canonical membership | Precision >=99.5% and lower 95% confidence bound >=99%; hard-conflict false merges = 0; rollback = 100%. Recall is secondary. |
| Review duplicate queue | Precision >=95%, recall >=85% on candidate pairs; p95 <=5 suggested pairs/offer. |
| Expiry auto-hide | Precision >=99%, false-hide rate <0.5%, direct evidence twice >=24h apart; restoration path passes every fixture. |
| Replacement suggestions | Top-3 suitable recall >=80%, precision >=90%, prohibited/off-scope rate = 0, “none” supported. |
| Content suggestion | Unsupported entity/number/unit claims = 0; source-fact retention = 100%; median editor rubric >=4/5; acceptance >=80%. |
| Automatic mechanical cleanup | Meaning/entity/number changes = 0 over all fixtures; reversible diff stored. |
| Article draft | Claim-evidence coverage = 100%, disclosure/internal-link/freshness checks = 100%, no near-duplicate above calibrated threshold, human rubric >=4/5. |

Content rubric (1–5 each): factual fidelity, clarity/readability, information
value, specificity without hype, grammar, shop tone, SEO naturalness, disclosure
and provenance. Also track editor acceptance, time-to-review, edit distance,
post-publication corrections, search impressions/clicks and affiliate click
quality. Do not optimize for raw word count or publication volume.

### Staged rollout

1. Offline POC and gold-set baselines.
2. Shadow mode: compute flags/matches/lifecycle actions but change nothing.
3. Review-only mode for one shop and 10% sample.
4. Automatic mechanical cleanup and safety demotion; expand to full shop.
5. Automatic high-confidence canonical links and evidence-backed expiry only
   after thresholds hold for four weeks and a manual reversal drill succeeds.
6. A/B test product copy at the canonical-product level (not variants/offers),
   measuring useful clicks, returns/corrections and engagement guardrails. Do not
   A/B index many near-duplicate SEO pages.

Key failure modes: source outage mistaken for expiry; two variants collapsed;
pack-size mismatch; translated title interpreted as a new product; image CDN
change triggering false novelty; prompt injection in merchant text; LLM adding a
safety/authenticity claim; stale price in prose; replacement chosen for higher
commission; scheduler overlap; budget runaway; model drift; review backlog;
generated articles cannibalising existing pages. The evidence gates, hard
conflicts, SQL leases, caps, immutable versions and human review above directly
mitigate them.

## Phased roadmap and estimated effort

Estimates are focused engineering days for one developer familiar with the
repository, plus named editorial/data work. They exclude waiting for host/network
answers and legal review.

### MVP: trustworthy freshness and deterministic quality (4–6 weeks)

1. Confirm SmarterASP .NET 10/SQL version/task entitlement; capture the current
   AliExpress agreement and ask support for quota/cache/permission answers.
2. **Complete locally:** source observation/content hashes, `LastCheckedUtc`,
   lifecycle evidence/state, consecutive misses, change events and safe backfill.
3. **Complete locally:** SQL automation items, unique idempotency keys, leases,
   checkpoints, bounded runs, retry/dead-letter handling and harmless wake
   endpoint; production remains disabled.
4. Implement direct refresh cohorts, expiry grace rules, feed-health metrics and
   link-health dashboard using the official adapters.
5. **Core complete locally:** add versioned normalizers, GTIN/model/pack/size/unit
   parsing, bounded match evidence tables and paged deterministic review. Image
   hashes and expanded labelled calibration remain follow-on work.
6. **Core complete locally:** content mechanical rules, immutable named content
   versions, field diffs, approval validation and restore-as-new-revision admin
   workflow.
7. Create identity/lifecycle/copy gold sets and shadow-mode reports.

Effort: 18–28 engineering days plus 4–6 editorial/data-label days.

### Next: cheap semantic escalation and replacements (3–5 weeks)

1. Benchmark local ONNX embeddings versus hosted embeddings on the identity set.
2. Add embedding candidate generation and provider-neutral interfaces/caches.
3. Add budgeted structured LLM adjudication and content suggestions, all
   review-only; implement full audit and prompt/model versioning.
4. Add pHash and tightly capped vision escalation.
5. Build replacement ranking/review UI and content fact validators.
6. Run four weeks of review-only metrics; calibrate thresholds.

Effort: 15–24 engineering days plus 5–8 reviewer days.

### Later: evidence-led editorial program and scale (3–6 weeks)

1. Add privacy-minimized internal search/demand aggregates and authorized Search
   Console import/API.
2. Add article/brief/version/evidence/internal-link models and review screens.
3. Add cannibalisation/freshness checks and current-fact product modules.
4. Pilot no more than four human-approved pieces/month and measure value.
5. Consider managed worker/queue or SQL 2025 vector search only after measured
   capacity limits; consider carefully scoped automatic canonical links/expiry
   only after evaluation gates hold.

Effort: 15–27 engineering days plus ongoing editor/legal review.

## Prioritized backlog with acceptance criteria

| Priority | Item | Effort | Acceptance criteria |
|---|---|---:|---|
| P0 | Capture current affiliate agreement, quota/cache answers and host capabilities | 1–2d active work | Evidence stored under existing AliExpress convention; unknowns explicitly remain unknown; no implementation assumes missing permission. |
| P0 | Observation/lifecycle migration | 4–6d | Idempotent backfill; two identical responses create no duplicate change event; query miss alone cannot hide; every state change has evidence and rollback. |
| P0 | Durable automation item/lease model and wake endpoint | 4–6d | Concurrent ticks claim once; killed worker resumes after lease; endpoint cannot directly mutate or accept job parameters; 15-minute bounded run tested. |
| P0 | Refresh/feed/link health planner | 4–6d | Cohorts/cadences configurable; official batch limits respected; alerts/metrics cover health thresholds; source outage fixture hides zero products. |
| P0 | Gold-set/eval harness | 3–5d | Offline CI report contains confusion matrices, per-class metrics, threshold/version and cost; no network or secrets. |
| P1 | Deterministic identity and evidence | 6–9d | All POC cases and gold set handled; pack/size conflicts cannot auto-link; decisions are reversible/explainable. |
| P1 | Versioned content and mechanical quality repair | 5–8d | Source untouched; immutable diff/provenance; unsupported numeric/entity changes rejected; admin can approve/reject/rollback. |
| P1 | Review queue UX | 4–7d | Paging beyond 50, filters/priorities, pair evidence, content diff, concurrency conflict and audited bulk preview. |
| P1 | Embedding benchmark/provider abstraction | 3–5d | Local/hosted evaluated on same test split, cached by hash/version, chosen option documented; no model call on unchanged data. |
| P1 | Budgeted LLM/vision escalation | 4–7d | Strict schema, prompt injection fixtures, per-purpose kill/cost limits, <configured candidate percentage, every result audited and review-only. |
| P1 | Replacement suggestions | 4–6d | Top three reasons/conflicts/freshness shown; no automatic redirects; thresholds pass. |
| P2 | Demand and article workflow | 7–11d | First-party demand provenance, duplicate/cannibalisation gate, claim sources, disclosure/freshness/internal-link checks, separate publish action. |
| P2 | Capacity/managed-worker decision | 1–2d | Based on measured queue age, CPU/memory, SQL version and failure rate; no speculative service purchase. |

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Full 2025 affiliate agreement/cache rule is missing | Treat storage permission as unresolved, retain minimum necessary raw data, add configurable retention, obtain and archive current terms before production automation. |
| Advanced/SKU APIs remain unavailable | Make SKU/variant/delivery optional; lower match confidence when absent; never infer these facts; apply only after an owner decision. |
| Shared IIS process sleeps/recycles | SmarterASP wake request, small leased SQL work batches and resume checkpoints; no in-memory-only queue. |
| API quota is unknown | Concurrency 1, configurable daily request ceiling, jitter/backoff/circuit breaker, priority cohorts and metrics. |
| False duplicate corrupts catalogue | Link, never delete/overwrite offers; hard conflicts; very high auto threshold; rollback; four-week shadow/review stage. |
| AI hallucination or prompt injection | Fact packets, strict structured schema, deterministic claim validator, merchant text as delimited untrusted data, all generative edits review-only. |
| Generated content becomes scaled/low-value | Four/month cap, first-party demand, unique-value requirement, cannibalisation gate, human approval and freshness withdrawal. |
| Costs spike | Hash/change gates, candidate caps, cached responses, Batch, per-purpose budgets and fail-closed deferral. |
| Local models destabilize shared hosting | Benchmark memory/CPU; run bounded offline worker; use penny-scale hosted embedding instead if needed; never infer on requests. |
| Review backlog makes flags useless | Priority/SLA, queue-age alerts, sampling, bulk preview for deterministic actions, stop generating suggestions when backlog exceeds threshold. |
| Model/prompt drift | Pinned/recorded versions, immutable results, eval gate before change, shadow replay and rollback. |

## Proof-of-concept and implementation hand-off

The included offline POC is intentionally narrow. It demonstrates that exact
image evidence plus compatible attributes can link differently titled/priced
merchant offers, while pack and size conflicts become bundle/variant relations.
It does not claim production thresholds or use embeddings/AI.

Run it with:

```powershell
dotnet run --project ./tools/AffiliateSuperstore.CatalogueQualityPoc
```

Success prints explainable pair decisions and exits zero; any fixture mismatch
exits non-zero. The first implementation change should convert these synthetic
fixtures into a versioned, reviewer-labelled set of real records that the
affiliate terms permit retaining.

Before implementation, the owner needs only three decisions:

1. confirm the production SmarterASP plan/task entitlement and SQL/.NET versions;
2. decide whether hosted AI may be trialled under a $1 shadow-mode cap after a
   data-handling review; and
3. nominate the editor/reviewer accountable for product copy and articles.

Everything else can begin deterministic-first without a paid service.
