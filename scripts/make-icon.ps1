#requires -Version 5.1
param(
    [string]$OutFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'assets\app.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-MasterBitmap {
    $size = 256
    $bmp = New-Object System.Drawing.Bitmap -ArgumentList $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg = New-RoundedPath 8 8 240 240 52
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 14, 23, 32))
    $g.FillPath($bgBrush, $bg)
    $border = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 94, 234, 212)), 4
    $g.DrawPath($border, $bg)

    $cx = 128.0
    $cy = 124.0
    $spike = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 232, 246, 248)), 14
    $spike.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $spike.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    foreach ($angle in @(0, 45, 90, 135)) {
        $rad = $angle * [Math]::PI / 180
        $dx = [Math]::Cos($rad) * 58
        $dy = [Math]::Sin($rad) * 58
        $g.DrawLine($spike, [float]($cx - $dx), [float]($cy - $dy), [float]($cx + $dx), [float]($cy + $dy))
    }

    $bodyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 10, 16, 21))
    $g.FillEllipse($bodyBrush, [float]($cx - 38), [float]($cy - 38), 76, 76)
    $bodyPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 232, 246, 248)), 10
    $g.DrawEllipse($bodyPen, [float]($cx - 38), [float]($cy - 38), 76, 76)

    $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 94, 234, 212))
    $g.FillEllipse($dot, [float]($cx - 11), [float]($cy - 11), 22, 22)

    $g.Dispose()
    return $bmp
}

$master = New-MasterBitmap
try {
    $sizes = @(16, 32, 48, 256)
    $images = New-Object System.Collections.Generic.List[byte[]]

    foreach ($size in $sizes) {
        $scaled = New-Object System.Drawing.Bitmap -ArgumentList $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($scaled)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage($master, (New-Object System.Drawing.Rectangle -ArgumentList 0, 0, $size, $size))
        $graphics.Dispose()

        $stream = New-Object System.IO.MemoryStream
        $scaled.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $images.Add($stream.ToArray())
        $stream.Dispose()
        $scaled.Dispose()
    }

    $output = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $output
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)

    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $dimension = if ($size -ge 256) { 0 } else { $size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }

    foreach ($image in $images) {
        $writer.Write($image)
    }

    $writer.Flush()
    $directory = Split-Path $OutFile -Parent
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    [System.IO.File]::WriteAllBytes($OutFile, $output.ToArray())
    $writer.Dispose()
    $output.Dispose()

    $wizardPath = Join-Path (Split-Path $OutFile -Parent) 'wizard-small.png'
    $wizard = New-Object System.Drawing.Bitmap -ArgumentList 58, 58, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $wizardGraphics = [System.Drawing.Graphics]::FromImage($wizard)
    $wizardGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $wizardGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $wizardGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $wizardGraphics.DrawImage($master, (New-Object System.Drawing.Rectangle -ArgumentList 0, 0, 58, 58))
    $wizardGraphics.Dispose()
    $wizard.Save($wizardPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $wizard.Dispose()

    Write-Host ("Wrote {0} ({1} bytes, {2} sizes)" -f $OutFile, (Get-Item $OutFile).Length, $sizes.Count)
    Write-Host ("Wrote {0}" -f $wizardPath)
}
finally {
    $master.Dispose()
}
