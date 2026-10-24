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

# ---------- Skull (styled after the in-game boss skull icon) ----------
# Round cranium that narrows into a slightly smaller jaw, symmetric around x = 124
function New-SkullPath([float]$dx, [float]$dy) {
    $pts = @(
        @(124, 48),
        @(162, 48), @(188, 74), @(188, 106),
        @(188, 128), @(178, 140), @(168, 150),
        @(163, 156), @(163, 170), @(155, 178),
        @(147, 186), @(134, 187), @(124, 187),
        @(114, 187), @(101, 186), @(93, 178),
        @(85, 170), @(85, 156), @(80, 150),
        @(70, 140), @(60, 128), @(60, 106),
        @(60, 74), @(86, 48), @(124, 48)
    )
    $pf = [System.Drawing.PointF[]]($pts | ForEach-Object { New-Object System.Drawing.PointF(($_[0] + $dx), ($_[1] + $dy)) })
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddBeziers($pf)
    $p.CloseFigure()
    return $p
}

$boneDark = New-Color 255 62 34 18
$outline = New-Object System.Drawing.Pen($boneDark, 5)
$outline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

# Shift the skull slightly up-left so the customize panel does not cover the jaw
$g.TranslateTransform(-10, -6)
# Scale the skull up around its center so it overlaps the ring and panel a bit
$skullScale = 1.18
$g.TranslateTransform(124, 118)
$g.ScaleTransform($skullScale, $skullScale)
$g.TranslateTransform(-124, -118)

# Drop shadow
$g.FillPath((New-Object System.Drawing.SolidBrush((New-Color 90 0 0 0))), (New-SkullPath 4 6))

# Bone fill: cream on top, warmer tan towards the jaw
$skullPath = New-SkullPath 0 0
$boneBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.RectangleF(60, 48, 128, 140)), (New-Color 255 250 243 226), (New-Color 255 214 192 156), 90.0)
$boneBlend = New-Object System.Drawing.Drawing2D.Blend(3)
$boneBlend.Factors = [float[]]@(0.0, 0.15, 1.0)
$boneBlend.Positions = [float[]]@(0.0, 0.55, 1.0)
$boneBrush.Blend = $boneBlend
$g.FillPath($boneBrush, $skullPath)

# Soft highlight on the upper-left of the cranium
$state = $g.Save()
$g.SetClip($skullPath)
$hl = New-Object System.Drawing.Drawing2D.GraphicsPath
$hl.AddEllipse(70, 54, 70, 46)
$hlSkull = New-Object System.Drawing.Drawing2D.PathGradientBrush($hl)
$hlSkull.CenterColor = New-Color 150 255 255 255
$hlSkull.SurroundColors = [System.Drawing.Color[]]@((New-Color 0 255 255 255))
$g.FillPath($hlSkull, $hl)
$g.Restore($state)

$g.DrawPath($outline, $skullPath)

# Eye sockets: big tilted ovals, dark brown with an orange glow at the bottom
$socketBrush = New-Object System.Drawing.SolidBrush($boneDark)
$glowBrush = New-Object System.Drawing.SolidBrush((New-Color 255 226 110 30))
foreach ($eye in @(@{ X = 99; R = -14 }, @{ X = 149; R = 14 })) {
    $state = $g.Save()
    $g.TranslateTransform($eye.X, 112)
    $g.RotateTransform($eye.R)
    $g.FillEllipse($socketBrush, -19, -23, 38, 46)
    $g.FillEllipse($glowBrush, -14, 2, 28, 18)
    $g.FillEllipse($socketBrush, -16, -21, 32, 36)
    $g.Restore($state)
}

# Nose: small rounded inverted triangle
$nose = New-Object System.Drawing.Drawing2D.GraphicsPath
$nose.AddPolygon([System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(118, 141)),
    (New-Object System.Drawing.PointF(130, 141)),
    (New-Object System.Drawing.PointF(124, 151))
))
$nosePen = New-Object System.Drawing.Pen($boneDark, 4)
$nosePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.FillPath($socketBrush, $nose)
$g.DrawPath($nosePen, $nose)

# Teeth: short, soft lines on the jaw
$toothPen = New-Object System.Drawing.Pen((New-Color 255 150 112 76), 3)
$toothPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$toothPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
foreach ($x in 111, 124, 137) {
    $g.DrawLine($toothPen, $x, 166, $x, 178)
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
