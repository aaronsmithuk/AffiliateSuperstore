# Production release runbook

This runbook targets SmarterASP.NET and protects other sites that may share the
same IIS application pool. It prepares and validates releases; it does not
authorize a live upload, DNS change or pool recycle.

SmarterASP's public documentation currently lists .NET 10 in framework-
dependent mode, which matches this publish profile, and its hosting page lists
SQL Server 2025 through older supported versions. The scheduled URL feature and
dedicated application pools are documented for Premium plans and above; the
features must still be confirmed on this specific account before release.

## Information required before the first deployment

- final canonical domain and HTTPS certificate status;
- the exact SmarterASP website and mapped target directory;
- every sibling website in the same hosting account;
- whether the target has a dedicated pool (assume shared until confirmed);
- confirmation that the account supports the .NET 10 Hosting Bundle;
- production SQL Server connection details and a tested backup/restore route;
- a persistent private directory outside `wwwroot` for Data Protection keys;
- Web Deploy or FTP deployment details, supplied outside source control; and
- scheduled-URL entitlement if `/health/wake` will be requested every 15
  minutes.

Provider references:

- [supported .NET versions](https://www.smarterasp.net/support/kb/a1986/supported-versions-of-_net-core.aspx)
- [scheduled URL tasks](https://www.smarterasp.net/support/kb/a2018/set-schedule-tasks-on-your-own-purpose_.aspx)
- [dedicated application pools](https://www.smarterasp.net/support/kb/a2247/why-do-you-need-dedicated-pool-per-site.aspx)
- [hosting and SQL Server versions](https://www.smarterasp.net/asp.net_hosting)

## Required protected configuration

Supply these through the hosting control panel or process environment. Never
put values in the repository, publish archive or command history.

| Setting | First release value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__AffiliateSuperstore` | production SQL connection string |
| `AliExpress__AppSecret` | rotated production App Secret |
| `AdminAuthentication__BootstrapUsername` | initial owner name |
| `AdminAuthentication__BootstrapPassword` | unique strong initial password |
| `Hosting__DataProtectionKeysPath` | persistent private directory outside `wwwroot` and preferably outside the replaceable site root |
| `AllowedHosts` | canonical hostname and any temporary verification hostname, separated by semicolons |
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
2. Confirm the connection targets the new Affiliate Superstore database, not a
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
