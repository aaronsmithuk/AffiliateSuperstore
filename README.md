# Affiliate Superstore

A greenfield ASP.NET Core application for UK-focused, themed affiliate shops
backed by the AliExpress Affiliate API.

The persistent delivery roadmap and current decisions are in
[`docs/PROJECT-PLAN.md`](docs/PROJECT-PLAN.md).

The public application uses Razor Pages. Interactive operational tooling lives
in a Blazor Server admin area, including the API workbench, SQL status,
catalogue review and automation schedule.

## Current foundation

- .NET 10
- Configurable Razor Pages shop shell at `/plushies`
- Blazor admin API workbench at `/admin/api-test`
- Blazor database, catalogue and automation pages
- Reusable HMAC-SHA256 AliExpress client covering all 16 published methods
- Typed request models, response normalisation and permission visibility
- Domain/path-aware shop resolution, theming and SEO metadata
- Bootstrap-free custom design tokens with controlled per-shop theme profiles
- Dedicated-or-fallback Tracking IDs plus campaign, placement and opaque click attribution
- EF Core SQL Server model and migrations for shops, products, immutable
  snapshots, review state, links, clicks, jobs and affiliate orders
- Live catalogue ingestion through the API with tracked-link generation
- Restart-safe multi-query discovery with normal refresh, failure retry,
  stale-job recovery and audited proactive affiliate-link renewal
- Mandatory persisted product-quality checks with a human approval gate
- Guarded editorial curation for public titles, descriptions, featuring and
  display order; source-listing risks cannot be hidden by rewritten copy
- Approved-only public catalogue, product detail and local category/price/sort
  controls
- Canonical URLs, safe robots rules, quality-gated XML sitemap and Product /
  ItemList structured data, with production indexing disabled by default
- Auditable `/go/{shop}/{product}` redirect
- Protected 90-day anonymous shopping list with item count and one-by-one
  AliExpress hand-off
- GB shipping market, GBP and English defaults
- No admin authentication yet; the admin must remain local until it is added

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
`/admin/automation`. The public shop is at `/plushies`, and its anonymous saved
list is at `/basket/plushies`. Public pages remain `noindex` until the first
safe catalogue is curated.

Development uses SQL Server LocalDB database `AffiliateSuperstoreLocal` and
applies checked-in migrations on application startup. A production connection
string must be supplied by the host; production does not auto-migrate.

To run a catalogue job from the command line instead of the admin:

```powershell
dotnet run --project ./tools/AffiliateSuperstore.CatalogueIngest -- plushies "plush toy" 1 20
```

Development automation is enabled and normally refreshes the first configured
page every 24 hours. It checks persisted job history every 15 minutes, so an
application restart does not duplicate a recent run. Production automation is
off by default.

## Verify

```powershell
dotnet test ./AffiliateSuperstore.slnx
```

The current suite covers request signing and normalisation, shop resolution,
database constraints and configuration sync, ingestion success/failure and its
quality gate, automation timing, approved-only redirect behaviour and anonymous
list state.

After configuring User Secrets, the live smoke test exercises categories, a
five-item UK plush search, product details, tracked-link generation, featured
promotions, promotion products and recent orders without printing the App
Secret or request signature:

```powershell
dotnet run --project ./tools/AffiliateSuperstore.ApiSmokeTest
```

Research and captured AliExpress source material is indexed in
[`docs/aliexpress/README.md`](docs/aliexpress/README.md).

The visual exploration contract and parallel-design-task boundaries are in
[`docs/DESIGN-SYSTEM-BRIEF.md`](docs/DESIGN-SYSTEM-BRIEF.md).
