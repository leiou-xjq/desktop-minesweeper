param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Probe,
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
if ($Probe) { $appArgsList += '--probe' }
if ($AppArgs) { $appArgsList += $AppArgs }

$args = @('run', '--project', (Join-Path $root 'src\DesktopMinesweeper\DesktopMinesweeper.csproj'), '-c', $Configuration)
if ($appArgsList.Count -gt 0) {
    $args += '--'
    $args += $appArgsList
}

& $dotnet @args
exit $LASTEXITCODE
