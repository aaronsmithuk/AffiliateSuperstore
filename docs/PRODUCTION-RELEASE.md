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
| Scheduled URL | Three slots are available and unused; add `/health/wake` only after the first release is stable |
| Deployment | Release `c46789e` was deployed through the target-scoped File Manager because the downloaded publish profile omitted its password and resetting the account-wide Web Deploy password could disrupt other sites |
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

## Canonical HTTPS verification

On 1 September 2026, `https://wonderaisle.co.uk/`, `/plushies`, both health
endpoints, `/robots.txt`, `/sitemap.xml` and `/admin/login` returned HTTP 200.
Plain HTTP returns a 307 redirect to the HTTPS origin. The certificate subject
is `wonderaisle.co.uk`, the issuer is Let's Encrypt, TLS 1.3 negotiated, and the
certificate is valid through 30 November 2026. HSTS is present with a 30-day
maximum age. The public shop still shows the intended eight reviewed products.

The final release-validator pass returned HTTP 200 for all seven neighbouring
domains as well as Wonder Aisle. An earlier raw redirect inspection briefly
surfaced an external expired-domain response for `ilovefitness.co.uk`, so that
legacy domain remains worth monitoring even though its final standardized check
was healthy.

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
`/health/wake`. It performs a read-only SQL readiness check and writes only to a
bounded in-memory wake signal; it accepts no job parameters. The worker then
plans due work through unique persisted idempotency keys, so the request cannot
directly create or execute duplicate work.

## Rollback

Keep the previous verified site ZIP and database backup. For an application
rollback, use target-scoped `app_offline.htm`, restore the previous site files,
remove the offline file and rerun target plus sibling checks. Do not recycle the
shared pool unless recovering from a confirmed pool-wide failure.

Prefer forward-compatible, additive migrations. Do not automatically reverse a
database migration after the new application has written data. Restore the
database backup only after confirming the effect on all production data and
with an explicit decision to discard post-backup writes.
