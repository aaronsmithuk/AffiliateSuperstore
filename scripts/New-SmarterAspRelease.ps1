[CmdletBinding()]
param(
    [string] $OutputRoot = 'artifacts/releases',
    [switch] $SkipTests,
    [switch] $AllowDirty
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $repositoryRoot 'AffiliateSuperstore.slnx'
$webProject = Join-Path $repositoryRoot 'src/AffiliateSuperstore.Web/AffiliateSuperstore.Web.csproj'
$persistenceProject = Join-Path $repositoryRoot 'src/AffiliateSuperstore.Persistence/AffiliateSuperstore.Persistence.csproj'
$outputBase = if ([IO.Path]::IsPathRooted($OutputRoot)) {
    [IO.Path]::GetFullPath($OutputRoot)
} else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputRoot))
}

$revision = (git -C $repositoryRoot rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to determine the Git revision.' }
$dirty = [bool] (git -C $repositoryRoot status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect the Git worktree.' }
if ($dirty -and -not $AllowDirty) {
    throw 'The Git worktree is not clean. Commit the release or pass -AllowDirty for a non-production validation bundle.'
}
$revisionLabel = if ($dirty) { "$revision-dirty" } else { $revision }
$releaseName = '{0:yyyyMMdd-HHmmss}-{1}' -f [DateTimeOffset]::UtcNow, $revisionLabel
$releaseRoot = Join-Path $outputBase $releaseName
$publishPath = Join-Path $releaseRoot 'site'
$migrationPath = Join-Path $releaseRoot 'database-migration.sql'
$manifestPath = Join-Path $releaseRoot 'sha256-manifest.txt'
$archivePath = "$releaseRoot.zip"

if (Test-Path -LiteralPath $releaseRoot) {
    throw "Release directory already exists: $releaseRoot"
}
New-Item -ItemType Directory -Path $publishPath -Force | Out-Null

dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }
if (-not $SkipTests) {
    dotnet test $solution --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
}

dotnet publish $webProject --configuration Release --no-restore --output $publishPath -p:PublishProfile=SmarterAsp
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

dotnet ef migrations script --idempotent --project $persistenceProject --configuration Release --output $migrationPath
if ($LASTEXITCODE -ne 0) { throw 'Generating the idempotent database migration script failed.' }

& (Join-Path $PSScriptRoot 'Test-SmarterAspRelease.ps1') -ProjectPath $webProject -PublishPath $publishPath
if ($LASTEXITCODE -ne 0) { throw 'SmarterASP release validation failed.' }

$filesToHash = Get-ChildItem -LiteralPath $releaseRoot -Recurse -File | Sort-Object FullName
$manifestLines = foreach ($file in $filesToHash) {
    $relativePath = [IO.Path]::GetRelativePath($releaseRoot, $file.FullName).Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $relativePath"
}
[IO.File]::WriteAllLines($manifestPath, $manifestLines)

Compress-Archive -Path (Join-Path $releaseRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal

[pscustomobject]@{
    Revision = $revisionLabel
    ReleaseDirectory = $releaseRoot
    SiteDirectory = $publishPath
    MigrationScript = $migrationPath
    Manifest = $manifestPath
    Archive = $archivePath
} | ConvertTo-Json -Depth 3
