# Runs the in-game tests once per language, one game after the other, and prints a table of what passed and failed
# (#291). Each run sets CBC_LANGUAGE, so the game switches to the language before each test and back after it
# (Tests/TestLanguage.cs). The log of each run is kept outside the repo; the receipt (TestResults/ingame.json) is the
# one of the last run, which the game writes itself.
#
#   .github/scripts/Run-InGameTests.ps1 -Language Arabic -Suite full
#   .github/scripts/Run-InGameTests.ps1 -Language English,Norwegian,Japanese,Monkey
#   .github/scripts/Run-InGameTests.ps1 -Language All                    # the quick suite in every language
#   .github/scripts/Run-InGameTests.ps1 -Language Arabic -Tests RetryTest  # only these tests (CBC_TESTS)
#
# Build the mod first (dotnet build), which copies it into the game's Mods folder. Only one game may run at a time.
[CmdletBinding()]
param(
    # Languages by their name in the game (LocalizationTest.Languages), Monkey for its joke language, or All
    [Parameter(Mandatory)] [string[]] $Language,
    [ValidateSet("quick", "full")] [string] $Suite = "quick",
    # Tests to run (CBC_TESTS): with -Suite quick they come on top of the quick suite, with full they are all that runs
    [string[]] $Tests = @(),
    [string] $Game = "C:\Program Files\Epic Games\BloonsTD6",
    [string] $LogFolder = (Join-Path ([IO.Path]::GetTempPath()) "CBC-languages"),
    # Prints what would run without starting the game
    [switch] $DryRun
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "../..")

# One list of languages: LocalizationTest's, plus the joke language the mod has no translation for
function Get-GameLanguages {
    $source = Get-Content (Join-Path $root "Tests/LocalizationTest.cs") -Raw
    $list = [regex]::Match($source, 'Languages\s*=\s*\{(?<names>[^}]*)\}').Groups["names"].Value
    $names = [regex]::Matches($list, '"(\w+)"') | ForEach-Object { $_.Groups[1].Value }
    if (-not $names) { throw "No languages found in Tests/LocalizationTest.cs" }
    @($names) + "Monkey"
}

$languages = if ($Language -contains "All") { Get-GameLanguages } else { $Language }
$known = Get-GameLanguages
foreach ($name in $languages) {
    if ($name -notin $known) { throw "$name is not one of the languages: $($known -join ', ')" }
}

$exe = Join-Path $Game "BloonsTD6.exe"
$log = Join-Path $Game "MelonLoader/Latest.log"
$arguments = @("moddingDev", "-batchmode", "--modhelper.offline", "--modhelper.only=CustomBossChallenge;",
    "--modhelper.noaudio", "--modhelper.run", "-q", "test", "mod", "CustomBossChallenge")

if ($DryRun) {
    foreach ($name in $languages) {
        Write-Host "CBC_LANGUAGE=$name CBC_SUITE=$(if ($Suite -eq 'quick') { 'quick' }) CBC_TESTS=$($Tests -join ',')"
    }
    Write-Host "$exe $($arguments -join ' ')"
    return
}

if (-not (Test-Path $exe)) { throw "No game at $exe" }
New-Item -ItemType Directory -Force $LogFolder | Out-Null

$results = foreach ($name in $languages) {
    if (Get-Process BloonsTD6 -ErrorAction SilentlyContinue) {
        throw "BloonsTD6 is already running: only one game at a time. Stopped before $name."
    }

    $env:CBC_LANGUAGE = $name
    $env:CBC_SUITE = if ($Suite -eq "quick") { "quick" } else { $null }
    $env:CBC_TESTS = if ($Tests) { $Tests -join "," } else { $null }
    $started = Get-Date
    Write-Host "$($started.ToString('HH:mm')) $name ($Suite)..."
    try {
        Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $Game -Wait
    }
    finally {
        Remove-Item Env:CBC_LANGUAGE, Env:CBC_SUITE, Env:CBC_TESTS -ErrorAction SilentlyContinue
    }

    $kept = Join-Path $LogFolder "$name.log"
    Copy-Item $log $kept -Force
    $lines = Get-Content $kept
    $failed = @($lines | Select-String '\] CustomBossChallenge-(\w+) failed$' | ForEach-Object { $_.Matches[0].Groups[1].Value })
    # A test left out of the run is reported as succeeded too: only those that started count
    $ran = @($lines | Select-String '\[CustomBossChallenge\] (\w+): starting$' | ForEach-Object { $_.Matches[0].Groups[1].Value })
    $passed = @($lines | Select-String '\] CustomBossChallenge-(\w+) succeeded$' |
        Where-Object { $_.Matches[0].Groups[1].Value -in $ran }).Count
    $summary = ($lines | Select-String 'Ran \d+ test\(s\)' | Select-Object -Last 1)
    [pscustomobject]@{
        Language = $name
        Passed   = $passed
        Failed   = $failed.Count
        Minutes  = [math]::Round(((Get-Date) - $started).TotalMinutes)
        Tests    = if ($summary) { $failed -join ", " } else { "the run didn't finish, see $kept" }
    }
}

$results | Format-Table -AutoSize -Wrap
Write-Host "Logs: $LogFolder"
if ($results | Where-Object { $_.Failed -gt 0 -or $_.Tests -like "the run*" }) { exit 1 }
