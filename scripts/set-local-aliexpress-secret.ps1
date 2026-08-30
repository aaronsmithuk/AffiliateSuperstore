param()

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\src\AffiliateSuperstore.Web\AffiliateSuperstore.Web.csproj'

Write-Host 'This stores the AliExpress App Secret in .NET User Secrets for this Windows account.'
Write-Host 'It will not be written to appsettings.json or the repository.'

$secret = Read-Host 'AliExpress App Secret' -MaskInput

if ([string]::IsNullOrWhiteSpace($secret)) {
    throw 'No secret was entered.'
}

dotnet user-secrets set 'AliExpress:AppSecret' $secret --project $projectPath

Write-Host 'Local AliExpress secret configured. Restart the web application if it is running.'
