#requires -Version 5.1
<#
.SYNOPSIS
    Produces the release artifacts: a self-contained single-file exe, a
    framework-dependent single-file exe, a zip bundle and SHA-256 checksums.

.DESCRIPTION
    Everything is written under .\dist. The project-local .NET SDK is used, so no
    system-wide SDK or administrator rights are required.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Version 1.0.0
#>
param(
    [string]$Version,
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [switch]$SkipFrameworkDependent,
    [switch]$NoZip
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$project = Join-Path $root 'src\DesktopMinesweeper\DesktopMinesweeper.csproj'

if (-not (Test-Path $dotnet)) {
    # CI runners and contributors may already have a global SDK; prefer it over
    # downloading another copy. Local builds keep using the project-local SDK.
    $globalDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($globalDotnet) {
        $dotnet = $globalDotnet.Source
        Write-Host ("Using installed SDK: {0}" -f $dotnet)
    }
    else {
        & (Join-Path $root 'scripts\setup-sdk.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'Failed to prepare the local .NET SDK.' }
    }
}

if (-not $Version) {
    [xml]$projectXml = Get-Content $project
    $Version = [string](@($projectXml.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1)
}
if (-not $Version) {
    throw 'Could not determine the version. Pass -Version explicitly.'
}

$dist = Join-Path $root 'dist'
$selfContainedDir = Join-Path $dist $Runtime
$frameworkDir = Join-Path $dist "$Runtime-fx"

foreach ($dir in @($selfContainedDir, $frameworkDir)) {
    if (Test-Path $dir) {
        Remove-Item $dir -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $dist -Force | Out-Null
Get-ChildItem $dist -File -Filter 'DesktopMinesweeper-*' -ErrorAction SilentlyContinue | Remove-Item -Force

function Invoke-Publish {
    param(
        [string]$OutputDirectory,
        [bool]$SelfContained
    )

    Write-Host ("Publishing {0} (self-contained={1}) ..." -f $OutputDirectory, $SelfContained) -ForegroundColor Cyan
    $arguments = @(
        'publish', $project,
        '-c', 'Release',
        '-r', $Runtime,
        '--self-contained', $SelfContained.ToString().ToLowerInvariant(),
        '-p:PublishSingleFile=true',
        '-p:PublishTrimmed=false',
        '-p:DebugType=none',
        '-p:DebugSymbols=false',
        ('-p:Version=' + $Version),
        '-o', $OutputDirectory
    )

    if ($SelfContained) {
        $arguments += '-p:IncludeNativeLibrariesForSelfExtract=true'
        $arguments += '-p:EnableCompressionInSingleFile=true'
    }

    & $dotnet @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet publish failed for {0}" -f $OutputDirectory)
    }

    $exe = Join-Path $OutputDirectory 'DesktopMinesweeper.exe'
    if (-not (Test-Path $exe)) {
        throw ("Publish did not produce {0}" -f $exe)
    }

    return $exe
}

function Write-Checksum {
    param([string]$Path)

    $hash = (Get-FileHash $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $line = "{0}  {1}" -f $hash, (Split-Path $Path -Leaf)
    Set-Content -Path "$Path.sha256" -Value $line -Encoding ASCII
    return $hash
}

$selfContainedExe = Invoke-Publish -OutputDirectory $selfContainedDir -SelfContained $true
$selfContainedAsset = Join-Path $dist ("DesktopMinesweeper-{0}-{1}.exe" -f $Version, $Runtime)
Copy-Item $selfContainedExe $selfContainedAsset -Force
$selfContainedHash = Write-Checksum $selfContainedAsset

$frameworkExe = $null
$frameworkAsset = $null
$frameworkHash = $null
if (-not $SkipFrameworkDependent) {
    $frameworkExe = Invoke-Publish -OutputDirectory $frameworkDir -SelfContained $false
    $frameworkAsset = Join-Path $dist ("DesktopMinesweeper-{0}-{1}-fx.exe" -f $Version, $Runtime)
    Copy-Item $frameworkExe $frameworkAsset -Force
    $frameworkHash = Write-Checksum $frameworkAsset
}

$zipPath = $null
if (-not $NoZip) {
    Write-Host 'Creating release zip ...' -ForegroundColor Cyan
    $zipPath = Join-Path $dist ("DesktopMinesweeper-{0}-{1}.zip" -f $Version, $Runtime)
    $staging = Join-Path $dist '_zip'
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    New-Item -ItemType Directory -Path $staging | Out-Null

    Copy-Item $selfContainedExe $staging
    foreach ($file in @('LICENSE', 'README.md')) {
        $source = Join-Path $root $file
        if (Test-Path $source) { Copy-Item $source $staging }
    }

    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath -CompressionLevel Optimal
    Remove-Item $staging -Recurse -Force
    Write-Checksum $zipPath | Out-Null
}

Write-Host ''
Write-Host 'Release artifacts' -ForegroundColor Green
Write-Host ("  version            : {0}" -f $Version)
Write-Host ("  self-contained exe : {0} ({1:N1} MB) sha256={2}" -f $selfContainedAsset, ((Get-Item $selfContainedAsset).Length / 1MB), $selfContainedHash)
if ($frameworkAsset) {
    Write-Host ("  framework exe      : {0} ({1:N1} MB) sha256={2}" -f $frameworkAsset, ((Get-Item $frameworkAsset).Length / 1MB), $frameworkHash)
}
if ($zipPath) {
    Write-Host ("  zip bundle         : {0} ({1:N1} MB)" -f $zipPath, ((Get-Item $zipPath).Length / 1MB))
}
Write-Host ("  output directory   : {0}" -f $dist)

exit 0
