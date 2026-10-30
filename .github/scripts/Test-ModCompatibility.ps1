<#
.SYNOPSIS
Runs this mod's in-game tests together with one other mod at a time (or a set of mods), and reports what breaks.

.DESCRIPTION
The compatibility tests of issue #24. Needs the game; it can't run on GitHub. For every run it:
  1. copies the other mod(s) from -ModsFolder (made by Get-CompatMods.ps1) into the game's Mods folder,
  2. starts the game headless with only those mods and runs this mod's tests, with a timeout,
  3. removes exactly what it added, and checks that the Mods folder is as it was,
  4. reads Latest.log: tests run and failed, load errors, PatchCheck errors and the CBC-CONFLICT lines of
     ModConflictTest, and keeps a copy of the log,
  5. reruns failing tests once on their own; only failures that come back count.
The game's profile is copied to <Results>\profile-backup before the first run, so it can be put back by hand.

.PARAMETER Mods
Repositories or DLL names from mods.json, "A", "B" or "all". Each entry is one run; join names with "+" to run
several mods together, e.g. "FasterForward+UsefulUtilities+BuffsInShop". "none" runs this mod alone.

.EXAMPLE
.github/scripts/Test-ModCompatibility.ps1 -ModsFolder $env:TEMP\cbc-compat\mods -Results $env:TEMP\cbc-compat\runs -Mods none,A
#>
param(
    [Parameter(Mandatory)] [string] $ModsFolder,
    [Parameter(Mandatory)] [string] $Results,
    [string[]] $Mods = @("none"),
    [string] $Game = "C:\Program Files\Epic Games\BloonsTD6",
    [int] $TimeoutMinutes = 20,
    [string] $Tests = "",
    [switch] $NoRerun,
    # Runs the ScreenshotTour in a windowed game instead of the tests, into <Results>\screenshots\<run>
    [switch] $Screenshots,
    [string] $Resolutions = ""
)

$ErrorActionPreference = "Stop"
$ourName = "CustomBossChallenge"
$gameMods = Join-Path $Game "Mods"
$log = Join-Path $Game "MelonLoader\Latest.log"
$settings = Join-Path $Game "Btd6ModHelper\Mod Settings"
New-Item -ItemType Directory -Force $Results, (Join-Path $Results "logs") | Out-Null
# Windows PowerShell's ConvertFrom-Json passes an array on as one object; ForEach-Object unrolls it
$catalog = @(Get-Content (Join-Path $ModsFolder "mods.json") -Raw | ConvertFrom-Json | ForEach-Object { $_ })

function Find-Mod([string] $name) {
    $found = @($catalog | Where-Object {
        $_.repo -eq $name -or ($_.repo -split "/")[1] -eq $name -or [IO.Path]::GetFileNameWithoutExtension($_.dll) -eq $name
    })
    if ($found.Count -eq 0) { throw "Unknown mod '$name', not in $ModsFolder\mods.json" }
    $found[0]
}

# Which runs: every entry is a list of mods to load together
# powershell -File passes "a,b" as one string
$runs = foreach ($entry in $Mods -split ",") {
    switch ($entry) {
        "none" { , @() }
        "all" { foreach ($m in $catalog) { , @($m) } }
        { $_ -in "A", "B" } { foreach ($m in $catalog | Where-Object group -eq $entry) { , @($m) } }
        default { , @($entry -split "\+" | ForEach-Object { Find-Mod $_ }) }
    }
}

function Get-Snapshot([string] $folder) {
    Get-ChildItem $folder -File | ForEach-Object { "{0}|{1}" -f $_.Name, (Get-FileHash $_.FullName -Algorithm SHA256).Hash } | Sort-Object
}

function Get-RunName($set) {
    if ($set.Count -eq 0) { return "none" }
    ($set | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.dll) }) -join "+"
}

function Start-Game([string] $only, [string] $testFilter, [bool] $windowed) {
    $arguments = @("moddingDev")
    if (-not $windowed) { $arguments += "-batchmode" }
    $arguments += "--modhelper.offline", "--modhelper.only=$only", "--modhelper.noaudio", "--modhelper.run", "-q", "test", "mod", $ourName
    if ($testFilter) { $env:CBC_TESTS = $testFilter } else { Remove-Item env:CBC_TESTS -ErrorAction SilentlyContinue }
    $started = Get-Date
    $process = Start-Process -FilePath (Join-Path $Game "BloonsTD6.exe") -WorkingDirectory $Game -ArgumentList $arguments -PassThru
    $timedOut = -not $process.WaitForExit($TimeoutMinutes * 60 * 1000)
    if ($timedOut) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 5
    }
    Remove-Item env:CBC_TESTS -ErrorAction SilentlyContinue
    [pscustomobject]@{ TimedOut = $timedOut; Minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1) }
}

function Read-Log([string] $path) {
    $text = if (Test-Path $path) { Get-Content $path -Raw -Encoding utf8 } else { "" }
    $ran = [regex]::Match($text, "Ran (\d+) test\(s\), (\d+) succeeded, (\d+) failed")
    [pscustomobject]@{
        Ran = if ($ran.Success) { [int]$ran.Groups[1].Value } else { $null }
        Succeeded = if ($ran.Success) { [int]$ran.Groups[2].Value } else { $null }
        Failed = if ($ran.Success) { [int]$ran.Groups[3].Value } else { $null }
        FailedTests = @([regex]::Matches($text, "Test (\w+) failed|(\w+Test)\b[^\r\n]*\bfailed\b") |
            ForEach-Object { if ($_.Groups[1].Success) { $_.Groups[1].Value } else { $_.Groups[2].Value } } | Sort-Object -Unique)
        LoadErrors = [regex]::Match($text, "Tests not run, mod has (\d+) load errors?").Value
        PatchCheck = @([regex]::Matches($text, "The Boss Creator is turned off[^\r\n]*") | ForEach-Object Value)
        Conflicts = @([regex]::Matches($text, "CBC-CONFLICT\|[^\r\n]*") | ForEach-Object Value | Sort-Object -Unique)
        Warnings = @([regex]::Matches($text, "also patches [^\r\n]*") | ForEach-Object Value | Sort-Object -Unique)
        Exceptions = @([regex]::Matches($text, "(?m)^.*Exception[^\r\n]*") | ForEach-Object Value | Select-Object -First 15)
    }
}

# The profile, so a mod that changes it can be undone by hand
$profileRoot = Join-Path $env:USERPROFILE "AppData\LocalLow\Ninja Kiwi"
$backup = Join-Path $Results "profile-backup"
if (-not (Test-Path $backup) -and (Test-Path $profileRoot)) {
    Get-ChildItem $profileRoot -Directory | ForEach-Object {
        Get-ChildItem $_.FullName -Directory | Where-Object { Test-Path (Join-Path $_.FullName "link") } | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $backup $_.Name) -Recurse
        }
    }
    "Profile copied to $backup"
}

$summary = Join-Path $Results "results.jsonl"
foreach ($set in $runs) {
    $name = Get-RunName $set
    $only = (@($ourName) + @($set | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.dll) })) -join ";"
    $before = Get-Snapshot $gameMods
    $settingsBefore = @(Get-ChildItem $settings -File -ErrorAction SilentlyContinue | ForEach-Object Name)
    $added = @()
    "== $name"
    try {
        foreach ($m in $set) {
            $target = Join-Path $gameMods $m.dll
            if (Test-Path $target) { continue }
            Copy-Item $m.path $target
            $added += $target
        }

        if ($Screenshots) {
            $env:CBC_SCREENSHOTS = Join-Path $Results "screenshots\$name"
            if ($Resolutions) { $env:CBC_RESOLUTIONS = $Resolutions }
            $run = Start-Game "$only;" "ScreenshotTour" $true
            Remove-Item env:CBC_SCREENSHOTS, env:CBC_RESOLUTIONS -ErrorAction SilentlyContinue
        } else {
            $run = Start-Game "$only;" $Tests $false
        }
        Copy-Item $log (Join-Path $Results "logs\$name.log") -ErrorAction SilentlyContinue
        $found = Read-Log $log

        $rerun = $null
        if (-not $NoRerun -and -not $Screenshots -and $found.FailedTests.Count -gt 0) {
            $again = Start-Game "$only;" ($found.FailedTests -join ",") $false
            Copy-Item $log (Join-Path $Results "logs\$name.rerun.log") -ErrorAction SilentlyContinue
            $rerun = Read-Log $log
            $rerun | Add-Member TimedOut $again.TimedOut
        }
    } finally {
        foreach ($file in $added) { Remove-Item $file -Force -ErrorAction SilentlyContinue }
        Get-ChildItem $settings -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -notin $settingsBefore } |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }

    $after = Get-Snapshot $gameMods
    if (Compare-Object $before $after) { throw "The Mods folder changed during the run of $name, stopping" }

    $failedTwice = if ($rerun) { @($found.FailedTests | Where-Object { $_ -in $rerun.FailedTests -or $rerun.TimedOut }) } else { $found.FailedTests }
    $result =
        if ($run.TimedOut) { "timeout" }
        elseif ($found.LoadErrors) { "load errors" }
        elseif ($null -eq $found.Ran) { "tests didn't run" }
        elseif ($found.PatchCheck.Count -gt 0) { "Boss Creator turned off" }
        elseif ($failedTwice.Count -gt 0) { "tests fail" }
        elseif ($found.FailedTests.Count -gt 0) { "OK (flaky: $($found.FailedTests -join ', '))" }
        else { "OK" }

    $row = [ordered]@{
        run = $name; mods = @($set | ForEach-Object { "$($_.repo)@$($_.tag)" }); result = $result; minutes = $run.Minutes
        ran = $found.Ran; failed = $found.Failed; failedTests = $failedTwice; firstFailures = $found.FailedTests
        loadErrors = $found.LoadErrors; patchCheck = $found.PatchCheck; conflicts = $found.Conflicts
        warnings = $found.Warnings; exceptions = $found.Exceptions
    }
    ($row | ConvertTo-Json -Compress -Depth 4) | Add-Content $summary -Encoding utf8
    "{0}: {1} ({2} min, ran {3}, failed {4}, {5} overlapping patches)" -f $name, $result, $run.Minutes, $found.Ran, $found.Failed, $found.Conflicts.Count
}
