param(
    [string]$Out = (Join-Path $PSScriptRoot '..\Assets\anytray.ico')
)

Add-Type -AssemblyName System.Drawing

# Палитра: изумрудно-циановая (замена прежнему синему/фиолетовому)
$colorBg1 = [System.Drawing.Color]::FromArgb(255,  16, 185, 129)  # #10B981  emerald
$colorBg2 = [System.Drawing.Color]::FromArgb(255,   6, 182, 212)  # #06B6D4  cyan
$colorFg  = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode   = 'HighQuality'
    $g.TextRenderingHint = 'AntiAliasGridFit'

    # Скруглённый фон: рисуем в клипе в форме rounded-rect
    $radius = [int]($Size * 0.22)
    $rect   = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [int]($radius * 2)
    $path.AddArc(0,            0,            $d, $d, 180, 90)
    $path.AddArc($Size - $d,   0,            $d, $d, 270, 90)
    $path.AddArc($Size - $d,   $Size - $d,   $d, $d,   0, 90)
    $path.AddArc(0,            $Size - $d,   $d, $d,  90, 90)
    $path.CloseFigure()
    $g.SetClip($path)

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect, $colorBg1, $colorBg2,
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillRectangle($brush, $rect)
    $g.ResetClip()
    $path.Dispose()

    # Лёгкий внутренний блик
    $hi = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(60, 255, 255, 255))
    $g.FillEllipse($hi, -[int]($Size*0.25), -[int]($Size*0.30), [int]($Size*0.85), [int]($Size*0.55))
    $hi.Dispose()

    # Стрелка вверх
    $pad   = [int]($Size * 0.20)
    $cx    = $Size / 2.0
    $topY  = $pad
    $botY  = $Size - $pad
    $shaftW = [Math]::Max(1.5, $Size * 0.16)   # толщина стержня
    $headW  = [Math]::Max(3.0, $Size * 0.36)   # размах наконечника

    $pen = New-Object System.Drawing.Pen $colorFg, ([float]$shaftW)
    $pen.StartCap = 'Round'
    $pen.EndCap   = 'Round'
    $pen.LineJoin = 'Round'

    # Стержень
    $g.DrawLine($pen, [float]$cx, [float]$topY, [float]$cx, [float]$botY)
    # Наконечник
    $g.DrawLine($pen, [float]$cx, [float]$topY, [float]($cx - $headW/2), [float]($topY + $headW/2))
    $g.DrawLine($pen, [float]$cx, [float]$topY, [float]($cx + $headW/2), [float]($topY + $headW/2))

    $pen.Dispose()
    $brush.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes  = @(16, 32, 48, 64, 128, 256)
$pngBlobs = @()
foreach ($s in $sizes) {
    $b  = New-IconBitmap -Size $s
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    $pngBlobs += @( , $ms.ToArray() )
    $ms.Dispose()
}

$msOut = New-Object System.IO.MemoryStream
$bw    = New-Object System.IO.BinaryWriter $msOut

# ICONDIR (6 байт)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

# Каждый кадр 16 байт ICONDIRENTRY
$headerSize = 6 + 16 * $sizes.Count
$offset = $headerSize

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $len = $pngBlobs[$i].Length
    $w = if ($s -ge 256) { 0 } else { [byte]$s }
    $h = $w
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$len)
    $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($blob in $pngBlobs) { $bw.Write($blob) }
$bw.Flush()

$dir = Split-Path -Parent $Out
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
[System.IO.File]::WriteAllBytes($Out, $msOut.ToArray())
$bw.Dispose()
$msOut.Dispose()

Write-Host "ICO written: $Out ($((Get-Item $Out).Length) bytes)"
