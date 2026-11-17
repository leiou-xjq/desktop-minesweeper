#requires -Version 5.1
<#
.SYNOPSIS
    Compiles the Inno Setup installer from the self-contained publish output.

.DESCRIPTION
    Requires Inno Setup 6 (per-user install is fine). Run .\publish.ps1 first so
    that dist\win-x64\DesktopMinesweeper.exe exists.

.EXAMPLE
    .\installer\build-installer.ps1
    .\installer\build-installer.ps1 -Version 1.2.0
#>
param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\DesktopMinesweeper\DesktopMinesweeper.csproj'
$sourceExe = Join-Path $root 'dist\win-x64\DesktopMinesweeper.exe'
$iss = Join-Path $PSScriptRoot 'desktop-minesweeper.iss'

if (-not $Version) {
    [xml]$projectXml = Get-Content $project
    $Version = [string](@($projectXml.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1)
}
if (-not $Version) {
    throw 'Could not determine the version. Pass -Version explicitly.'
}
if (-not (Test-Path $sourceExe)) {
    throw "Missing $sourceExe. Run .\publish.ps1 first."
}
if (-not (Test-Path $iss)) {
    throw "Missing installer script: $iss"
}

$candidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
    $command = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($command) {
        $iscc = $command.Source
    }
}
if (-not $iscc) {
    throw @'
Inno Setup 6 was not found.
Install it for the current user (no admin needed):
  https://jrsoftware.org/isdl.php  ->  "Install for me only"
or let GitHub Actions build the installer.
'@
}

Write-Host ("Compiling installer with {0} ..." -f $iscc)
& $iscc "/DAppVersion=$Version" $iss | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw ("ISCC failed with exit code {0}" -f $LASTEXITCODE)
}

$setup = Join-Path $root ("dist\DesktopMinesweeper-{0}-setup.exe" -f $Version)
if (-not (Test-Path $setup)) {
    throw ("Installer was not produced: {0}" -f $setup)
}

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
$line = "{0}  {1}" -f $hash, (Split-Path $setup -Leaf)
Set-Content -Path "$setup.sha256" -Value $line -Encoding ASCII

Write-Host ''
Write-Host 'Installer ready' -ForegroundColor Green
Write-Host ("  file   : {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
Write-Host ("  sha256 : {0}" -f $hash)
exit 0
