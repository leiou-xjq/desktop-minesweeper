param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Probe,
    [switch]$FixedGrid,
    [switch]$SelfTest,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AppArgs
)
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    & (Join-Path $root 'scripts\setup-sdk.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Failed to prepare the local .NET SDK.' }
}

$appArgsList = @()
$selfTestReport = $null
if ($Probe) { $appArgsList += '--probe' }
if ($FixedGrid) { $appArgsList += '--fixed-grid' }
if ($SelfTest) {
    $selfTestReport = Join-Path $env:TEMP 'dms-selftest.txt'
    $appArgsList += '--selftest'
    $appArgsList += $selfTestReport
}
if ($AppArgs) { $appArgsList += $AppArgs }

$args = @('run', '--project', (Join-Path $root 'src\DesktopMinesweeper\DesktopMinesweeper.csproj'), '-c', $Configuration)
if ($appArgsList.Count -gt 0) {
    $args += '--'
    $args += $appArgsList
}

& $dotnet @args
$code = $LASTEXITCODE

if ($SelfTest -and $selfTestReport -and (Test-Path $selfTestReport)) {
    Get-Content $selfTestReport -Encoding UTF8
}

exit $code
