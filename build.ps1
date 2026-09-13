param([string]$DotnetPath, [string]$DalamudPath, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (-not $DotnetPath) {
    $taskLocalDotnet = Join-Path $PSScriptRoot '..\..\work\toolchain\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $taskLocalDotnet) { $DotnetPath = (Resolve-Path -LiteralPath $taskLocalDotnet).Path }
    else { $DotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
}
if (-not $DalamudPath) { $DalamudPath = Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\15.0.3.4' }
if (-not (Test-Path -LiteralPath (Join-Path $DalamudPath 'Dalamud.dll'))) { throw 'Dalamud 15.0.3.4 libraries not found; provide -DalamudPath.' }
$taskPreviousDalamud = $env:DALAMUD_HOME
$taskPreviousTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
try {
    $env:DALAMUD_HOME = $DalamudPath
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    Push-Location $PSScriptRoot
    try {
        & $DotnetPath restore 'plugin\HudEditor.csproj' --locked-mode --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
        $taskBuildArgs = @('build', 'plugin\HudEditor.csproj', '-c', 'Release', '--no-restore', '--nologo')
        if ($OutputDirectory) { $taskBuildArgs += @('--output', $OutputDirectory) }
        & $DotnetPath @taskBuildArgs
        if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    } finally { Pop-Location }
} finally { $env:DALAMUD_HOME = $taskPreviousDalamud; $env:DOTNET_CLI_TELEMETRY_OPTOUT = $taskPreviousTelemetry }
