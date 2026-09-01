# Production release runbook

This runbook targets SmarterASP.NET and protects the owner's existing sites.
It prepares and validates releases; it does not authorize domain registration,
resource creation, a live upload, DNS change or pool recycle.

SmarterASP's public documentation currently lists .NET 10 in framework-
dependent mode, which matches this publish profile, and its hosting page lists
SQL Server 2025 through older supported versions. The scheduled URL feature and
dedicated application pools are documented for Premium plans and above and
were confirmed on this specific account on 31 August 2026.

## Verified account design

The signed-in, read-only control-panel audit established this deployment
shape:

| Resource | Verified state / first-release choice |
|---|---|
| Hosting plan | Existing EU plan, .NET Core 10 supported |
| Website | Provisioned as `wonderaisle`, mapped to `\wonderaisle`, with temporary hostname `hydraadmin-001-site5.gtempurl.com` |
| Application pool | Provisioned as dedicated 1024 MB .NET Core pool `hydraadmin-001E96`; verified to contain only the `wonderaisle` site |
| Secrets | Pool-scoped Environment Variables on `hydraadmin-001E96`; the production connection string is configured there and never in source control or a shared pool |
| Database | Provisioned as the isolated 1000 MB MSSQL 2022 database `db_a34d03_wonderaisle` |
| Scheduled URL | One of three slots calls the protected `/health/wake` endpoint every 15 minutes; two slots remain unused |
| Deployment | Current release `0ad6ba8` was deployed through the target-scoped File Manager because the downloaded publish profile omitted its password and resetting the account-wide Web Deploy password could disrupt other sites |
| Domain | `wonderaisle.co.uk` is registered, attached and resolving through `ns1.site4now.net`, `ns2.site4now.net` and `ns3.site4now.net`; apex and `www` resolve to the hosting service |
| TLS | Managed Let's Encrypt TLS is active for `wonderaisle.co.uk`; TLS 1.3, HTTP-to-HTTPS redirection and a 30-day HSTS header were verified on 1 September 2026 |

The plan currently has four existing websites split across two pools. The new
pool removes all application siblings from Wonder Aisle's runtime failure and
secret boundary. Account-wide pre/post checks should still cover the public
sites `circlesofstone.co.uk`, `iloveplushies.co.uk`, `ilovefnaf.co.uk`,
`ilovewitchcraft.co.uk`, `ilovefitness.co.uk`, `animesuperstore.co.uk` and
`propertiesandhomes.co.uk`.

## Information required before public launch

- an AliExpress production App Secret supplied through the dedicated pool;
- bootstrap administrator credentials supplied through the dedicated pool and
  removed after the first successful owner login; and
- final production catalogue/API checks after those protected values are set.

Provider references:

- [supported .NET versions](https://www.smarterasp.net/support/kb/a1986/supported-versions-of-_net-core.aspx)
- [scheduled URL tasks](https://www.smarterasp.net/support/kb/a2018/set-schedule-tasks-on-your-own-purpose_.aspx)
- [dedicated application pools](https://www.smarterasp.net/support/kb/a2247/why-do-you-need-dedicated-pool-per-site.aspx)
- [hosting and SQL Server versions](https://www.smarterasp.net/asp.net_hosting)
- [pool-scoped environment variables](https://www.smarterasp.net/support/kb/a2437/how-to-set-environment-variable-for-your-account.aspx)

## Required protected configuration

Supply these as environment variables on the dedicated Wonder Aisle pool.
SmarterASP variables are pool-scoped and visible to every site assigned to that
pool, which is why no existing site may share it. Never put the values in the
repository, publish archive or command history.

| Setting | First release value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__AffiliateSuperstore` | production SQL connection string |
| `AliExpress__AppSecret` | rotated production App Secret |
| `AdminAuthentication__BootstrapUsername` | initial owner name |
| `AdminAuthentication__BootstrapPassword` | unique strong initial password |
| `Hosting__DataProtectionKeysPath` | persistent private directory outside `wwwroot` and preferably outside the replaceable site root |
| `AllowedHosts` | canonical hostname and any temporary verification hostname, separated by semicolons |
| `Superstore__Shops__0__Hostnames__2` | exact temporary verification hostname until the canonical domain is attached |
| `CatalogueAutomation__Enabled` | `false` for first release |
| `CatalogueAutomation__WakeToken` | unique high-entropy secret required by the scheduled wake URL; never commit or log it |
| `OrderReconciliation__Enabled` | `false` for first release |
| `Seo__IndexingEnabled` | `false` for first release |
| `AliExpressS2s__Enabled` | `false` until the callback verification checklist passes |

Both admin bootstrap settings are required together. Startup creates the role
and owner idempotently and never resets an existing password. Remove the
bootstrap password from host configuration after the first successful owner
login.

The Data Protection key directory is mandatory in Production. Losing it signs
out administrators and invalidates saved-list cookies. It must be writable by
the application identity, excluded from downloads and logs, retained across
deployments, and covered by backup policy.

The provider denied the application identity access to the account-root
directory originally prepared for keys. The deployed site therefore uses
`App_Data\keys`, which is inside the site content root but outside the public
`wwwroot`. The directory is writable by the isolated Wonder Aisle pool,
persists across file-manager releases and contains the generated key ring.

## First production release record

Release `c46789e` was built from a clean detached worktree on 31 August 2026.
All 90 tests passed and both the project and generated hosting configuration
validated as OutOfProcess. The database backup was verified at
`\db\db_a34d03_wonderaisle_8_31_2026_5.bak` before applying the migration.

SmarterASP's SQL Studio parser rejected EF's dynamic `EXEC` wrappers and large
multi-statement batches, so the same idempotent statements were applied in
small static batches. `__EFMigrationsHistory` was then verified to contain all
seven expected migrations, and the object explorer reported 18 tables.

The deployed application returns `200` for `/`, `/plushies`, `/health/live`
and `/health/ready` on `https://hydraadmin-001-site5.gtempurl.com`. The
readiness response confirms the production SQL connection. Temporary startup
logging, both uploaded ZIP files and the provider `default.asp` placeholder
were removed after verification. Custom-domain HTTP reaches the application
and redirects to HTTPS; the missing custom-domain certificate prevents that
redirect target from completing TLS.

## Second production release record

Release `49e6304` was deployed on 31 August 2026 from a clean, validated
release bundle. All 116 tests passed and the published `web.config` was
verified as OutOfProcess. A fresh isolated database backup completed
successfully before the six pending migrations were applied in provider-safe
static batches. Production now reports all 13 migrations and 26 tables.

The release corrected production shop synchronisation, so startup created the
configured `plushies` shop. A temporary owner bootstrap was used once to create
the `aaronsmithmsc` administrator; both bootstrap environment variables were
removed immediately afterwards. The AliExpress App Secret remains in the
dedicated pool and was never placed in the release archive or repository.

A single controlled automation cycle completed catalogue discovery, product
enrichment, affiliate-link refresh and image/identity processing successfully
on the first attempt. It imported 188 products into review. Eight distinct
products with active affiliate links and clean automated checks were given
original, versioned editorial copy and approved as the public starter range.
Catalogue automation was then returned to `false` and the isolated pool was
restarted.

`/health/live`, `/health/ready`, `/`, `/plushies` and a representative product
detail return HTTP 200. The public shop reports eight results and the outbound
route returns an AliExpress redirect. All neighbouring production sites also
returned HTTP 200 after the release. The temporary maintenance marker and
uploaded release ZIP were removed after verification.

Administrator cookies intentionally remain Secure in Production. Managed TLS
now allows the production admin session to be retained without weakening the
cookie policy.

## Third production release record

Release `fb68023` was deployed on 1 September 2026 from the validated
`20260901-085504-fb68023` bundle. All 119 tests passed, and both the project and
published `web.config` were verified as OutOfProcess. Before any schema or site
change, SmarterASP completed backup
`\db\db_a34d03_wonderaisle_9_1_2026_2.bak` successfully.

Migration `20260901084943_AddProductIdentityGoldLabels` was applied as a
provider-safe static batch. Production now reports 14 EF migrations and 27
tables. `ProductIdentityGoldLabels` and its three review indexes are present,
and the table started with zero rows as intended.

The site-root archive was extracted into `\wonderaisle` while
`app_offline.htm` held the application offline. `App_Data` and its persistent
Data Protection key ring were excluded from the archive and remained present.
The maintenance marker and uploaded archive were removed after the provider
reported successful decompression; no application-pool recycle was needed.

Both health endpoints, the home page, `/plushies`, a representative product
detail, `/robots.txt` and `/sitemap.xml` return HTTP 200. The catalogue still
contains eight public products, its representative outbound route returns an
AliExpress redirect, and protected identity/database admin routes redirect to
the login page. All active neighbouring sites checked healthy. The legacy
`ilovefitness.co.uk` hostname now redirects to an external expired-domain page
and returns HTTP 404 after redirects; it is not hosted in the Wonder Aisle pool
and was unaffected by this release.

## Fourth production release and indexing record

Release `b48f3b8` was deployed on 1 September 2026 from the clean detached
`20260901-094016-b48f3b8` bundle. All 119 tests passed, and the project plus
published `web.config` again validated as OutOfProcess. The release has no new
database migration. SmarterASP backup queue item `3744188` completed
successfully before the application files changed.

Twelve clear, owner-authorized first-review duplicate examples were recorded in
the identity gold set as immutable tuning labels. They did not accept canonical
membership or change the public catalogue; the required second-reviewer and
adjudication work and 500-label target remain open. Four distinct,
quality-clear products were then given original editorial titles and
descriptions and approved, taking the
production readiness gate from 8/12 to 12/12.

The target-root archive was extracted into `\wonderaisle` while
`app_offline.htm` held only Wonder Aisle offline. `App_Data`, the persistent
Data Protection key ring and every neighbouring site remained untouched. The
maintenance marker and uploaded ZIP were removed after successful extraction.
The release corrected the admin percentage and editorial-version rendering
without changing the schema.

After post-release health, content, canonical, structured-data and affiliate-
redirect checks passed, `Seo__IndexingEnabled` was changed to `true` on the
dedicated `hydraadmin-001E96` pool. `/plushies` and all 12 product pages now
emit `index,follow`; filtered catalogue URLs and the intentionally thin
umbrella home emit `noindex,follow`. The sitemap contains exactly the shop plus
12 unique product URLs. `robots.txt` allows the public catalogue while blocking
admin, basket, outbound redirect, health and error routes.

The home page, shop, both health endpoints, all four new outbound affiliate
routes and the authenticated admin were rechecked after the pool configuration
reload. All active neighbouring domains returned HTTP 200. No account-wide or
shared-pool recycle was performed; `ilovefitness.co.uk` remains excluded for
the previously documented external expired-domain behaviour.

## Fifth production release and automation record

Release `0ad6ba8` was deployed on 1 September 2026 from the validated
`20260901-102101-0ad6ba8` bundle after rebasing the automation hardening onto
the Wonder Aisle homepage and favicon work. All 122 tests passed, the project
and published hosting configuration validated as OutOfProcess, and the release
contains no schema change. A fresh isolated production database backup
completed successfully before the target files changed.

The corrected target-root archive was extracted into `\wonderaisle` while a
target-scoped `app_offline.htm` held only Wonder Aisle offline. `App_Data` and
the persistent Data Protection key ring remained present. The live application
binary and health behaviour were verified after extraction; no application-
pool recycle was used. The homepage, shop, representative product, SEO files,
both health endpoints and every active neighbouring domain returned HTTP 200.

Production catalogue automation is enabled on the dedicated pool. A unique
256-bit wake secret is stored only in SmarterASP environment configuration,
and one provider scheduled task calls the protected wake endpoint every 15
minutes. Requests without the key return HTTP 401. Order reconciliation, S2S
callbacks and automatic publication remain disabled.

The first supervised cycle ran all 12 configured discovery searches and grew
the manual review queue from 188 to 190 candidates without publishing any of
them. A product-detail refresh then checked all 12 published products, wrote 12
fresh observations and 74 media records, with no missing, suspected-unavailable
or unavailable products. The automation dashboard reported 12 fresh published
products, zero failed runs, retries, active or recoverable leases and dead
letters, and no operational alerts. The public catalogue remained at exactly
12 human-approved products.

## Sixth production release and performance record

Release `652eb04` was deployed on 1 September 2026 from the clean detached
`20260901-114138-652eb04` bundle. All 124 tests passed, and both the project
and published `web.config` validated as OutOfProcess. SmarterASP backup queue
item `3744565` completed successfully before the additive production migration
or site files changed.

Migration `20260901113234_AddProductImpressionAggregates` was applied to the
isolated `db_a34d03_wonderaisle` database in provider-safe static batches.
Production now reports 15 EF migrations and 28 tables. The new
`ProductImpressions` table has all eight expected columns, its primary key,
two foreign keys and both reporting indexes.

The verified target-root archive was extracted into `\wonderaisle` while a
target-scoped `app_offline.htm` held only Wonder Aisle offline. The archive
excluded `App_Data`, so the production Data Protection key ring remained
untouched. Provider decompression completed successfully, after which the
maintenance marker and uploaded archive were removed. No application-pool or
account-wide restart was performed.

Both health endpoints, the home page, `/plushies`, a representative product,
`/robots.txt` and `/sitemap.xml` return HTTP 200. The anonymous performance
route redirects to the admin login, while the authenticated dashboard renders
the selected attribution window, impressions, outbound clicks, CTR, order and
commission summaries, placement channels and product-level performance. A
live antiforgery-protected product-card impression was accepted by
`/analytics/impressions` and verified in SQL.

The protected scheduler wake credential was rotated in the dedicated
`hydraadmin-001E96` pool and the single scheduled URL was replaced with a new
15-minute task. The replacement credential returns HTTP 200 from the bounded
wake check and an invalid credential returns HTTP 401. All active neighbouring
sites returned HTTP 200 after the release; `ilovefitness.co.uk` remains
excluded for its previously documented external expired-domain behaviour.

## Canonical HTTPS verification

On 1 September 2026, `https://wonderaisle.co.uk/`, `/plushies`, both health
endpoints, `/robots.txt`, `/sitemap.xml` and `/admin/login` returned HTTP 200.
Plain HTTP returns a 307 redirect to the HTTPS origin. The certificate subject
is `wonderaisle.co.uk`, the issuer is Let's Encrypt, TLS 1.3 negotiated, and the
certificate is valid through 30 November 2026. HSTS is present with a 30-day
maximum age. The public shop now shows the intended 12 reviewed products.

The release-validator pass returned HTTP 200 for the active neighbouring
domains as well as Wonder Aisle. The legacy `ilovefitness.co.uk` hostname now
redirects to an external expired-domain service and is excluded from the set of
hosted application health checks.

## Seventh production release and webmaster record

Release `499fb5a` was deployed on 1 September 2026 from the clean detached
`20260901-130423-499fb5a` bundle. All 134 tests passed, and both the project
and published `web.config` validated as OutOfProcess. This release contains no
database migration and did not change production data.

The verified target-root archive was extracted into `\wonderaisle` while a
target-scoped `app_offline.htm` held only Wonder Aisle offline. `App_Data` and
its persistent Data Protection key ring remained present. The maintenance
marker and uploaded archive were removed after successful extraction. No
application-pool or account-wide restart was performed.

The release adds optional GA4 analytics using basic consent mode. A fresh live
visit displays equal Accept and Reject choices while loading zero Google
scripts. Rejecting analytics leaves the site functional, loads no Google
script, and the footer retains a Cookie settings control. The privacy and
cookie notice documents the optional processing and six-month limits.

Both health endpoints, the home page, `/plushies`, a representative product,
`/robots.txt` and `/sitemap.xml` return HTTP 200. `/admin/api-test` redirects
anonymous visitors to the admin login. All active neighbouring sites returned
HTTP 200 after following their canonical redirects.

Google Search Console domain ownership for `wonderaisle.co.uk` was verified
through the authoritative DNS provider. The sitemap was accepted with 13 URLs
discovered. The verification TXT record remains in DNS and is intentionally
excluded from source control.

## Eighth production release and analytics funnel record

Commit `21a64f5` was deployed on 1 September 2026 from the clean detached
`20260901-135019-21a64f5` bundle. All 134 tests passed, and both the project
and published `web.config` validated as OutOfProcess. This release contains no
database migration and made no intentional production-data change.

The verified target-root archive was extracted into `\wonderaisle` while a
target-scoped `app_offline.htm` held only Wonder Aisle offline. The archive
excluded `App_Data`; the directory and persistent Data Protection key ring
remained present. Provider decompression completed successfully, after which
the maintenance marker and this release's uploaded archive were removed. No
application-pool or account-wide restart was performed.

The release adds consent-gated GA4 storefront funnel events for catalogue list
views and selections, product views, saved-list changes, catalogue searches
and filters, and outbound affiliate handoffs. Event payloads use catalogue
identifiers, categories, prices, currency, shop and placement metadata without
product titles, seller names, click identifiers or typed search phrases.
`page_location` is reduced to the canonical query-free URL. Advertising
consent and Google Signals remain disabled.

A live rejected-consent visit loaded zero Google scripts. Accepting analytics
loaded one Google tag script, and withdrawing consent removed it again. Google
Analytics had not yet populated its Recent events table immediately after the
release and stated that first events can take up to 24 hours to appear;
`affiliate_handoff` therefore remains to be starred as a key event once it is
available, rather than creating a duplicate event configuration.

Both health endpoints, the home page, `/plushies`, a representative product,
`/basket/plushies`, `/robots.txt`, `/sitemap.xml` and the updated privacy notice
returned HTTP 200. `/admin/api-test` redirected anonymous visitors to
`/admin/login`. The live catalogue, product and JavaScript assets contain the
expected funnel instrumentation. `circlesofstone.co.uk`,
`www.iloveplushies.co.uk`, `www.ilovefnaf.co.uk`,
`www.ilovewitchcraft.co.uk`, `www.animesuperstore.co.uk` and
`propertiesandhomes.co.uk` all returned HTTP 200 after following canonical
redirects.

## Ninth production release and editorial collections record

Commit `38c6630` was deployed on 1 September 2026 from the clean detached
`20260901-144046-38c6630` bundle. All 140 tests in that committed revision
passed, and both the project and published `web.config` validated as
OutOfProcess.

A successful production SQL backup was recorded as provider queue item
`3745113` before the additive `20260901142700_AddEditorialCollections`
migration was applied. The migration created the `Collections` and
`CollectionProducts` tables, their foreign keys and four supporting indexes,
then advanced `__EFMigrationsHistory` to 16 entries. It did not alter existing
catalogue rows or publish collection content automatically.

The verified target-root archive was extracted into `\wonderaisle` while a
target-scoped `app_offline.htm` held only Wonder Aisle offline. The archive
excluded `App_Data`; its persistent Data Protection key ring remained intact.
The maintenance marker was renamed out of its active filename after extraction,
and this release's 29 MB uploaded archive was deleted after explicit
confirmation. No application-pool or account-wide restart was performed.

Both health endpoints, the home page, `/plushies`, a representative product,
`/robots.txt` and `/sitemap.xml` returned HTTP 200. `/admin/collections`
redirected anonymous visitors to `/admin/login`. `circlesofstone.co.uk`,
`www.iloveplushies.co.uk`, `www.ilovefnaf.co.uk`,
`www.ilovewitchcraft.co.uk`, `www.animesuperstore.co.uk` and
`propertiesandhomes.co.uk` all returned HTTP 200 after following canonical
redirects.

## Build a release bundle

From the repository root:

```powershell
./scripts/New-SmarterAspRelease.ps1
```

The command restores, runs the complete test suite, publishes a framework-
dependent site, creates an idempotent SQL migration script, checks the project
and generated `web.config`, writes SHA-256 hashes and creates a timestamped ZIP
under `artifacts/releases`.

Every acceptable bundle must have:

- project and generated `web.config` hosting models set to `OutOfProcess`;
- no `app_offline.htm` in the archive;
- no `appsettings.Development.json` in the archive;
- no credentials or production connection strings; and
- a migration script and hash manifest alongside the `site` directory.

To recheck an extracted bundle and capture a pre-release baseline for sibling
sites:

```powershell
./scripts/Test-SmarterAspRelease.ps1 `
  -ProjectPath ./src/AffiliateSuperstore.Web/AffiliateSuperstore.Web.csproj `
  -PublishPath ./artifacts/releases/<release>/site `
  -Urls https://existing-site-one.example/,https://existing-site-two.example/
```

## First database migration

1. Take and verify a production SQL backup.
2. Confirm the connection targets the new Wonder Aisle database, not a
   database used by an existing site.
3. Review `database-migration.sql` and run it using SmarterASP's SQL tooling.
4. Confirm `__EFMigrationsHistory` contains the expected migrations.
5. Do not enable automation, S2S or indexing yet.

Production startup deliberately does not apply migrations. The generated SQL
is idempotent, but a backup and target verification are still mandatory.

## Pool-safe deployment

1. Record HTTP results for the target (if it exists) and every sibling site.
2. Confirm `.NET Core Mode` is OutOfProcess. On a shared pool, all ASP.NET Core
   sites must be compatible with OutOfProcess hosting.
3. Upload only to the target site's mapped directory.
4. Prefer Web Deploy with app-offline support. For FTP or file-manager uploads,
   place `app_offline.htm` only in the target root, copy the staged `site`
   contents, and remove `app_offline.htm` in guaranteed cleanup.
5. Do not restart or recycle the shared pool as a normal deployment step.

A live upload or control-panel change requires an explicit confirmation after
the target path, sibling list and backup have been rechecked.

## Verification

Check in this order:

1. `/health/live` returns `200` and `status: healthy`.
2. `/health/ready` returns `200`; `503` means SQL is unavailable.
3. `/robots.txt` blocks indexing.
4. `/plushies` and one product page render over HTTPS.
5. `/admin/api-test` redirects to `/admin/login` anonymously.
6. the bootstrap owner can sign in and the admin database page shows no pending
   migrations;
7. the AliExpress categories API test succeeds without exposing its signature;
8. every sibling URL returns its pre-release status; and
9. no `app_offline.htm` remains in the target root.

Only after this baseline is stable should the scheduled task call
`/health/wake?key=<CatalogueAutomation__WakeToken>`. Missing or incorrect keys
return HTTP 401. A correctly authenticated request performs a read-only SQL
readiness check and writes only to a bounded in-memory wake signal; it accepts
no catalogue or job parameters. The worker then plans due work through unique
persisted idempotency keys, so the request cannot directly create or execute
duplicate work. Treat the scheduled URL as a secret because some provider logs
and control panels retain full query strings.

## Rollback

Keep the previous verified site ZIP and database backup. For an application
rollback, use target-scoped `app_offline.htm`, restore the previous site files,
remove the offline file and rerun target plus sibling checks. Do not recycle the
shared pool unless recovering from a confirmed pool-wide failure.

Prefer forward-compatible, additive migrations. Do not automatically reverse a
database migration after the new application has written data. Restore the
database backup only after confirming the effect on all production data and
with an explicit decision to discard post-backup writes.
