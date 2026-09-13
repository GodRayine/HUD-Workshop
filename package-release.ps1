param(
    [string]$DotnetPath,
    [string]$DalamudPath
)
$ErrorActionPreference = 'Stop'
$taskReleaseRoot = Join-Path $PSScriptRoot 'release\1.0.0'
$taskBuildRoot = Join-Path $taskReleaseRoot 'build'
New-Item -ItemType Directory -Force -Path $taskReleaseRoot | Out-Null
& (Join-Path $PSScriptRoot 'build.ps1') -DotnetPath $DotnetPath -DalamudPath $DalamudPath -OutputDirectory $taskBuildRoot
if (-not $DotnetPath) {
    $taskSdk = Join-Path $PSScriptRoot '..\..\work\toolchain\dotnet\dotnet.exe'
    $DotnetPath = if (Test-Path -LiteralPath $taskSdk) { (Resolve-Path -LiteralPath $taskSdk).Path } else { (Get-Command dotnet).Source }
}
Push-Location $PSScriptRoot
try {
    & $DotnetPath run --project 'tests\GeometryTests.csproj' --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Release tests failed' }
} finally { Pop-Location }
$taskDll = Join-Path $taskBuildRoot 'HudEditor.dll'
$taskManifestPath = Join-Path $taskBuildRoot 'HudEditor.json'
$taskManifest = Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
$taskVersion = [Reflection.AssemblyName]::GetAssemblyName($taskDll).Version.ToString()
if ($taskVersion -ne '1.0.0.0' -or $taskManifest.AssemblyVersion -ne $taskVersion -or $taskManifest.InternalName -ne 'HudEditor' -or $taskManifest.DalamudApiLevel -ne 15) { throw 'Assembly/manifest mismatch' }
if ($taskManifest.ApplicableVersion -ne '2026.09.01.0000.0000') { throw 'Game version mismatch' }
Add-Type -AssemblyName System.IO.Compression
function Write-ReleaseZip([string]$Destination, [hashtable]$Entries) {
    $taskZipStream = [IO.File]::Open($Destination, [IO.FileMode]::Create)
    $taskArchive = [IO.Compression.ZipArchive]::new($taskZipStream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($taskEntryName in ($Entries.Keys | Sort-Object)) {
            $taskEntry = $taskArchive.CreateEntry($taskEntryName, [IO.Compression.CompressionLevel]::Optimal)
            $taskInput = [IO.File]::OpenRead($Entries[$taskEntryName]); $taskOutput = $taskEntry.Open()
            try { $taskInput.CopyTo($taskOutput) } finally { $taskInput.Dispose(); $taskOutput.Dispose() }
        }
    } finally { $taskArchive.Dispose(); $taskZipStream.Dispose() }
}
# Explicit allowlist: no backups, diagnostics, user config, runtime DLLs or development paths.
$taskPayload = @{}
foreach ($taskName in @('HudEditor.dll', 'HudEditor.json', 'HudEditor.deps.json')) { $taskPayload[$taskName] = Join-Path $taskBuildRoot $taskName }
$taskPluginZip = Join-Path $taskReleaseRoot 'HudWorkshop-1.0.0.zip'
Write-ReleaseZip $taskPluginZip $taskPayload
$taskCheck = [IO.Compression.ZipFile]::OpenRead($taskPluginZip)
try {
    $taskActual = @($taskCheck.Entries | ForEach-Object { $_.FullName } | Sort-Object)
    if (Compare-Object $taskActual @($taskPayload.Keys | Sort-Object)) { throw 'Unexpected package content' }
    foreach ($taskEntry in $taskCheck.Entries) {
        $taskStream = $taskEntry.Open()
        try { $taskPackedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskStream)) } finally { $taskStream.Dispose() }
        if ($taskPackedHash -ne (Get-FileHash -LiteralPath $taskPayload[$taskEntry.FullName] -Algorithm SHA256).Hash) { throw 'Package hash mismatch' }
    }
} finally { $taskCheck.Dispose() }
$taskSource = @{}
foreach ($taskFile in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'plugin') -File) {
    if ($taskFile.Extension -in @('.cs', '.csproj') -or $taskFile.Name -eq 'packages.lock.json') { $taskSource['plugin/' + $taskFile.Name] = $taskFile.FullName }
}
foreach ($taskName in @('tests/Program.cs','tests/GeometryTests.csproj','build.ps1','package-release.ps1','make-repository.ps1','global.json','docs/RELEASE.md')) { $taskSource[$taskName] = Join-Path $PSScriptRoot $taskName }
$taskSource['README.md'] = Join-Path $PSScriptRoot 'docs\RELEASE.md'
Write-ReleaseZip (Join-Path $taskReleaseRoot 'HudWorkshop-1.0.0-source.zip') $taskSource
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\RELEASE.md') -Destination (Join-Path $taskReleaseRoot 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'make-repository.ps1') -Destination $taskReleaseRoot -Force
Copy-Item -LiteralPath $taskManifestPath -Destination $taskReleaseRoot -Force
$taskTemplate = Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
$taskTemplate | Add-Member DownloadLinkInstall 'REPLACE_WITH_PUBLIC_HTTPS_ZIP_URL'
$taskTemplate | Add-Member DownloadLinkUpdate 'REPLACE_WITH_PUBLIC_HTTPS_ZIP_URL'
$taskTemplate | Add-Member IsHide $false
$taskTemplate | Add-Member IsTestingExclusive $false
ConvertTo-Json -InputObject @($taskTemplate) -Depth 10 | Set-Content -LiteralPath (Join-Path $taskReleaseRoot 'repo.template.json') -Encoding utf8
$taskDeliverables = @('HudWorkshop-1.0.0.zip','HudWorkshop-1.0.0-source.zip','HudEditor.json','README.md','make-repository.ps1','repo.template.json')
$taskHashes = foreach ($taskName in $taskDeliverables) { '{0}  {1}' -f (Get-FileHash -LiteralPath (Join-Path $taskReleaseRoot $taskName) -Algorithm SHA256).Hash, $taskName }
$taskHashes | Set-Content -LiteralPath (Join-Path $taskReleaseRoot 'SHA256SUMS.txt') -Encoding ascii
$taskBundle = @{}
foreach ($taskName in ($taskDeliverables + 'SHA256SUMS.txt')) { $taskBundle[$taskName] = Join-Path $taskReleaseRoot $taskName }
Write-ReleaseZip (Join-Path $taskReleaseRoot 'HudWorkshop-1.0.0-release-kit.zip') $taskBundle
Write-Output "Release kit ready: $taskReleaseRoot"

