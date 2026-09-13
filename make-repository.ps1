param(
    [Parameter(Mandatory)][string]$PackageUrl,
    [Parameter(Mandatory)][string]$ProjectUrl,
    [string]$ManifestPath = (Join-Path $PSScriptRoot 'HudEditor.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot 'repo.json')
)
$ErrorActionPreference = 'Stop'
foreach ($taskValue in @($PackageUrl, $ProjectUrl)) {
    $taskUri = $null
    if (-not [Uri]::TryCreate($taskValue, [UriKind]::Absolute, [ref]$taskUri) -or $taskUri.Scheme -ne 'https' -or $taskUri.UserInfo -or $taskUri.IsLoopback) { throw 'Use a public HTTPS URL without embedded credentials.' }
}
$taskEntry = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($taskEntry.InternalName -ne 'HudEditor' -or $taskEntry.AssemblyVersion -ne '1.0.0.0') { throw 'Unexpected manifest' }
$taskEntry | Add-Member RepoUrl $ProjectUrl -Force
$taskEntry | Add-Member DownloadLinkInstall $PackageUrl -Force
$taskEntry | Add-Member DownloadLinkUpdate $PackageUrl -Force
$taskEntry | Add-Member IsHide $false -Force
$taskEntry | Add-Member IsTestingExclusive $false -Force
$taskEntry | Add-Member LastUpdate ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()) -Force
ConvertTo-Json -InputObject @($taskEntry) -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Catalog generated: $OutputPath. Publish only after checking anonymous download of both JSON and ZIP."
