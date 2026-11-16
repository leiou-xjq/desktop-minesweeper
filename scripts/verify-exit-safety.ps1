#requires -Version 5.1
<#
.SYNOPSIS
    Verifies that every exit path removes the overlay and leaves desktop icons untouched.

.DESCRIPTION
    Runs the real app, exits it through each supported path, and compares a read-only
    dump of icon positions/text before and after. No icon is ever moved; the dump only
    reads metadata.

    Paths covered:
      * Exit button / Esc      (window.Close() -> ExitGuard)
      * Window close / Alt+F4  (WM_CLOSE)
      * Esc key                (real key press, when the overlay has focus)
      * Force kill             (Stop-Process -Force, like Task Manager)
      * Unhandled exception    (ExitGuard fallback)

.EXAMPLE
    .\scripts\verify-exit-safety.ps1
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "src\DesktopMinesweeper\bin\$Configuration\net8.0-windows\DesktopMinesweeper.exe"
if (-not (Test-Path $exe)) {
    throw "Build first: .\build.ps1 -Configuration $Configuration"
}

$dotnetRoot = Join-Path $root '.dotnet'
if (-not (Test-Path (Join-Path $dotnetRoot 'dotnet.exe'))) {
    throw "Local SDK missing. Run .\scripts\setup-sdk.ps1 first."
}
$env:DOTNET_ROOT_X64 = $dotnetRoot

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class DmsExitWindows
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr Find(uint pid, string title)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, lParam) =>
        {
            uint owner;
            GetWindowThreadProcessId(hwnd, out owner);
            if (owner != pid)
            {
                return true;
            }

            var buffer = new StringBuilder(256);
            GetWindowText(hwnd, buffer, buffer.Capacity);
            if (buffer.ToString() == title)
            {
                found = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@

$windowTitle = 'Desktop Minesweeper'
$temp = $env:TEMP
$results = New-Object System.Collections.Generic.List[object]
$failures = 0

function Get-IconLines([string]$path) {
    return @(Get-Content $path -Encoding UTF8 | Where-Object { $_ -match '^icon\[' })
}

function Write-Step([string]$mode, [bool]$exited, [bool]$identical, [int]$icons, [string]$note) {
    if ($exited -and $identical) {
        Write-Host ("PASS {0,-22} icons={1} {2}" -f $mode, $icons, $note) -ForegroundColor Green
    }
    else {
        Write-Host ("FAIL {0,-22} icons={1} {2}" -f $mode, $icons, $note) -ForegroundColor Red
    }

    $script:results.Add([pscustomobject]@{
        Mode      = $mode
        Exited    = $exited
        Identical = $identical
        Icons     = $icons
        Note      = $note
    })

    if (-not ($exited -and $identical)) {
        $script:failures++
    }
}

function Assert-DesktopUnchanged([string]$mode, [bool]$exited, [string]$note) {
    $after = Join-Path $temp ("dms-exit-after-{0}.txt" -f $mode)
    Start-Process -FilePath $exe -ArgumentList @('--dump', $after) -Wait
    $icons = Get-IconLines $after
    $identical = (($script:expected -join "`n") -eq ($icons -join "`n"))
    Write-Step $mode $exited $identical $icons.Count $note
}

function Assert-NoLeftoverProcess {
    $left = @(Get-Process -Name 'DesktopMinesweeper' -ErrorAction SilentlyContinue)
    if ($left.Count -gt 0) {
        foreach ($process in $left) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }

        $script:failures++
        Write-Host ("FAIL leftover process cleaned up") -ForegroundColor Red
    }
}

Write-Host "Baseline icon dump..." -ForegroundColor Cyan
$baselinePath = Join-Path $temp 'dms-exit-baseline.txt'
Start-Process -FilePath $exe -ArgumentList @('--dump', $baselinePath) -Wait
$expected = Get-IconLines $baselinePath
if ($expected.Count -eq 0) {
    throw "Baseline dump contains no icons; cannot verify."
}
Write-Host ("Baseline has {0} icons." -f $expected.Count) -ForegroundColor Cyan
Write-Host ''

# 1. Exit button / Esc code path (window.Close()).
Start-Process -FilePath $exe -ArgumentList @('--auto-exit', '1200') -Wait
Assert-DesktopUnchanged 'exit-button-esc' $true 'window.Close() path'
Assert-NoLeftoverProcess

# 2. Window close / Alt+F4 (WM_CLOSE).
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 2
$handle = [DmsExitWindows]::Find([uint32]$process.Id, $windowTitle)
if ($handle -eq [IntPtr]::Zero) {
    throw 'Overlay window not found for WM_CLOSE test.'
}
[void][DmsExitWindows]::PostMessage($handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
$exited = $process.WaitForExit(5000)
if (-not $exited) { Stop-Process -Id $process.Id -Force }
Assert-DesktopUnchanged 'wm-close' $exited 'Alt+F4 / close window'
Assert-NoLeftoverProcess

# 3. Real Esc key press (WM_KEYDOWN/WM_KEYUP to the overlay window).
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 2
$handle = [DmsExitWindows]::Find([uint32]$process.Id, $windowTitle)
if ($handle -eq [IntPtr]::Zero) {
    Stop-Process -Id $process.Id -Force
    throw 'Overlay window not found for Esc test.'
}
[void][DmsExitWindows]::PostMessage($handle, 0x0100, [IntPtr]0x1B, [IntPtr]0x00010001)
Start-Sleep -Milliseconds 150
[void][DmsExitWindows]::PostMessage($handle, 0x0101, [IntPtr]0x1B, [IntPtr]0xC0010001)
$exited = $process.WaitForExit(5000)
if (-not $exited) {
    Stop-Process -Id $process.Id -Force
    $process.WaitForExit(3000) | Out-Null
}
Assert-DesktopUnchanged 'esc-key' $exited 'Esc key message'
Assert-NoLeftoverProcess

# 4. Force kill (Task Manager equivalent).
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 2
Stop-Process -Id $process.Id -Force
$process.WaitForExit(5000) | Out-Null
Start-Sleep -Milliseconds 400
Assert-DesktopUnchanged 'force-kill' $true 'Stop-Process -Force'
Assert-NoLeftoverProcess

# 5. Unhandled exception (ExitGuard).
$process = Start-Process -FilePath $exe -ArgumentList @('--crash', '1200') -PassThru
$exited = $process.WaitForExit(8000)
if (-not $exited) {
    Stop-Process -Id $process.Id -Force
}
$crashExitCode = if ($exited) { $process.ExitCode } else { -1 }
$crashOk = $exited -and $crashExitCode -ne 0
Start-Sleep -Milliseconds 400
Assert-DesktopUnchanged 'unhandled-exception' $crashOk ("exit code {0}" -f $crashExitCode)
Assert-NoLeftoverProcess

Write-Host ''
Write-Host 'Summary' -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String | Write-Host

if ($failures -gt 0) {
    Write-Host ("FAILED: {0} check(s) failed." -f $failures) -ForegroundColor Red
    exit 1
}

Write-Host 'PASS: every exit path removed the overlay and left desktop icons unchanged.' -ForegroundColor Green
exit 0
