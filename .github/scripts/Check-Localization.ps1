# Checks the translations without the game: every Localization/*.json must be valid JSON without duplicate keys, have
# exactly the English keys in the same order, keep every {0} placeholder and leave no text empty. Every text in
# Menus/Texts.cs must have an English entry. Works in Windows PowerShell 5.1 and PowerShell 7.
param([string] $Root)

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
