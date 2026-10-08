# Generates Icon.png (256x256, 32-bit ARGB) at the repository root.
# Usage: powershell -ExecutionPolicy Bypass -File tools/GenerateIcon.ps1
# Compatible with Windows PowerShell 5.1.

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

$size = 256
$repoRoot = Split-Path -Parent $PSScriptRoot
$outPath = Join-Path $repoRoot 'Icon.png'

$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

function New-Color([int]$a, [int]$r, [int]$gr, [int]$b) {
    return [System.Drawing.Color]::FromArgb($a, $r, $gr, $b)
}

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# ---------- Gold ring ----------
$ringRect = New-Object System.Drawing.RectangleF(6, 6, 244, 244)
$ringBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($ringRect, (New-Color 255 255 226 120), (New-Color 255 176 112 20), 45.0)
$g.FillEllipse($ringBrush, $ringRect)
$g.DrawEllipse((New-Object System.Drawing.Pen((New-Color 255 90 50 10), 3)), 6, 6, 244, 244)

# ---------- Radial disc (dark red center -> purple edge) ----------
$discRect = New-Object System.Drawing.RectangleF(20, 20, 216, 216)
$discPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$discPath.AddEllipse($discRect)
$discBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($discPath)
$discBrush.CenterPoint = New-Object System.Drawing.PointF(118, 100)
$discBrush.CenterColor = New-Color 255 190 30 40
$discBrush.SurroundColors = [System.Drawing.Color[]]@((New-Color 255 60 10 85))
$blend = New-Object System.Drawing.Drawing2D.Blend(3)
$blend.Factors = [float[]]@(0.0, 0.6, 1.0)
$blend.Positions = [float[]]@(0.0, 0.5, 1.0)
$discBrush.Blend = $blend
$g.FillEllipse($discBrush, $discRect)
$g.DrawEllipse((New-Object System.Drawing.Pen((New-Color 255 110 60 10), 3)), $discRect)

# Subtle top highlight on the disc
$hlPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$hlPath.AddEllipse(50, 28, 156, 80)
$hlBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($hlPath)
$hlBrush.CenterColor = New-Color 70 255 255 255
$hlBrush.SurroundColors = [System.Drawing.Color[]]@((New-Color 0 255 255 255))
$g.FillPath($hlBrush, $hlPath)

# ---------- Skull ----------
$skullFill = New-Object System.Drawing.SolidBrush((New-Color 255 245 242 235))
$skullShade = New-Object System.Drawing.SolidBrush((New-Color 255 205 198 188))
$outline = New-Object System.Drawing.Pen((New-Color 255 40 10 30), 4)
$outline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

# Shift the skull slightly up-left so the customize panel does not cover the jaw
$g.TranslateTransform(-10, -6)

# Drop shadow
$shadow = New-Object System.Drawing.SolidBrush((New-Color 90 0 0 0))
$g.FillEllipse($shadow, 70, 58, 116, 104)
$g.FillPath($shadow, (New-RoundedRectPath 94 140 68 46 12))

# Cranium + jaw as a single outlined shape
$skullPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$skullPath.FillMode = [System.Drawing.Drawing2D.FillMode]::Winding
$skullPath.AddEllipse(66, 52, 116, 104)
$skullPath.AddPath((New-RoundedRectPath 90 132 68 48 12), $false)
$region = New-Object System.Drawing.Region($skullPath)
# outline: draw both, then fill over seams
$g.DrawEllipse($outline, 66, 52, 116, 104)
$g.DrawPath($outline, (New-RoundedRectPath 90 132 68 48 12))
$g.FillRegion($skullFill, $region)

# Shading on the right side of cranium
$shadePath = New-Object System.Drawing.Drawing2D.GraphicsPath
$shadePath.AddEllipse(66, 52, 116, 104)
$shadeBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.RectangleF(66, 52, 116, 104)), (New-Color 0 0 0 0), (New-Color 60 80 40 60), 20.0)
$g.FillPath($shadeBrush, $shadePath)

# Eye sockets
$eyeBrush = New-Object System.Drawing.SolidBrush((New-Color 255 20 5 15))
$g.FillEllipse($eyeBrush, 82, 96, 34, 30)
$g.FillEllipse($eyeBrush, 132, 96, 34, 30)
# Red eye glints
$glint = New-Object System.Drawing.SolidBrush((New-Color 255 230 40 50))
$g.FillEllipse($glint, 94, 106, 9, 9)
$g.FillEllipse($glint, 144, 106, 9, 9)

# Nose (inverted heart-ish triangle)
$nose = New-Object System.Drawing.Drawing2D.GraphicsPath
$nose.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(124, 128)),
    (New-Object System.Drawing.PointF(114, 146)),
    (New-Object System.Drawing.PointF(134, 146))
))
$g.FillPath($eyeBrush, $nose)

# Teeth
$toothPen = New-Object System.Drawing.Pen((New-Color 255 40 10 30), 3)
$toothPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$toothPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($toothPen, 98, 160, 150, 160)
foreach ($x in 108, 118, 128, 138) {
    $g.DrawLine($toothPen, $x, 152, $x, 172)
}

$g.ResetTransform()

# ---------- Customize panel (lower right) ----------
$panel = New-RoundedRectPath 150 158 92 78 14
$g.FillPath((New-Object System.Drawing.SolidBrush((New-Color 110 0 0 0))), (New-RoundedRectPath 153 162 92 78 14))
$panelBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.RectangleF(150, 158, 92, 78)), (New-Color 255 52 58 78), (New-Color 255 24 26 40), 90.0)
$g.FillPath($panelBrush, $panel)
$g.DrawPath((New-Object System.Drawing.Pen((New-Color 255 236 190 70), 3)), $panel)

$trackPen = New-Object System.Drawing.Pen((New-Color 255 110 116 140), 6)
$trackPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$trackPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$fillPen = New-Object System.Drawing.Pen((New-Color 255 255 196 60), 6)
$fillPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$fillPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$knobBrush = New-Object System.Drawing.SolidBrush((New-Color 255 255 255 255))
$knobPen = New-Object System.Drawing.Pen((New-Color 255 30 30 45), 2)

$x0 = 164; $x1 = 228
$sliders = @(@{ Y = 178; K = 0.70 }, @{ Y = 197; K = 0.30 }, @{ Y = 216; K = 0.55 })
foreach ($s in $sliders) {
    $kx = $x0 + ($x1 - $x0) * $s.K
    $g.DrawLine($trackPen, $x0, $s.Y, $x1, $s.Y)
    $g.DrawLine($fillPen, $x0, $s.Y, $kx, $s.Y)
    $g.FillEllipse($knobBrush, $kx - 7, $s.Y - 7, 14, 14)
    $g.DrawEllipse($knobPen, $kx - 7, $s.Y - 7, 14, 14)
}

$g.Dispose()
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$len = (Get-Item $outPath).Length
Write-Host "Wrote $outPath ($len bytes)"
