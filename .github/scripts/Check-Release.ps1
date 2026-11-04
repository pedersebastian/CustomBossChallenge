# Checks a release tag before players get it: the tag must be a version like 2.2.1, match the Version in
# ModHelperData.cs (Mod Helper uses that to tell players about updates), and CHANGELOG.md must describe it, either in
# its own section or in the Unreleased section the release workflow turns into one, and TestResults/ingame.json must
# show a passing in-game test run of this version and code. Works in Windows PowerShell 5.1 and PowerShell 7.
param(
    [Parameter(Mandatory = $true)] [string] $Tag,
    [string] $Root
)

if (-not $Root) { $Root = Join-Path $PSScriptRoot "../.." }
$ErrorActionPreference = "Stop"
$errors = New-Object System.Collections.Generic.List[string]

function Report([string] $file, [string] $message) {
    $errors.Add("${file}: $message")
    Write-Host "::error file=$file::$message"
}

if ($Tag -notmatch '^\d+\.\d+\.\d+$') {
    Report "ModHelperData.cs" "tag '$Tag' is not a version like 2.2.1"
}

$data = [System.IO.File]::ReadAllText((Join-Path $Root "ModHelperData.cs"))
$match = [regex]::Match($data, 'const string Version = "([^"]+)"')
if (-not $match.Success) {
    Report "ModHelperData.cs" "no Version found"
} elseif ($match.Groups[1].Value -ne $Tag) {
    Report "ModHelperData.cs" "Version is $($match.Groups[1].Value) but the tag is $Tag; Mod Helper would show players the wrong version"
}

$changelog = [System.IO.File]::ReadAllText((Join-Path $Root "CHANGELOG.md"))
$ownSection = $changelog -match "(?m)^## \[$([regex]::Escape($Tag))\]"
$unreleased = [regex]::Match($changelog, '(?ms)^## \[Unreleased\]\s*$(.*?)(?=^## \[|\z)')
$hasUnreleased = $unreleased.Success -and ($unreleased.Groups[1].Value -match '(?m)^\s*-\s+\S')
if (-not $ownSection -and -not $hasUnreleased) {
    Report "CHANGELOG.md" "nothing under Unreleased and no section for $Tag, so the release would have no notes"
}

# The in-game tests can't run on GitHub, so a release needs their report: a passing run of every test, on this
# version and this code (TestRunReport.cs writes it, Get-SourceHash.ps1 says what "this code" is)
$reportFile = "TestResults/ingame.json"
$reportPath = Join-Path $Root $reportFile
$rerun = "run the in-game tests on a Debug build and commit $reportFile with the version (see CONTRIBUTING.md)"
if (-not (Test-Path $reportPath)) {
    Report $reportFile "missing: $rerun"
} else {
    $report = [System.IO.File]::ReadAllText($reportPath) | ConvertFrom-Json
    $failed = @($report.failed)
    if ($failed.Count -gt 0) {
        Report $reportFile "$($failed.Count) in-game test(s) failed: $($failed -join ', ')"
    }
    $skipped = @($report.skipped.PSObject.Properties | ForEach-Object { "$($_.Name) ($($_.Value))" })
    if ($skipped.Count -gt 0) {
        Report $reportFile "$($skipped.Count) in-game test(s) skipped, so this wasn't a full run: $($skipped -join ', ')"
    }
    if ($report.modVersion -ne $Tag) {
        Report $reportFile "the in-game tests ran on version $($report.modVersion), not $Tag; $rerun"
    }
    $sourceHash = & (Join-Path $PSScriptRoot "Get-SourceHash.ps1") -Root $Root
    if ($report.sourceHash -ne $sourceHash) {
        Report $reportFile "the mod's code changed after the in-game tests ran (commit $($report.commit)); $rerun"
    }
}

if ($errors.Count -gt 0) {
    Write-Host "$($errors.Count) release problem(s)"
    exit 1
}
Write-Host "Release $Tag matches ModHelperData.cs, CHANGELOG.md and a passing in-game test run ($($report.passed) tests, game $($report.gameVersion))"
