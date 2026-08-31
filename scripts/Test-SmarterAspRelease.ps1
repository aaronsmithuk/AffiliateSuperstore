[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ProjectPath,
    [Parameter(Mandatory)] [string] $PublishPath,
    [string[]] $Urls = @(),
    [int] $TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$publish = (Resolve-Path -LiteralPath $PublishPath).Path

[xml] $projectXml = Get-Content -Raw -LiteralPath $project
$models = @(
    $projectXml.Project.PropertyGroup.AspNetCoreHostingModel |
        ForEach-Object { [string] $_ } |
        Where-Object { $_ }
)
if ($models.Count -eq 0) {
    $failures.Add('The web project does not explicitly set AspNetCoreHostingModel.')
} elseif ($models | Where-Object { $_ -notmatch '^(?i:OutOfProcess)$' }) {
    $failures.Add("The project contains a non-OutOfProcess hosting model: $($models -join ', ').")
}

$webConfigPath = Join-Path $publish 'web.config'
$publishedModel = $null
if (-not (Test-Path -LiteralPath $webConfigPath -PathType Leaf)) {
    $failures.Add("The published root has no web.config: $webConfigPath")
} else {
    [xml] $webConfig = Get-Content -Raw -LiteralPath $webConfigPath
    $aspNetCore = $webConfig.configuration.location.'system.webServer'.aspNetCore
    if (-not $aspNetCore) { $aspNetCore = $webConfig.configuration.'system.webServer'.aspNetCore }
    $publishedModel = [string] $aspNetCore.hostingModel
    if ($publishedModel -notmatch '^(?i:OutOfProcess)$') {
        $failures.Add("The published hosting model is '$publishedModel', not OutOfProcess.")
    }
}

if (Test-Path -LiteralPath (Join-Path $publish 'app_offline.htm')) {
    $failures.Add('The publish output contains app_offline.htm.')
}
if (Test-Path -LiteralPath (Join-Path $publish 'appsettings.Development.json')) {
    $failures.Add('The publish output contains appsettings.Development.json.')
}

$health = foreach ($url in $Urls) {
    try {
        $response = Invoke-WebRequest -Uri $url -TimeoutSec $TimeoutSeconds -MaximumRedirection 5 -SkipHttpErrorCheck
        $healthy = $response.StatusCode -ge 200 -and $response.StatusCode -lt 400
        if (-not $healthy) { $warnings.Add("$url returned HTTP $($response.StatusCode).") }
        [pscustomobject]@{ Url = $url; StatusCode = [int] $response.StatusCode; Healthy = $healthy; Error = $null }
    } catch {
        $warnings.Add("$url failed: $($_.Exception.Message)")
        [pscustomobject]@{ Url = $url; StatusCode = $null; Healthy = $false; Error = $_.Exception.Message }
    }
}

$result = [pscustomobject]@{
    Project = $project
    PublishPath = $publish
    ProjectHostingModel = $models
    PublishedHostingModel = $publishedModel
    Health = @($health)
    Warnings = @($warnings)
    Errors = @($failures)
    Passed = $failures.Count -eq 0
}
$result | ConvertTo-Json -Depth 5
if (-not $result.Passed) { exit 1 }
