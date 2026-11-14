# Checks the translations without the game: every Localization/*.json must be valid JSON without duplicate keys, have
# exactly the English keys in the same order, keep every {0} placeholder and leave no text empty. Every text in
# Menus/Texts.cs must have an English entry. Works in Windows PowerShell 5.1 and PowerShell 7.
#
# Localization/english-sources.txt has, for every key and language, a short hash of the English text the translation
# was made from (#172), so a changed English text without the other languages fails here. After translating, run
#   .github/scripts/Check-Localization.ps1 -UpdateSources [-Language German,French]
# to record the English texts the given languages (all of them without -Language) are now translated from.
param([string] $Root, [switch] $UpdateSources, [string[]] $Language)

if (-not $Root) { $Root = Join-Path $PSScriptRoot "../.." }
$ErrorActionPreference = "Stop"
$errors = New-Object System.Collections.Generic.List[string]

function Report([string] $file, [string] $message) {
    $errors.Add("${file}: $message")
    Write-Host "::error file=$file::$message"
}

function Read-Translation([string] $path) {
    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $names = [regex]::Matches($text, '(?m)^\s*"((?:[^"\\]|\\.)*)"\s*:') | ForEach-Object { $_.Groups[1].Value }
    $duplicates = $names | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name }
    $json = $text | ConvertFrom-Json
    $table = [ordered]@{}
    foreach ($property in $json.PSObject.Properties) { $table[$property.Name] = [string] $property.Value }
    return @{ Table = $table; Duplicates = @($duplicates) }
}

function Placeholders([string] $text) {
    ([regex]::Matches($text, '\{\d+\}') | ForEach-Object { $_.Value } | Sort-Object) -join ","
}

$folder = Join-Path $Root "Localization"
$englishPath = Join-Path $folder "English.json"
$english = (Read-Translation $englishPath).Table
$keys = @($english.Keys)

$files = Get-ChildItem $folder -Filter *.json | Sort-Object Name
foreach ($file in $files) {
    $relative = "Localization/$($file.Name)"
    try {
        $translation = Read-Translation $file.FullName
    } catch {
        $reason = ($_.Exception.Message -split "`r?`n")[0]
        if ($reason.Length -gt 200) { $reason = $reason.Substring(0, 200) + "..." }
        Report $relative "not valid JSON: $reason"
        continue
    }

    foreach ($duplicate in $translation.Duplicates) { Report $relative "duplicate key $duplicate" }
    $table = $translation.Table
    $missing = $keys | Where-Object { -not $table.Contains($_) }
    $extra = @($table.Keys) | Where-Object { -not $english.Contains($_) }
    foreach ($key in $missing) { Report $relative "missing $key" }
    foreach ($key in $extra) { Report $relative "unknown key $key (not in English.json)" }
    if (-not $missing -and -not $extra -and ((@($table.Keys) -join "|") -ne ($keys -join "|"))) {
        Report $relative "keys are not in the same order as English.json"
    }

    foreach ($key in $keys) {
        if (-not $table.Contains($key)) { continue }
        if ([string]::IsNullOrWhiteSpace($table[$key])) { Report $relative "$key is empty" }
        $expected = Placeholders $english[$key]
        $actual = Placeholders $table[$key]
        if ($actual -ne $expected) { Report $relative "$key has placeholders '$actual', English has '$expected'" }
    }
}

# Every translation must be made from the English text as it is now
$sourcesPath = Join-Path $folder "english-sources.txt"
$sourcesFile = "Localization/english-sources.txt"
$sha = [System.Security.Cryptography.SHA256]::Create()
function Hash([string] $text) {
    $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text))
    (($bytes[0..3] | ForEach-Object { $_.ToString("x2") }) -join "")
}
$languages = @($files | ForEach-Object { $_.BaseName } | Where-Object { $_ -ne "English" })
$current = @{}
foreach ($key in $keys) { $current[$key] = Hash $english[$key] }

# Map "language|key" -> hash, from the file
$sources = @{}
if (Test-Path $sourcesPath) {
    $lines = @([System.IO.File]::ReadAllLines($sourcesPath) | Where-Object { $_ -and -not $_.StartsWith("#") })
    $header = @($lines[0] -split "`t")
    foreach ($line in ($lines | Select-Object -Skip 1)) {
        $cells = @($line -split "`t")
        for ($i = 1; $i -lt [Math]::Min($cells.Count, $header.Count); $i++) {
            $sources["$($header[$i])|$($cells[0])"] = $cells[$i]
        }
    }
}

if ($UpdateSources) {
    $update = if ($Language) { $Language } else { $languages }
    foreach ($name in $update) {
        if ($languages -notcontains $name) { throw "No Localization/$name.json" }
        foreach ($key in $keys) { $sources["$name|$($key -replace '^CustomBossChallenge-', '')"] = $current[$key] }
    }
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add("# The English text each translation was made from, as the first 8 hex digits of the SHA-256 of its UTF-8, tab-separated.")
    $out.Add("# Written by .github/scripts/Check-Localization.ps1 -UpdateSources after translating; see CONTRIBUTING.md.")
    $out.Add((@("Key") + $languages) -join "`t")
    foreach ($key in $keys) {
        $short = $key -replace '^CustomBossChallenge-', ''
        $row = @($short) + @($languages | ForEach-Object {
            $value = $sources["$_|$short"]
            if ($value) { $value } else { "-" }
        })
        $out.Add($row -join "`t")
    }
    [System.IO.File]::WriteAllText($sourcesPath, (($out -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding $false))
    Write-Host "$sourcesFile updated for $($update -join ', ')"
}

if (-not (Test-Path $sourcesPath)) {
    Report $sourcesFile "missing, run .github/scripts/Check-Localization.ps1 -UpdateSources"
} else {
    foreach ($name in $languages) {
        $stale = @($keys | Where-Object { $sources["$name|$($_ -replace '^CustomBossChallenge-', '')"] -ne $current[$_] })
        foreach ($key in $stale) {
            Report "Localization/$name.json" ("$key was translated from another English text; translate it again, then " +
                "run .github/scripts/Check-Localization.ps1 -UpdateSources -Language $name")
        }
    }
}

# Every text the mod uses must exist in English
$texts = [System.IO.File]::ReadAllText((Join-Path $Root "Menus/Texts.cs"))
foreach ($match in [regex]::Matches($texts, 'Key\(nameof\((\w+)\)')) {
    $key = "CustomBossChallenge-$($match.Groups[1].Value)"
    if (-not $english.Contains($key)) { Report "Localization/English.json" "missing $key, used in Menus/Texts.cs" }
}

if ($errors.Count -gt 0) {
    Write-Host "$($errors.Count) translation problem(s)"
    exit 1
}
Write-Host "$($files.Count) translations with $($keys.Count) keys each are complete"
