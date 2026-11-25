<#
.SYNOPSIS
Makes every picture the README uses (docs/*.jpg) from the game, in one command (#269).

.DESCRIPTION
Needs the game and a Debug build of the mod in its Mods folder, so it can't run on GitHub; it is a local step before a
release. It:
  1. starts the game windowed with only this mod and runs ScreenshotTour and TierLookTour, which save screenshots in
     English, at the resolution in pictures.json, with fixed setups (CBC_SCREENSHOTS, CBC_RESOLUTIONS, CBC_TESTS),
  2. makes each picture in pictures.json from them: scaled to the README's 1280x720, cropped, or for tier-look a grid
     of tiers 5-10 of a few bosses, saved as JPEG,
  3. compares each with the picture in docs/ and only replaces it when it really changed. Animations make every run a
     little different, so a picture whose pixels changed for less than its tolerance (a share of the pixels, 1% by
     default) is kept, and git shows no change for it.

The game's own profile isn't changed: the tour uses throwaway setup files and puts the player's language back.

.PARAMETER Work
Where the screenshots and the new pictures go. Emptied before the game runs.

.PARAMETER SkipGame
Use the screenshots already in -Work instead of running the game, e.g. after changing a crop in pictures.json.

.PARAMETER Force
Replace every picture in docs/, even the ones that didn't change more than their tolerance.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File tools/screenshots/Make-Screenshots.ps1
#>
param(
    [string] $Game = "C:\Program Files\Epic Games\BloonsTD6",
    [string] $Work = (Join-Path $env:TEMP "cbc-screenshots"),
    [string] $Docs = (Join-Path $PSScriptRoot "..\..\docs"),
    [string] $Manifest = (Join-Path $PSScriptRoot "pictures.json"),
    [int] $TimeoutMinutes = 20,
    [switch] $SkipGame,
    [switch] $Force
)

$ErrorActionPreference = "Stop"
$config = Get-Content $Manifest -Raw | ConvertFrom-Json
$tour = Join-Path $Work "tour"
$out = Join-Path $Work "pictures"
$Docs = [IO.Path]::GetFullPath($Docs)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

public static class ReadmePictures
{
    // Part of a screenshot (x, y, width, height in its own pixels), scaled to width x height
    public static Bitmap Crop(string path, Rectangle part, int width, int height)
    {
        using (var shot = new Bitmap(path))
        {
            part.Intersect(new Rectangle(0, 0, shot.Width, shot.Height));
            var picture = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(picture))
            using (var attributes = new ImageAttributes())
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                // Keeps the edges from fading into the background colour
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(shot, new Rectangle(0, 0, width, height), part.X, part.Y, part.Width, part.Height, GraphicsUnit.Pixel, attributes);
            }
            return picture;
        }
    }

    // A grid of square pictures with column and row labels, like docs/tier-look.jpg
    public static Bitmap Grid(string[,] paths, string[] columns, string[] rows, int width, int height)
    {
        const int cell = 182, gap = 4, left = 164, top = 100;
        var picture = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(picture))
        using (var font = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Point))
        using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (var leftAligned = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
        {
            g.Clear(Color.FromArgb(24, 24, 30));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            for (int c = 0; c < columns.Length; c++)
                g.DrawString(columns[c], font, Brushes.White, new RectangleF(left + c * (cell + gap), top - 40, cell, 40), centered);
            for (int r = 0; r < rows.Length; r++)
            {
                g.DrawString(rows[r], font, Brushes.White, new RectangleF(4, top + r * (cell + gap), left - 8, cell), leftAligned);
                for (int c = 0; c < columns.Length; c++)
                using (var image = new Bitmap(paths[r, c]))
                    g.DrawImage(image, new Rectangle(left + c * (cell + gap), top + r * (cell + gap), cell, cell));
            }
        }
        return picture;
    }

    public static void SaveJpeg(Bitmap picture, string path, long quality)
    {
        var codec = Array.Find(ImageCodecInfo.GetImageEncoders(), e => e.FormatID == ImageFormat.Jpeg.Guid);
        using (var parameters = new EncoderParameters(1))
        {
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            picture.Save(path, codec, parameters);
        }
    }

    // The share of pixels whose colour changed by more than threshold (0-255) in a channel; 1 if the sizes differ
    public static double Changed(string a, string b, int threshold)
    {
        using (var x = new Bitmap(a))
        using (var y = new Bitmap(b))
        {
            if (x.Width != y.Width || x.Height != y.Height) return 1;
            int[] px = Pixels(x), py = Pixels(y);
            long changed = 0;
            for (int i = 0; i < px.Length; i++)
            {
                int p = px[i], q = py[i];
                int d = Math.Max(Math.Abs(((p >> 16) & 255) - ((q >> 16) & 255)),
                        Math.Max(Math.Abs(((p >> 8) & 255) - ((q >> 8) & 255)), Math.Abs((p & 255) - (q & 255))));
                if (d > threshold) changed++;
            }
            return (double) changed / px.Length;
        }
    }

    static int[] Pixels(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new int[bitmap.Width * bitmap.Height];
        for (int y = 0; y < bitmap.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bitmap.Width, bitmap.Width);
        bitmap.UnlockBits(data);
        return pixels;
    }
}
"@

# 1. The game: the tour, at a fixed size, into $tour
if (-not $SkipGame) {
    if (Get-Process BloonsTD6 -ErrorAction SilentlyContinue) { throw "Close the game first: the tour starts a game of its own" }
    if (Test-Path $tour) { Remove-Item $tour -Recurse -Force }
    New-Item -ItemType Directory -Force $tour | Out-Null
    $bosses = @($config.pictures | Where-Object { $_.tierLook } | ForEach-Object { $_.tierLook.bosses } | Select-Object -Unique)

    $variables = @{
        CBC_SCREENSHOTS = $tour
        CBC_RESOLUTIONS = $config.resolution
        CBC_TESTS = "ScreenshotTour,TierLookTour"
        CBC_TIERLOOK_BOSSES = $bosses -join ","
    }
    $arguments = "moddingDev", "--modhelper.offline", "--modhelper.only=CustomBossChallenge;", "--modhelper.noaudio",
        "--modhelper.run", "-q", "test", "mod", "CustomBossChallenge"
    "Running the screenshot tour in the game (a few minutes; leave the game window alone)"
    try {
        foreach ($name in $variables.Keys) { Set-Item "env:$name" $variables[$name] }
        $process = Start-Process -FilePath (Join-Path $Game "BloonsTD6.exe") -WorkingDirectory $Game -ArgumentList $arguments -PassThru
    }
    finally {
        foreach ($name in $variables.Keys) { Remove-Item "env:$name" -ErrorAction SilentlyContinue }
    }
    if (-not $process.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "The game didn't finish the tour in $TimeoutMinutes minutes"
    }

    $log = Get-Content (Join-Path $Game "MelonLoader\Latest.log") -Raw -Encoding utf8
    Copy-Item (Join-Path $Game "MelonLoader\Latest.log") (Join-Path $Work "Latest.log")
    $ran = [regex]::Match($log, "Ran (\d+) test\(s\), (\d+) succeeded, (\d+) failed")
    if (-not $ran.Success) { throw "The tests didn't run; see $Work\Latest.log" }
    if ([int]$ran.Groups[3].Value -gt 0) { throw "$($ran.Groups[3].Value) test(s) failed; see $Work\Latest.log" }
}

# 2. The pictures, from the screenshots
$size = $config.resolution -split "x"
$shots = Join-Path $tour $config.resolution
if (-not (Test-Path $shots)) { throw "No screenshots in $shots; run without -SkipGame first" }
$unit = [double]$size[0] / 1280
New-Item -ItemType Directory -Force $out | Out-Null

function New-Picture($picture) {
    if ($picture.tierLook) {
        $look = $picture.tierLook
        $paths = New-Object 'string[,]' $look.bosses.Count, $look.tiers.Count
        for ($r = 0; $r -lt $look.bosses.Count; $r++) {
            for ($c = 0; $c -lt $look.tiers.Count; $c++) {
                $path = Join-Path $tour ("tier-look\{0}-{1}.png" -f $look.bosses[$r], $look.tiers[$c])
                if (-not (Test-Path $path)) { throw "Missing $path" }
                $paths[$r, $c] = $path
            }
        }
        $columns = [string[]]@($look.tiers | ForEach-Object { "Tier $_" })
        return [ReadmePictures]::Grid($paths, $columns, [string[]]@($look.bosses), 1280, 720)
    }

    $path = Join-Path $shots "$($picture.name).png"
    if (-not (Test-Path $path)) { throw "Missing $path" }
    $scale = if ($picture.scale) { [double]$picture.scale } else { 1 }
    if ($picture.region) {
        # A part of the screen the tour wrote down in <name>.regions.json, in the screenshot's own pixels
        $regions = (Get-Content ([IO.Path]::ChangeExtension($path, ".regions.json")) -Raw | ConvertFrom-Json).regions
        $region = $regions | Where-Object name -eq $picture.region | Select-Object -First 1
        if (-not $region) { throw "$path has no region '$($picture.region)'" }
        $pad = [double]$picture.padding * $unit
        $part = New-Object Drawing.Rectangle ([int]($region.x - $pad)), ([int]($region.y - $pad)), ([int]($region.width + 2 * $pad)), ([int]($region.height + 2 * $pad))
        $width = [int]($part.Width / $unit * $scale); $height = [int]($part.Height / $unit * $scale)
    }
    elseif ($picture.crop) {
        $crop = $picture.crop
        $part = New-Object Drawing.Rectangle ([int]($crop.x * $unit)), ([int]($crop.y * $unit)), ([int]($crop.width * $unit)), ([int]($crop.height * $unit))
        $width = [int]($crop.width * $scale); $height = [int]($crop.height * $scale)
    }
    else {
        $part = New-Object Drawing.Rectangle 0, 0, ([int]$size[0]), ([int]$size[1])
        $width = [int](1280 * $scale); $height = [int](720 * $scale)
    }
    [ReadmePictures]::Crop($path, $part, $width, $height)
}

# 3. Into docs/, only where a picture really changed
$results = foreach ($picture in $config.pictures) {
    $file = "$($picture.name).jpg"
    $new = Join-Path $out $file
    $bitmap = New-Picture $picture
    try { [ReadmePictures]::SaveJpeg($bitmap, $new, 90) } finally { $bitmap.Dispose() }

    $old = Join-Path $Docs $file
    $tolerance = if ($picture.tolerance) { [double]$picture.tolerance } else { 0.01 }
    $changed = if (Test-Path $old) { [ReadmePictures]::Changed($old, $new, 32) } else { 1 }
    $state = if (-not (Test-Path $old)) { "new" } elseif ($Force -or $changed -gt $tolerance) { "updated" } else { "unchanged" }
    if ($state -ne "unchanged") { Copy-Item $new $old -Force }
    [pscustomobject]@{ Picture = $file; State = $state; Changed = "{0:P1}" -f $changed; Tolerance = "{0:P1}" -f $tolerance }
}
$results | Format-Table -AutoSize
"New pictures in $out; docs/ is $Docs"

# What Check-Release.ps1 reads to remind about the pictures before a release
$version = [regex]::Match((Get-Content (Join-Path $PSScriptRoot "..\..\ModHelperData.cs") -Raw), 'const string Version = "([^"]+)"').Groups[1].Value
$receipt = [ordered]@{
    modVersion = $version
    made = (Get-Date).ToString("yyyy-MM-dd")
    resolution = $config.resolution
    changed = @($results | Where-Object State -ne "unchanged" | ForEach-Object Picture)
}
$receiptPath = Join-Path $PSScriptRoot "..\..\TestResults\screenshots.json"
[IO.File]::WriteAllText([IO.Path]::GetFullPath($receiptPath), ($receipt | ConvertTo-Json) + "`n")
$changed = @($receipt.changed)
if ($changed.Count -gt 0) { "Look through these before committing them with TestResults/screenshots.json: $($changed -join ', ')" }
else { "No picture changed; commit TestResults/screenshots.json so the release check knows they were made for $version" }
