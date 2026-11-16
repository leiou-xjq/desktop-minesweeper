param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    & (Join-Path $root 'scripts\setup-sdk.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Failed to prepare the local .NET SDK.' }
}

& $dotnet run --project (Join-Path $root 'src\DesktopMinesweeper\DesktopMinesweeper.csproj') -c $Configuration
exit $LASTEXITCODE
