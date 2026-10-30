<#
.SYNOPSIS
Downloads the latest release DLL of every mod in compat-candidates.json, for the compatibility tests.

.DESCRIPTION
Each mod goes into its own folder under -Out, named after its repository. The DLLs are third-party code: this script
only downloads them and never loads them. They run only inside the game, started by Test-ModCompatibility.ps1.
Prints name, size and SHA-256 of every file, and writes them to <Out>\mods.json. Set GITHUB_TOKEN to avoid GitHub's
rate limit for anonymous requests.

.EXAMPLE
.github/scripts/Get-CompatMods.ps1 -Out $env:TEMP\cbc-compat\mods
#>
param(
    [Parameter(Mandatory)] [string] $Out,
    [string] $Candidates
)

$ErrorActionPreference = "Stop"
if (-not $Candidates) { $Candidates = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "compat-candidates.json" }
$headers = @{ "User-Agent" = "CustomBossChallenge-compat"; "Accept" = "application/vnd.github+json" }
if ($env:GITHUB_TOKEN) { $headers["Authorization"] = "Bearer $env:GITHUB_TOKEN" }

New-Item -ItemType Directory -Force $Out | Out-Null
$list = (Get-Content $Candidates -Raw | ConvertFrom-Json).mods
$found = @()
foreach ($mod in $list) {
    $folder = Join-Path $Out ($mod.repo -replace "/", "__")
    New-Item -ItemType Directory -Force $folder | Out-Null
    try {
        $release = Invoke-RestMethod -Headers $headers "https://api.github.com/repos/$($mod.repo)/releases/latest"
    } catch {
        Write-Warning "$($mod.repo): no release ($($_.Exception.Message))"
        continue
    }

    foreach ($asset in $release.assets | Where-Object { $_.name -like "*.dll" }) {
        $path = Join-Path $folder $asset.name
        if (-not (Test-Path $path) -or (Get-Item $path).Length -ne $asset.size) {
            Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $path -UseBasicParsing
        }

        $hash = (Get-FileHash $path -Algorithm SHA256).Hash
        $found += [pscustomobject]@{
            repo = $mod.repo; group = $mod.group; tag = $release.tag_name; published = $release.published_at
            dll = $asset.name; path = $path; size = (Get-Item $path).Length; sha256 = $hash
        }
        "{0,-36} {1,-12} {2,-28} {3,10:N0} B  {4}" -f $mod.repo, $release.tag_name, $asset.name, (Get-Item $path).Length, $hash
    }
}

$found | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $Out "mods.json") -Encoding utf8
"{0} DLLs in {1}" -f $found.Count, $Out
