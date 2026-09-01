# Affiliate Superstore project plan

Last updated: 1 September 2026

## Product objective

Build a UK-focused, multi-shop affiliate supermarket that discovers and
curates AliExpress affiliate products, presents them through themed shop paths,
and sends customers to AliExpress for checkout. The application never takes
payment or places an AliExpress order.

The first viable shop is plushies and related collectables. The platform must
be reusable for additional shops without another application runtime.

## Working principles

- Continue through safe, in-scope implementation work without waiting for a
  generic instruction to continue.
- Ask only when a missing decision would materially change the product or when
  new authority is needed for an external or irreversible action.
- Keep the public experience Razor Pages-first and use interactive Blazor for
  the operational admin.
- Treat AliExpress as the source of product truth, while keeping auditable
  local snapshots for catalogue performance and order reconciliation.
- Use SQL rules, normalized facts, hashes and change detection before AI. Keep
  model output advisory unless a documented evaluation threshold explicitly
  permits a reversible automatic action.
- Keep secrets outside source control and preserve source evidence for every
  programme-rule decision.
- Build one runtime with domain/path-aware shop configuration, theming and
  content rather than duplicating applications.

## Decisions made

| Area | Decision |
|---|---|
| Market | United Kingdom, English, GBP, ship-to GB |
| Initial shop | Plushies and adjacent collectables |
| Public UI | ASP.NET Core Razor Pages |
| Admin UI | Interactive Blazor Server components |
| Design system | Custom semantic CSS tokens and reusable components; no Bootstrap or per-shop arbitrary CSS |
| Hosting target | Existing SmarterASP.NET W1050-EU plan; site `wonderaisle`, dedicated 1 GB .NET Core pool `hydraadmin-001E96` and isolated 1 GB SQL Server 2022 database are provisioned; Web Deploy and three unused scheduled URLs remain available |
| Checkout | AliExpress checkout only; no payments or order placement in this application |
| Basket | Anonymous local shopping list with one-by-one affiliate hand-off |
| Identity | No customer account for MVP; anonymous cookie/browser identifier only where necessary |
| Multi-shop routing | One neutral primary domain with shops under paths such as `/plushies` |
| Umbrella identity | Wonder Aisle at canonical origin `https://wonderaisle.co.uk`; the domain, managed TLS, HTTP-to-HTTPS redirect, HSTS and deployed application are working |
| AliExpress credentials | .NET configuration with local User Secrets in development and protected hosting configuration in production |
| Plushies Tracking ID | Use existing durable ID `theplushyshop` |
| Tracking scale | Optional durable Tracking ID per important shop, shared fallback for others; `cn`, `cv` and opaque `dp` provide detailed attribution |
| Catalogue intelligence | Extend the existing .NET/SQL pipeline; deterministic identity, lifecycle and quality rules first, embeddings/LLM/vision only for ambiguous cases |
| AI publication policy | Product rewrites, replacement choices and all blog content remain human-approved; AI can never set source facts such as price, availability, SKU, pack size or delivery |
| AI cost policy | Hash/cache unchanged inputs, use asynchronous batches where suitable, enforce per-purpose kill switches and begin hosted-AI shadow mode with an absolute $1 monthly cap |

## Tracking taxonomy

Tracking IDs are permanent and limited to 50, so they are not suitable for
products, pages or short campaigns.

| Field | Planned use | Example shape |
|---|---|---|
| Tracking ID | Durable high-level shop/channel | `theplushyshop` |
| `cn` | Internal shop or campaign slug | `plushies` |
| `cv` | Placement/creative type | `search-card`, `product-cta`, `basket` |
| `dp` | Opaque unique outbound-click ID | random non-personal identifier |

The `dp` value will join an outbound click to an AliExpress S2S paid-order
notification. It must never contain an email address, customer ID or other
personal information.

## MVP definition

The MVP is complete when:

1. `/plushies` serves a branded, crawlable product catalogue from SQL Server.
2. Catalogue data is populated and refreshed automatically through the
   AliExpress Affiliate API for GB/GBP/EN.
3. Search, category, price, delivery and popularity filters work without
   querying AliExpress on every visitor request.
4. Product and listing links are generated through the affiliate API, carry
   the correct shop/placement/click attribution and are refreshed before expiry.
5. An anonymous shopping list survives browser restarts and hands each item to
   AliExpress clearly and safely.
6. The admin shows catalogue freshness, API health, links, outbound clicks,
   S2S paid orders, reconciled settlement state and ingestion failures.
7. Affiliate disclosure, privacy, prohibited-product controls and AliExpress
   brand restrictions are implemented.
8. Canonicals, sitemaps, structured data and index-quality rules prevent thin
   or duplicate shop/search pages from polluting search results.
9. The admin is authenticated before any public deployment.
10. Deployment and rollback are documented and verified without harming other
    sites sharing the SmarterASP.NET application pool.

## Delivery phases

| Phase | Outcome | Status |
|---|---|---|
| 0. Evidence and feasibility | API, programme rules, hosting direction, basket limits and compliance evidence | Complete; 2025 agreement still to capture |
| 1. API foundation | Typed Affiliate API client, signing, response normalisation, test workbench and live smoke coverage | Complete |
| 2. Shop and tracking model | Shop/path/theme configuration and hybrid tracking taxonomy | Complete |
| 3. Persistence | SQL Server catalogue, snapshots, ordered product media, links, clicks, jobs and order state | Complete; additive freshness/lifecycle evidence and change history are included |
| 4. Automation | Scheduled discovery, product-detail refresh, curation, link renewal and failure recovery | Operational in production; a protected 15-minute wake schedule, durable due-state, lease renewal, retry/dead-letter recovery, freshness alerts and guarded manual publication are working |
| 5. Public plushies MVP | Razor Pages catalogue, rich product pages, search/filtering and disclosures | Functionally complete for the current product slice; approved-only catalogue/detail pages, API-backed galleries and richer facts, category/price/popularity filters, curated content, disclosure and click redirect are working |
| 6. Shopping list | Anonymous basket-style experience and one-by-one hand-off | Functionally complete for MVP; protected 90-day list, count and next-item hand-off are working |
| 7. Conversion operations | S2S, reconciliation, retention and monetisation dashboard | Functionally complete for local MVP; restart-safe pull reconciliation, monthly 180-day recovery, guarded S2S inbox, click attribution, durable SQL retention, safe CSV export and performance reporting are working; production S2S setup remains |
| 8. SEO/content and visual system | Structured data, sitemaps, editorial landing pages, index controls and reviewed shop identities | MVP launch gate reached; 12 distinct products have original reviewed copy, canonical URLs, quality-gated sitemap membership, Product/ItemList JSON-LD and live `index,follow` directives. Filtered pages and the thin umbrella home remain `noindex,follow`; broader visual/content work can continue without blocking the plushies launch. |
| 9. Production | Admin authentication, security, domain, GitHub, SmarterASP release and monitoring | Operational for the public plushies MVP; release `0ad6ba8`, a fresh isolated backup, all 14 migrations, protected recurring catalogue automation, the owner account, 12-product reviewed catalogue and search indexing are live. Managed TLS, redirects, HSTS, health, affiliate redirects and all applicable neighbouring-site checks pass; production S2S enablement remains a later conversion-operations task. |

## AI-assisted catalogue and content integration

The decision-ready design, repository findings, data model, cost assumptions,
evaluation thresholds and complete backlog are maintained in
[`AI_CONTENT_AUTOMATION_PLAN.md`](AI_CONTENT_AUTOMATION_PLAN.md). It is the
implementation authority for AI-assisted catalogue work; this section fixes its
place in the main delivery sequence.

### Integration decisions

- Do not create a separate AI service for the MVP. Extend the current
  application services, EF Core model, SQL job history and Blazor review area.
- Treat the current AliExpress-keyed `ProductRecord` as a source offer and add a
  canonical-product/variant layer. Matching links offers; it never deletes or
  overwrites merchant prices, SKUs, source text or snapshots.
- Use a SmarterASP scheduled URL only to wake the application. SQL due-state,
  idempotency keys and expiring leases decide and resume work; the GET request
  itself must not mutate catalogue data or accept job parameters.
- Preserve original source content and create immutable editorial versions with
  field-level provenance, diffs, validation results, reviewer and rollback.
- Keep AI out of public requests. Source refresh and deterministic safety rules
  continue when a model provider is unavailable or its budget is exhausted.
- Keep production automation and every model provider disabled until admin
  authentication, host capability, migrations, rollback and evaluation gates
  are verified.

### Coordinated implementation sequence

| Workstream | Main-build outcome | Dependencies | Status |
|---|---|---|---|
| AI-0. Evidence and controls | Capture the current affiliate agreement and API quota/cache answers; verify SmarterASP .NET 10, SQL version and scheduled-task entitlement; define feature flags and budgets | Existing phase 0 and phase 9 work | Planned; blocks production enablement, not offline development |
| AI-1. Freshness foundation | Add source-observation hashes, `LastCheckedUtc`, lifecycle evidence, consecutive misses, change events and reversible availability state | Persistence migrations and existing ingestion/link adapters | Complete locally; migration, admin visibility and lifecycle regression coverage added 31 August 2026 |
| AI-2. Durable automation | Add SQL work items, unique idempotency keys, leases/checkpoints, bounded retries, independent job types, health metrics and a harmless wake endpoint | AI-1 schema; SmarterASP verification before production | Live in production from release `0ad6ba8`: long work renews its lease, cycle failures do not stop the host, the wake endpoint requires a fixed secret, SmarterASP signals it every 15 minutes, and admin exposes freshness/link/availability alerts, full run history and dead-letter retry |
| AI-3. Deterministic identity and review | Add normalized identifiers/units/pack size, image metadata, candidate blocking, explainable confidence, gold-set evaluation and paged admin review | AI-1 observations and current approval gate | Core, exact-image evidence and calibration workflow deployed in release `fb68023`; immutable reviewer labels, protected tuning/threshold/final-test slices, disagreement/adjudication handling, Wilson confidence bounds and false-merge reporting are live; populating the 500-pair labelled set remains editorial work |
| AI-4. Versioned content quality | Add mechanical quality rules, immutable editorial versions, claim provenance, diffs and rollback; no generative auto-approval | AI-1 facts and AI-3 review primitives | Core complete locally 31 August 2026; immutable named revisions, optimistic edit protection, deterministic claim validation, approval gating, admin evidence/diffs and restore-as-new-revision are working |
| AI-5. Optional semantic escalation | Benchmark local versus hosted embeddings; add cached provider-neutral embedding/LLM/vision adapters in shadow/review-only mode | AI-3 gold set, admin authentication, data-handling review and budget controls | Later; disabled by default |
| AI-6. Responsible editorial content | Add first-party demand aggregates, briefs, evidence, duplication/cannibalisation, disclosure, internal links, freshness and a separate human publish action | Phase 8 SEO foundations, AI-4 versioning and an accountable editor | Later; maximum four reviewed drafts per month during pilot |

### Main-build acceptance gates

- Every schema change is additive/backfilled, migration-tested and has a
  documented rollback; existing public queries keep working throughout.
- Source outages or keyword-page misses hide no products. Automatic expiry
  requires direct source evidence twice at least 24 hours apart and >=99%
  precision on the labelled lifecycle set.
- Automatic canonical membership requires no hard pack/size/model conflict and
  >=99.5% measured precision. Offers and evidence remain independently
  recoverable.
- Generated copy has zero unsupported entity/number/unit claims in the test set,
  remains review-only and cannot alter source price, stock or delivery fields.
- Model calls are absent for unchanged hashes, capped by purpose and fully
  audited with provider/model, prompt, validator, token and cost versions.
- No article is automatically published. Every draft passes source, freshness,
  disclosure, internal-link and cannibalisation checks plus human approval.
- CI remains offline: deterministic fixtures and provider fakes run in the
  existing test project; live/paid model calls are prohibited in builds.

The standalone offline catalogue-quality proof under
`tools/AffiliateSuperstore.CatalogueQualityPoc` is planning evidence, not a
production project. When AI-3 starts, its identical/translation/bundle/variant/
unrelated cases must be moved into the existing xUnit suite and expanded with
reviewer-labelled catalogue records before matching code enters the application.

## Phase 1 acceptance criteria

- Every published Affiliate API operation is represented by a named method and
  typed request object where it accepts business parameters.
- Standard operations can be exercised from the Blazor workbench without
  displaying secrets or live signatures.
- Advanced/SKU operations are visible and clearly identified as unavailable
  until permission is granted.
- Order operations require deliberate date/status/order input and never run
  broad accidental queries.
- AliExpress method-specific response envelopes and platform errors are
  normalised consistently.
- Unit tests cover signing, parameter construction, response normalisation and
  important validation limits.
- A repeatable live smoke tool verifies categories, product search, product
  details, featured promotions and link generation.

## Domain decision

The domain must be neutral and must not use AliExpress, AE, AliE or a confusing
variant. The owner selected and registered `wonderaisle.co.uk` on 31 August
2026. Earlier
shortlist order was:

1. `wonderaisle.co.uk` — selected umbrella brand and canonical domain.
2. `playfulfinds.co.uk` — warmer and well suited to the first niche, but less
   naturally supermarket-like.
3. `wonderbasket.co.uk` — clearly shopping-led, though the product does not
   provide a single AliExpress checkout basket.
4. `joyaisle.co.uk` — short and broad.
5. `treasuretrolley.co.uk` — distinctive but longer.

The pre-purchase availability checks are historical evidence rather than
ongoing availability claims. They are not trademark clearance. The rationale
and architecture are in
[`DOMAIN-RECOMMENDATION.md`](DOMAIN-RECOMMENDATION.md).

## Current risks and evidence gaps

- The full Affiliate Programme Service Agreement effective 1 April 2025 has
  not yet been captured; the full local copy is the 2022 agreement.
- Cookie duration and the attribution tie-break rule remain unpublished.
- The account's precise API quota is not displayed.
- Advanced API and SKU Dimension permission groups are inactive.
- Coupon/promotion-info returned `InsufficientPermission` in a live test even
  though the method appears in the published affiliate surface; treat it as a
  separate unavailable capability until AliExpress confirms or grants access.
- The account's commission model should be rechecked after its verified-site
  classification finishes updating.
- Automated quality screening now catches common prohibited, off-niche,
  ambiguous-quantity, variant-pricing and third-party-character risks, but it
  deliberately does not replace human editorial and image review.
- Admin access now requires the ASP.NET Core Identity Administrator role. There
  is no registration; local first-owner setup is loopback/Development/empty-
  database only and production uses temporary protected bootstrap settings.
- The expanded live discovery pool still includes pet toys, variant-price
  listings and likely third-party-character merchandise. Those products are
  held from publication by persisted flags or manual review; the local launch
  set contains only 12 individually checked and edited products.
- Production now fails fast unless ASP.NET Core Data Protection keys are sent
  to a configured persistent private directory outside `wwwroot`; the host
  path and backup behavior still require verification.

## Current implementation snapshot

As of 31 August 2026, the local SQL Server database contains one configured
shop, 211 products, 232 immutable price/commission snapshots, 211 active
affiliate links and 18 successful ingestion jobs. The guarded full-discovery
run completed all 12 planned API requests, reading and writing 196 products and
refreshing 196 links. Automated reassessment leaves 119 products quality-clear
and flags 92 for persisted review reasons. Human review has produced a
12-product editorially complete, approved and indexable local launch set; 93
products need review, 102 remain pending and four have been rejected.

Implemented operational surfaces:

- `/admin/api-test` — permission-aware workbench for all 16 documented methods.
- `/admin/database` — connectivity, migrations and operational counts.
- `/admin/catalogue` — live discovery, a guarded full-plan action, publication
  readiness totals, persisted automated quality flags, prioritised filters,
  guarded approval actions and a curation drawer for public titles,
  descriptions, featuring and display order.
- `/admin/automation` — schedule, retry policy, next-run and due-state visibility.
- `/admin/orders` — paid/confirmed/settled/invalid lifecycle totals, base and
  incentive commission reporting, click attribution, S2S readiness, safe
  development CSV export and incremental/full reconciliation actions.
- `/admin/performance` — selectable-window clicks, converting clicks,
  click-to-order rate, active-link use, attributed orders, S2S events and
  commission breakdowns by campaign/placement and product.
- `/plushies` — SQL-backed approved-only catalogue with local search,
  category, price and popularity sorting.
- `/plushies/product/{productId}` — approved-only product detail with current
  snapshot facts, disclosure, save and tracked hand-off actions.
- `/basket/plushies` — protected anonymous list retained for 90 days.
- `/go/{shop}/{product}` — approved-only tracked redirect with an auditable
  outbound-click record.
- `/sitemap.xml` — only products passing the editorial, image, price and
  freshness index-quality policy; thin shop landing pages are withheld until
  they contain at least 12 indexable products.
- `/robots.txt` — blocks admin, saved-list and redirect routes and advertises
  the sitemap when indexing is enabled.

The web application no longer depends on Bootstrap. Shared primitives and
semantic tokens are defined once, with controlled brand, accent, canvas,
surface, text and profile values supplied per shop. The current `playful`
profile proves the mechanism; visual exploration and review are specified in
`docs/DESIGN-SYSTEM-BRIEF.md` before a final identity is implemented.

Editorial approval is enforced below the UI: inactive or ineligible products,
products with automated quality flags, and products without an active affiliate
link cannot be approved. Quality reassessment always includes the original
AliExpress title as well as any editorial title, so rewriting visible copy
cannot hide a source-listing risk. The 12 locally approved products now provide
a meaningful end-to-end curated catalogue on both the catalogue and
product-detail pages. Approval followed source-title screening, offer-shape
checks and individual image review; source risks and misleading images were not
papered over with editorial copy.

Development catalogue automation is enabled with a 24-hour refresh, 15-minute
poll, 60-minute failure retry and two-hour stale-job recovery. The plushies
shop currently expands six controlled discovery queries across two pages into
12 sequential API requests per refresh. A process-wide guard prevents
overlapping manual and scheduled plans, and a failed request stops the
remaining plan. Every result still passes the persisted quality gate.
Active product links older than 120 hours are revalidated in batches of 50;
changed URLs replace and expire the previous link through an audited
`LinkRefresh` job. The worker reads SQL job history on startup, so restarting
the app does not trigger duplicate work. Production automation remains disabled
until the hardened release, protected wake token and scheduled URL are deployed
and verified.

Canonical URLs collapse all search, price, category and sort variants back to
the unfiltered shop URL; filtered variants remain `noindex,follow`. Product
pages require original editorial copy, an image, a positive price and a fresh
snapshot before they can be indexed. Production indexing is additionally
disabled by default through `Seo:IndexingEnabled`; it must be deliberately
enabled only after domain, content, privacy and release review. Local
development enables the switch so the eligible and ineligible paths can be
verified.

Development order reconciliation is enabled on a 60-minute schedule. The first
run walks all four documented order states through the index cursor over the
180-day retention window; later discovery runs overlap by 48 hours, force a
complete 180-day recovery scan every 30 days and refresh every known
non-terminal sub-order directly until Completed Settlement or Invalid.
Checkpoints and metrics are stored in the shared ingestion-job history. The SQL
order table is the authoritative archive beyond the API query window. A
spreadsheet-injection-safe CSV backup is available only to an authenticated
administrator in Development. A live account run on 30 August 2026
completed successfully and found no orders in the current retention window;
AliExpress's platform code 405 / "The result is empty" is normalised only for
that exact benign response.

The performance report joins outbound click IDs to reconciled orders and
aggregates channels, products and estimated/settled commission by currency.
Invalid orders are retained for audit but excluded from commission. Because
page impressions are not yet stored, the UI deliberately reports click-to-order
conversion rather than claiming an impression-to-click CTR.

The S2S paid-order route has an immutable, duplicate-suppressed inbox and feeds
the same order/click model, including base commission, CPX incentive and new-
buyer bonus fields. Because AliExpress documents no callback signature, it is
disabled by default and additionally requires a fixed secret parameter. Admin
authentication is complete; S2S must remain disabled until a public HTTPS
endpoint and protected production configuration exist. See `docs/S2S-SETUP.md`.

## Next milestone

Managed custom-domain TLS, HTTP-to-HTTPS redirection and HSTS are now verified.
AI-1 freshness, AI-2 durable work leasing, AI-3 deterministic identity/review
and AI-4 versioned editorial quality are deployed through release `0ad6ba8`.
Protected recurring production automation now wakes every 15 minutes. Its first
supervised cycle expanded the manual review queue to 190 candidates and
refreshed all 12 published products without failures or automatic publication.
Search indexing is enabled for the quality-gated shop and product URLs. The
identity gold set now has 12 owner-authorized first-review
tuning labels, but still needs an independent second reviewer, adjudication and
progress toward the 500-label target before any automatic canonical linking can
be considered. Lifecycle/copy evaluation sets, production S2S setup and later
hosted/local model trials remain gated follow-on work.

Impression tracking can be added when a real CTR is operationally useful. The
catalogue-depth and SEO launch gates have been met. The provider-hosted
production release is live and indexable over the canonical HTTPS origin.
