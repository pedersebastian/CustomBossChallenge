<#
.SYNOPSIS
Compares ScreenshotTour screenshots taken with other mods against a baseline taken with this mod alone.

.DESCRIPTION
For every PNG in -Baseline (subfolders too) it looks for the same file in each folder of -Candidates and works out how
many pixels changed, on the whole screen and inside this mod's own UI (the <name>.regions.json files ScreenshotTour
writes next to each screenshot). It saves a diff image per screenshot (changed pixels red over a grey copy) and writes
report.html with baseline, candidate and diff side by side, the biggest changes inside our UI first.

Animations make every run a little different. Pass a second baseline as the first candidate (e.g. "baseline-2"): its
changes are the noise floor, and a candidate only counts as changed where it changed clearly more than that.

.EXAMPLE
.github/scripts/Compare-Screenshots.ps1 -Baseline runs\screenshots\none -Candidates runs\screenshots\none-2, runs\screenshots\UsefulUtilities -Out runs\screenshot-report
#>
param(
    [Parameter(Mandatory)] [string] $Baseline,
    [Parameter(Mandatory)] [string[]] $Candidates,
    [Parameter(Mandatory)] [string] $Out,
    # How much a colour channel must differ (0-255) for a pixel to count as changed
    [int] $Threshold = 32,
    # The first candidate is a second baseline, whose changes are the noise floor
    [switch] $FirstIsNoise
)

$ErrorActionPreference = "Stop"
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ScreenshotDiff
{
    // Returns the share of changed pixels on the whole image, then in each region (x, y, width, height from the top
    // left), and saves the diff image
    public static double[] Compare(string basePath, string otherPath, string diffPath, int threshold, int[] regions)
    {
        using (var a = new Bitmap(basePath))
        using (var b = new Bitmap(otherPath))
        {
            int w = Math.Min(a.Width, b.Width), h = Math.Min(a.Height, b.Height);
            var pa = Pixels(a); var pb = Pixels(b);
            var changed = new bool[w * h];
            long total = 0;
            var diff = new int[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int ca = pa[y * a.Width + x], cb = pb[y * b.Width + x];
                int d = Math.Max(Math.Abs(((ca >> 16) & 255) - ((cb >> 16) & 255)),
                        Math.Max(Math.Abs(((ca >> 8) & 255) - ((cb >> 8) & 255)), Math.Abs((ca & 255) - (cb & 255))));
                bool c = d > threshold;
                changed[y * w + x] = c;
                if (c) total++;
                int grey = (((cb >> 16) & 255) + ((cb >> 8) & 255) + (cb & 255)) / 6 + 40;
                diff[y * w + x] = c ? unchecked((int)0xFFFF2020) : unchecked((int)0xFF000000) | (grey << 16) | (grey << 8) | grey;
            }

            var result = new double[1 + regions.Length / 4];
            result[0] = (double) total / (w * h);
            for (int r = 0; r < regions.Length / 4; r++)
            {
                int x0 = Math.Max(0, regions[r * 4]), y0 = Math.Max(0, regions[r * 4 + 1]);
                int x1 = Math.Min(w, x0 + regions[r * 4 + 2]), y1 = Math.Min(h, y0 + regions[r * 4 + 3]);
                long n = 0, c = 0;
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) { n++; if (changed[y * w + x]) c++; }
                result[1 + r] = n == 0 ? 0 : (double) c / n;
                for (int x = x0; x < x1; x++) { Mark(diff, w, x, y0); Mark(diff, w, x, y1 - 1); }
                for (int y = y0; y < y1; y++) { Mark(diff, w, x0, y); Mark(diff, w, x1 - 1, y); }
            }

            using (var image = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                var data = image.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                for (int y = 0; y < h; y++) Marshal.Copy(diff, y * w, data.Scan0 + y * data.Stride, w);
                image.UnlockBits(data);
                image.Save(diffPath, ImageFormat.Png);
            }
            return result;
        }
    }

    static void Mark(int[] pixels, int w, int x, int y)
    {
        if (x >= 0 && y >= 0 && x < w && y * w + x < pixels.Length) pixels[y * w + x] = unchecked((int)0xFF20D0FF);
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

function Read-Regions([string] $png) {
    $file = [IO.Path]::ChangeExtension($png, ".regions.json")
    if (Test-Path $file) { @((Get-Content $file -Raw | ConvertFrom-Json).regions) } else { @() }
}

New-Item -ItemType Directory -Force $Out | Out-Null
$baselineRoot = (Resolve-Path $Baseline).Path
$rows = @()
$noise = @{}
$first = $true
# powershell -File passes "a,b" as one string
foreach ($candidate in $Candidates -split ",") {
    $candidateRoot = (Resolve-Path $candidate).Path
    $label = Split-Path $candidateRoot -Leaf
    foreach ($png in Get-ChildItem $baselineRoot -Recurse -Filter *.png) {
        $relative = $png.FullName.Substring($baselineRoot.Length).TrimStart("\")
        $other = Join-Path $candidateRoot $relative
        if (-not (Test-Path $other)) {
            $rows += [pscustomobject]@{ Candidate = $label; Shot = $relative; Missing = $true; Screen = 1.0; Ours = 1.0; Region = "(missing)"; Moved = ""; Diff = ""; Noise = $false }
            continue
        }

        # Our UI, where it is in the baseline; a region that moved is reported on its own
        $regions = @(Read-Regions $png.FullName)
        $otherRegions = @(Read-Regions $other)
        $moved = @($regions | Where-Object {
            $r = $_; $o = $otherRegions | Where-Object name -eq $r.name | Select-Object -First 1
            -not $o -or [math]::Abs($o.x - $r.x) -gt 2 -or [math]::Abs($o.y - $r.y) -gt 2 -or [math]::Abs($o.width - $r.width) -gt 2
        } | ForEach-Object name)
        $flat = [int[]]@($regions | ForEach-Object { [int]$_.x; [int]$_.y; [int]$_.width; [int]$_.height })

        $diffFile = Join-Path $Out ("diff\$label\" + $relative)
        New-Item -ItemType Directory -Force (Split-Path $diffFile) | Out-Null
        $shares = [ScreenshotDiff]::Compare($png.FullName, $other, $diffFile, $Threshold, $flat)
        $worst = 0.0; $worstName = ""
        for ($i = 0; $i -lt $regions.Count; $i++) { if ($shares[$i + 1] -ge $worst) { $worst = $shares[$i + 1]; $worstName = $regions[$i].name } }

        $row = [pscustomobject]@{
            Candidate = $label; Shot = $relative; Missing = $false; Screen = $shares[0]; Ours = $worst; Region = $worstName
            Moved = ($moved -join ", "); Diff = $diffFile; Base = $png.FullName; Other = $other; Noise = ($FirstIsNoise -and $first)
        }
        if ($row.Noise) { $noise[$relative] = $row }
        $rows += $row
    }
    $first = $false
}

# Changed: clearly more than the noise floor inside our UI, or our UI moved
foreach ($row in $rows) {
    $floor = if ($noise.ContainsKey($row.Shot)) { $noise[$row.Shot].Ours } else { 0 }
    $row | Add-Member Flag ($row.Missing -or $row.Moved -or ($row.Ours -gt [math]::Max(0.02, $floor * 2 + 0.01)))
}

function Href([string] $path) { if ($path) { "file:///" + ($path -replace "\\", "/" -replace " ", "%20") } }
$html = @("<!doctype html><meta charset=utf-8><title>Screenshot comparison</title>",
    "<style>body{font-family:system-ui;margin:16px;background:#111;color:#eee}table{border-collapse:collapse}td,th{padding:4px 8px;border-bottom:1px solid #333;text-align:left;vertical-align:top}img{width:420px}.flag{color:#ff6b6b;font-weight:bold}</style>",
    "<h1>Screenshot comparison</h1><p>Baseline: $baselineRoot. Threshold $Threshold. Changed pixels in our UI / whole screen; red = changed, cyan = our UI.</p><table>",
    "<tr><th>Candidate</th><th>Screenshot</th><th>Our UI</th><th>Screen</th><th>Moved</th><th>Baseline</th><th>Candidate</th><th>Diff</th></tr>")
foreach ($row in $rows | Sort-Object @{ e = { -not $_.Flag } }, @{ e = { -$_.Ours } }) {
    $class = if ($row.Flag) { " class=flag" } else { "" }
    $html += "<tr><td$class>$($row.Candidate)$(if ($row.Noise) { ' (noise)' })</td><td>$($row.Shot)</td><td$class>{0:P1} {1}</td><td>{2:P1}</td><td>$($row.Moved)</td><td><img src='$(Href $row.Base)'></td><td><img src='$(Href $row.Other)'></td><td><img src='$(Href $row.Diff)'></td></tr>" -f $row.Ours, $row.Region, $row.Screen
}
$html += "</table>"
$html -join "`n" | Set-Content (Join-Path $Out "report.html") -Encoding utf8
$rows | Select-Object Candidate, Shot, Flag, @{ n = "Ours"; e = { "{0:P1}" -f $_.Ours } }, Region, @{ n = "Screen"; e = { "{0:P1}" -f $_.Screen } }, Moved, Diff |
    ConvertTo-Json -Depth 3 | Set-Content (Join-Path $Out "comparison.json") -Encoding utf8
$rows | Where-Object Flag | Format-Table Candidate, Shot, @{ n = "Ours"; e = { "{0:P1}" -f $_.Ours } }, Region, @{ n = "Screen"; e = { "{0:P1}" -f $_.Screen } }, Moved -AutoSize | Out-String -Width 200
"{0} comparisons, {1} changed. Report: {2}" -f $rows.Count, @($rows | Where-Object Flag).Count, (Join-Path $Out "report.html")
