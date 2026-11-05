# Prints a SHA-256 of the mod's own source: the files that change what players get, not docs, the website, CI or the
# tests. Debug builds embed it (CustomBossChallenge.csproj), the in-game tests write it to TestResults/ingame.json and
# Check-Release.ps1 compares it with the tagged code, so a release is refused when the code changed after the tests
# ran. Text files count with LF line endings, so a Windows checkout and CI's Linux checkout agree. Works in Windows
# PowerShell 5.1 and PowerShell 7.
param(
    [string] $Root
)

if (-not $Root) { $Root = Join-Path $PSScriptRoot "../.." }
$ErrorActionPreference = "Stop"
$Root = (Resolve-Path $Root).Path.TrimEnd('\', '/')

# Never looked into: build output and tools anywhere, and the folders at the top that aren't the mod
$skipAnywhere = @("bin", "obj", ".git", ".vs", ".idea", "node_modules")
$skipAtTop = @("Tests", "UnitTests", "PropertyTests", "web", "UnityProject", "docs", "tools", ".github", ".claude")

function Test-Counts([string] $relative) {
    $extension = [System.IO.Path]::GetExtension($relative).ToLowerInvariant()
    switch ($extension) {
        ".cs" { return $true }
        ".csproj" { return $true }
        ".json" { return $relative.StartsWith("Localization/") }
        ".bundle" { return $relative.StartsWith("Bundles/") }
    }
    return $false
}

$files = New-Object System.Collections.Generic.List[string]
function Add-Folder([string] $folder, [string] $prefix) {
    foreach ($directory in [System.IO.Directory]::GetDirectories($folder)) {
        $name = [System.IO.Path]::GetFileName($directory)
        if ($skipAnywhere -contains $name) { continue }
        if ($prefix -eq "" -and $skipAtTop -contains $name) { continue }
        Add-Folder $directory "$prefix$name/"
    }
    foreach ($file in [System.IO.Directory]::GetFiles($folder)) {
        $relative = $prefix + [System.IO.Path]::GetFileName($file)
        if (Test-Counts $relative) { $files.Add($relative) }
    }
}
Add-Folder $Root ""
$files.Sort([System.StringComparer]::Ordinal)

$sha = [System.Security.Cryptography.SHA256]::Create()
function Get-Hex([byte[]] $bytes) {
    return -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") })
}

$lines = New-Object System.Text.StringBuilder
foreach ($relative in $files) {
    $bytes = [System.IO.File]::ReadAllBytes((Join-Path $Root $relative))
    if (-not $relative.EndsWith(".bundle")) {
        # CRLF -> LF, the way git stores the file
        $kept = New-Object System.Collections.Generic.List[byte] $bytes.Length
        for ($i = 0; $i -lt $bytes.Length; $i++) {
            if ($bytes[$i] -eq 13 -and $i + 1 -lt $bytes.Length -and $bytes[$i + 1] -eq 10) { continue }
            $kept.Add($bytes[$i])
        }
        $bytes = $kept.ToArray()
    }
    [void] $lines.Append("$(Get-Hex $bytes)  $relative`n")
}

Write-Output (Get-Hex ([System.Text.Encoding]::UTF8.GetBytes($lines.ToString())))
