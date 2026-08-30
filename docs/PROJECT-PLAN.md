# Affiliate Superstore project plan

Last updated: 30 August 2026

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
| Hosting target | SmarterASP.NET-compatible ASP.NET Core and SQL Server; final runtime support must be checked before deployment |
| Checkout | AliExpress checkout only; no payments or order placement in this application |
| Basket | Anonymous local shopping list with one-by-one affiliate hand-off |
| Identity | No customer account for MVP; anonymous cookie/browser identifier only where necessary |
| Multi-shop routing | One neutral primary domain with shops under paths such as `/plushies` |
| AliExpress credentials | .NET configuration with local User Secrets in development and protected hosting configuration in production |
| Plushies Tracking ID | Use existing durable ID `theplushyshop` |
| Tracking scale | Optional durable Tracking ID per important shop, shared fallback for others; `cn`, `cv` and opaque `dp` provide detailed attribution |

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
| 3. Persistence | SQL Server catalogue, snapshots, links, clicks, jobs and order state | Complete |
| 4. Automation | Scheduled discovery, refresh, curation, link renewal and failure recovery | In progress; restart-safe discovery schedule and retry policy are working |
| 5. Public plushies MVP | Razor Pages catalogue, product pages, search/filtering and disclosures | In progress; approved-only listing, local search, disclosure and click redirect are working |
| 6. Shopping list | Anonymous basket-style experience and one-by-one hand-off | In progress; protected persistent list and one-by-one hand-off are working |
| 7. Conversion operations | S2S, reconciliation, retention and monetisation dashboard | Planned |
| 8. SEO/content | Structured data, sitemaps, editorial landing pages and index controls | Planned |
| 9. Production | Admin authentication, security, domain, GitHub, SmarterASP release and monitoring | Planned |

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

## Domain direction

The domain must be neutral and must not use AliExpress, AE, AliE or a confusing
variant. Current naming direction:

1. `wonderaisle.co.uk` — preferred umbrella brand.
2. `playfulfinds.co.uk` — warmer and well suited to the first niche, but less
   naturally supermarket-like.
3. `wonderbasket.co.uk` — clearly shopping-led, though the product does not
   provide a single AliExpress checkout basket.
4. `joyaisle.co.uk` — short and broad.
5. `treasuretrolley.co.uk` — distinctive but longer.

Availability, UK company names and trademarks must be checked immediately
before registration. A positive domain-availability result is not a trademark
clearance.

Nominet WHOIS returned **not registered** for all five names at 18:20 UK time
on 30 August 2026. This is a point-in-time result only; recheck at purchase.

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
- Product content needs quality and prohibited-product screening; API inclusion
  alone is not sufficient editorial approval.
- Public admin access is forbidden until authentication and authorisation are
  implemented.
- The first live discovery results include pet toys and likely third-party
  character or celebrity merchandise. All 18 products remain unapproved; no
  imported item is public merely because the Affiliate API returned it.
- ASP.NET Core Data Protection keys must be stored persistently on the
  production host so protected shopping-list cookies survive application
  restarts and deployments.

## Current implementation snapshot

As of 30 August 2026, the local SQL Server database contains one configured
shop, 18 products, 36 immutable price/commission snapshots, 18 active affiliate
links and two successful live discovery jobs. Both jobs read and wrote 18
products and refreshed 18 links. Every product is still in `Pending` review.

Implemented operational surfaces:

- `/admin/api-test` — permission-aware workbench for all 16 documented methods.
- `/admin/database` — connectivity, migrations and operational counts.
- `/admin/catalogue` — live discovery, job result, catalogue and review actions.
- `/admin/automation` — schedule, retry policy, next-run and due-state visibility.
- `/plushies` — SQL-backed approved-only catalogue and local search.
- `/basket/plushies` — protected anonymous list retained for 90 days.
- `/go/{shop}/{product}` — approved-only tracked redirect with an auditable
  outbound-click record.

Development catalogue automation is enabled with a 24-hour refresh, 15-minute
poll, 60-minute failure retry and two-hour stale-job recovery. It reads the SQL
job history on startup, so restarting the app does not trigger duplicate API
work. Production automation remains disabled until deployment configuration is
reviewed.

## Next milestone

Complete the curated public catalogue slice: improve discovery quality,
approve a safe initial set through the admin, add an approved-only product
detail page, and implement category/price/popularity filters. Then expand the
scheduled ingestion beyond one search page and add link-expiry refresh.
