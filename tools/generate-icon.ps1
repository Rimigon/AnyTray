# Генератор фирменной иконки AnyTray (многоразмерный .ico, PNG-in-ICO).
# Логотип: скруглённый бейдж с сине-фиолетовым градиентом + белая стрелка «в трей» и полка.
# Запуск:  pwsh -STA -File tools\generate-icon.ps1
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$icoPath = Join-Path $PSScriptRoot "..\Assets\anytray.ico"
$icoDir = Split-Path $icoPath
if (-not (Test-Path $icoDir)) { New-Item -ItemType Directory -Path $icoDir | Out-Null }

function New-Point([double]$x, [double]$y, [double]$s) {
    New-Object System.Windows.Point -ArgumentList ($x * $s), ($y * $s)
}

function New-LogoVisual([double]$S) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()

    # Градиентный фон (синий -> фиолетовый), диагональ
    $bg = New-Object System.Windows.Media.LinearGradientBrush
    $bg.StartPoint = New-Object System.Windows.Point -ArgumentList 0, 0
    $bg.EndPoint = New-Object System.Windows.Point -ArgumentList 1, 1
    $c1 = [System.Windows.Media.Color]::FromRgb(45, 125, 246)   # #2D7DF6
    $c2 = [System.Windows.Media.Color]::FromRgb(124, 77, 255)   # #7C4DFF
    $bg.GradientStops.Add((New-Object System.Windows.Media.GradientStop -ArgumentList $c1, 0.0))
    $bg.GradientStops.Add((New-Object System.Windows.Media.GradientStop -ArgumentList $c2, 1.0))

    $r = $S * 0.22
    $rect = New-Object System.Windows.Rect -ArgumentList 0, 0, $S, $S
    $dc.DrawRoundedRectangle($bg, $null, $rect, $r, $r)

    # Лёгкий верхний блик
    $hl = New-Object System.Windows.Media.LinearGradientBrush
    $hl.StartPoint = New-Object System.Windows.Point -ArgumentList 0, 0
    $hl.EndPoint = New-Object System.Windows.Point -ArgumentList 0, 1
    $hl.GradientStops.Add((New-Object System.Windows.Media.GradientStop -ArgumentList ([System.Windows.Media.Color]::FromArgb(60, 255, 255, 255)), 0.0))
    $hl.GradientStops.Add((New-Object System.Windows.Media.GradientStop -ArgumentList ([System.Windows.Media.Color]::FromArgb(0, 255, 255, 255)), 0.6))
    $dc.DrawRoundedRectangle($hl, $null, $rect, $r, $r)

    # Белая стрелка «в трей»
    $white = [System.Windows.Media.Brushes]::White
    $pen = New-Object System.Windows.Media.Pen -ArgumentList $white, ($S * 0.092)
    $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round

    $dc.DrawLine($pen, (New-Point 0.5 0.18 $S), (New-Point 0.5 0.55 $S))   # стержень
    $dc.DrawLine($pen, (New-Point 0.5 0.60 $S), (New-Point 0.31 0.40 $S))  # левый ус
    $dc.DrawLine($pen, (New-Point 0.5 0.60 $S), (New-Point 0.69 0.40 $S))  # правый ус

    # Полка «трея»
    $barPen = New-Object System.Windows.Media.Pen -ArgumentList $white, ($S * 0.082)
    $barPen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $barPen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $dc.DrawLine($barPen, (New-Point 0.27 0.79 $S), (New-Point 0.73 0.79 $S))

    $dc.Close()
    return $visual
}

function Get-Png([double]$S) {
    $v = New-LogoVisual $S
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap -ArgumentList ([int]$S), ([int]$S), 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($v)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $ms = New-Object System.IO.MemoryStream
    $enc.Save($ms)
    return , $ms.ToArray()
}

$sizes = @(256, 64, 48, 32, 24, 20, 16)
$pngs = @{}
foreach ($s in $sizes) { $pngs[$s] = Get-Png ([double]$s) }

# Сборка ICO (заголовок + записи + PNG-данные)
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $len = $pngs[$s].Length
    $wb = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$wb); $bw.Write([byte]$wb); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($s in $sizes) { $bw.Write($pngs[$s]) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath $icoDir | ForEach-Object { Join-Path $_ "anytray.ico" }), $out.ToArray())
Write-Host "Иконка создана: $((Resolve-Path (Join-Path $icoDir 'anytray.ico')).Path)  ($($out.Length) байт)"
