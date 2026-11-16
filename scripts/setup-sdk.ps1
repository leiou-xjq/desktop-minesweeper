# Installs a project-local .NET 8 SDK into <repo>\.dotnet.
# It does not modify PATH, the registry, or any system-wide setting.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'

if (Test-Path $dotnet) {
    Write-Host ".NET SDK already present: $dotnet"
    & $dotnet --version
    exit 0
}

$installer = Join-Path $PSScriptRoot 'dotnet-install.ps1'
if (-not (Test-Path $installer)) {
    Write-Host 'Downloading dotnet-install.ps1 ...'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
}

Write-Host 'Installing .NET 8 SDK into .dotnet (local only) ...'
& powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Channel 8.0 -InstallDir (Join-Path $root '.dotnet') -NoPath
if ($LASTEXITCODE -ne 0) { throw "dotnet-install failed with exit code $LASTEXITCODE" }

& $dotnet --version
