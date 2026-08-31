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
| Website | Create a new `wonderaisle` site mapped to `\wonderaisle`; do not reuse an existing site root |
| Application pool | Create a new 1024 MB .NET Core pool from the plan's unallocated RAM and assign only Wonder Aisle |
| Secrets | Configure on that new pool through Environment Variables; never put Wonder Aisle secrets on either existing shared pool |
| Database | Create a new 1000 MB MSSQL 2022 database with suffix `wonderaisle`; do not reuse any Hydra database |
| Scheduled URL | Three slots are available and unused; add `/health/wake` only after the first release is stable |
| Deployment | The website surface provides VSDeploy/Web Deploy and FTP; prefer Web Deploy after downloading target-specific settings privately |
| Domain | `wonderaisle.co.uk` was shown available but has not been registered or attached |

The plan currently has four existing websites split across two pools. The new
pool removes all application siblings from Wonder Aisle's runtime failure and
secret boundary. Account-wide pre/post checks should still cover the public
sites `circlesofstone.co.uk`, `iloveplushies.co.uk`, `ilovefnaf.co.uk`,
`ilovewitchcraft.co.uk`, `ilovefitness.co.uk`, `animesuperstore.co.uk` and
`propertiesandhomes.co.uk`.

## Information required before the first deployment

- registered canonical domain and HTTPS certificate status;
- the assigned temporary hostname and generated application-pool name after
  the approved resource-creation step;
- every sibling website in the same hosting account;
- production SQL Server connection details for the new database and a tested
  backup/restore route;
- a persistent private directory outside `wwwroot` for Data Protection keys;
- Web Deploy or FTP deployment details, supplied outside source control; and
- exact environment-variable names and a post-bootstrap plan for removing the
  temporary owner password.

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
`/health/wake`. It performs a read-only SQL readiness check; application
startup wakes the background workers, whose persisted due-state prevents a
request from directly triggering duplicate work.

## Rollback

Keep the previous verified site ZIP and database backup. For an application
rollback, use target-scoped `app_offline.htm`, restore the previous site files,
remove the offline file and rerun target plus sibling checks. Do not recycle the
shared pool unless recovering from a confirmed pool-wide failure.

Prefer forward-compatible, additive migrations. Do not automatically reverse a
database migration after the new application has written data. Restore the
database backup only after confirming the effect on all production data and
with an explicit decision to discard post-backup writes.
