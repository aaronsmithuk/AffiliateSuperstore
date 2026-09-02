# Wonder Aisle

A greenfield ASP.NET Core application for UK-focused, themed affiliate shops
backed by the AliExpress Affiliate API.

The persistent delivery roadmap and current decisions are in
[`docs/PROJECT-PLAN.md`](docs/PROJECT-PLAN.md).

The standing publication rules for AI-assisted, affiliate and search-facing
content are in
[`docs/SEARCH-CONTENT-GOVERNANCE.md`](docs/SEARCH-CONTENT-GOVERNANCE.md).

The public application uses Razor Pages. Interactive operational tooling lives
in a Blazor Server admin area, including the API workbench, SQL status,
catalogue review and automation schedule.

## Current foundation

- .NET 10
- Configurable Razor Pages shop shell at `/plushies`
- Blazor admin API workbench at `/admin/api-test`
- Blazor database, catalogue, automation, affiliate-order and performance pages
- Reusable HMAC-SHA256 AliExpress client covering all 16 published methods
- Typed request models, response normalisation and permission visibility
- Domain/path-aware shop resolution, theming and SEO metadata
- Bootstrap-free custom design tokens with controlled per-shop theme profiles
- Dedicated-or-fallback Tracking IDs plus campaign, placement and opaque click attribution
- EF Core SQL Server model and migrations for shops, products, ordered image/
  video media, immutable snapshots, review state, links, clicks, jobs and
  affiliate orders
- Live catalogue ingestion through the API with tracked-link generation
- Provenance-aware Standard, hot-product and Smart Match ingestion adapters;
  Advanced automation remains independently gated per shop
- A guarded 12-request full-discovery plan and publication-readiness report,
  with a single-run lock and stop-on-failure behaviour
- Restart-safe multi-query discovery with normal refresh, failure retry,
  stale-job recovery and audited proactive affiliate-link renewal
- Mandatory persisted product-quality checks with a human approval gate
- Bounded exact-byte SHA-256 fingerprints for approved AliExpress image CDN
  hosts, retained as explainable identity-review evidence with retry state
- Versioned editorial curation for public titles, descriptions, featuring and
  display order, with source-claim validation, named provenance, field diffs,
  optimistic edit protection and restore-as-new-revision history
- Approved-only public catalogue, product detail and local category/price/sort
  controls
- Batched Standard API product-detail enrichment for approved products, with
  persisted galleries, optional listing video, SKU/EAN, aggregate feedback,
  sales, seller and freshness facts; individual AliExpress reviews are not
  exposed by the Affiliate API
- Canonical URLs, safe robots rules, quality-gated XML sitemap and Product /
  ItemList structured data, with production indexing disabled by default
- Auditable `/go/{shop}/{product}` redirect
- Restart-safe paid-order discovery across all four AliExpress lifecycle
  states, open-order refresh, click attribution, monthly recovery backfill and
  commission reporting
- Spreadsheet-safe development CSV export of the durable SQL order archive
- Privacy-minimised visible-impression aggregation and CTR reporting by shop,
  placement and product, alongside click-to-order and commission reporting
- Guarded, idempotent S2S paid-order inbox, disabled until its production HTTPS
  URL and fixed verification secret are configured
- Protected 90-day anonymous shopping list with item count and one-by-one
  AliExpress hand-off
- GB shipping market, GBP and English defaults
- Owner-only ASP.NET Core Identity authentication across every admin page and
  order export, with lockout protection and no public registration
- Pool-safe OutOfProcess SmarterASP publishing, persistent production cookie-
  key enforcement and public liveness/readiness/wake probes

## Configure the local App Secret

The AppKey and tracking ID are non-secret configuration in `appsettings.json`.
The App Secret must remain outside the repository.

From PowerShell at the repository root:

```powershell
./scripts/set-local-aliexpress-secret.ps1
```

The script prompts without echoing the value and stores it in .NET User
Secrets. Because a secret was shared in conversation during initial setup,
rotate it in the AliExpress App Console before deploying anywhere.

## Run locally

```powershell
dotnet run --project ./src/AffiliateSuperstore.Web
```

Open the URL printed by ASP.NET Core, followed by `/admin/api-test`. The
other current operations pages are `/admin/database`, `/admin/catalogue` and
`/admin/automation`, `/admin/orders` and `/admin/performance`. The public shop is at `/plushies`, and its anonymous saved
list is at `/basket/plushies`. The local catalogue now has a 12-product
quality-gated launch set; production pages remain `noindex` until the domain,
content and release review deliberately enables indexing.

Development uses SQL Server LocalDB database `AffiliateSuperstoreLocal` and
applies checked-in migrations on application startup. A production connection
string must be supplied by the host; production does not auto-migrate.

On the first local run, visit `/admin/setup` from the same machine and create
the owner account. The setup route is available only in Development, only over
a loopback connection and only while the user table is empty. After that,
`/admin/login` is the sole entry point; five failed attempts lock the account
for 15 minutes. There is no public registration flow.

For production, create the first owner through protected host configuration:

```text
AdminAuthentication__BootstrapUsername=<owner-name>
AdminAuthentication__BootstrapPassword=<strong-unique-password>
```

Both values are required together. Startup creates the owner and Administrator
role idempotently but never resets an existing password. Remove the bootstrap
password from hosting configuration after the first successful startup. Do not
put either credential in `appsettings.json`, source control or deployment
artifacts. Persist ASP.NET Core Data Protection keys on the production host so
admin and saved-list cookies remain valid across restarts.

To run a catalogue job from the command line instead of the admin:

```powershell
dotnet run --project ./tools/AffiliateSuperstore.CatalogueIngest -- plushies "plush toy" 1 20
```

Development automation is enabled and runs the configured discovery plan every
24 hours. The current plushies plan searches six controlled themes across two
pages (12 sequential API calls), stops on the first failure and cannot overlap
another plan in the same application process. It checks persisted job history
every 15 minutes, so an application restart does not duplicate a recent run.
Production automation is off by default. The external `/health/wake` endpoint
requires the configured `CatalogueAutomation:WakeToken`; it only signals a
bounded worker cycle and never accepts catalogue or job parameters.
After a due discovery run, approved linked products whose detail data is older
than 24 hours are refreshed in batches of up to 50 IDs. A forced detail refresh
is also available on `/admin/catalogue`; its result reports enriched products,
missing API results and stored media items.

Development also reconciles affiliate orders every 60 minutes. Its first run
queries the documented 180-day window; later runs use a 48-hour overlap,
refresh every locally open sub-order by ID and force another full 180-day scan
every 30 days. SQL is the authoritative long-term archive. A CSV backup can be
downloaded by an authenticated owner from the local-development Orders page;
it deliberately returns 404 outside Development. Production
order automation is off by default. S2S production setup is documented in
[`docs/S2S-SETUP.md`](docs/S2S-SETUP.md).

## Verify

```powershell
dotnet test ./AffiliateSuperstore.slnx
```

The current suite covers request signing and normalisation, shop resolution,
database constraints and configuration sync, ingestion success/failure and its
quality gate, batched product-detail/media enrichment, guarded discovery-plan
execution, publication readiness,
automation timing, deterministic product identity/version supersession,
reviewed canonical membership, approved-only redirect behaviour and anonymous list state,
cursor-based order reconciliation, S2S idempotency and click
attribution, archive export safety, visible-impression aggregation and CTR/
click/link performance reporting.

After configuring User Secrets, the live smoke test exercises categories, a
five-item UK plush search, product details, tracked-link generation, featured
promotions, promotion products and recent orders. When Advanced permission is
enabled it also verifies hot-product query/download, Smart Match and type-2 link
generation. Calls are paced and the App Secret and request signature are never
printed:

```powershell
dotnet run --project ./tools/AffiliateSuperstore.ApiSmokeTest
```

Research and captured AliExpress source material is indexed in
[`docs/aliexpress/README.md`](docs/aliexpress/README.md).
The verified Advanced integration and controlled activation sequence are in
[`docs/ADVANCED-API-ROLLOUT.md`](docs/ADVANCED-API-ROLLOUT.md).

The production configuration, release, sibling-site verification and rollback
procedure is in [`docs/PRODUCTION-RELEASE.md`](docs/PRODUCTION-RELEASE.md). Build
a checked release archive with `./scripts/New-SmarterAspRelease.ps1`.

The adopted umbrella-brand/domain decision and point-in-time checks are in
[`docs/DOMAIN-RECOMMENDATION.md`](docs/DOMAIN-RECOMMENDATION.md). Wonder Aisle
is now the canonical product identity; `wonderaisle.co.uk` is registered, mapped
and serving the deployed application while managed custom-domain TLS remains the
public-launch blocker.

The visual exploration contract and parallel-design-task boundaries are in
[`docs/DESIGN-SYSTEM-BRIEF.md`](docs/DESIGN-SYSTEM-BRIEF.md).
