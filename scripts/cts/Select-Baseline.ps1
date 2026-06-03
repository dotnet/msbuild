<#
.SYNOPSIS
    Selects the most recent CTS baseline snapshot that is an ancestor of
    the current commit, and downloads it.

.DESCRIPTION
    The CTS apply pipeline calls this helper after `actions/checkout` (with
    full history). It enumerates artifacts produced by the daily CTS collect
    pipeline, filters them by OS, and walks `git rev-list HEAD` to pick the
    first SHA that has a published baseline. The chosen baseline is
    downloaded to $DownloadDir and a flat metadata file is written to
    $MetadataPath so the calling yaml can read it back.

    Designed to run under PowerShell 7 on both Windows and Linux agents.

.PARAMETER Organization
    Azure DevOps organization URL, e.g. https://dev.azure.com/devdiv.

.PARAMETER Project
    Azure DevOps project name, e.g. DevDiv.

.PARAMETER CollectPipelineId
    Numeric pipeline definition id of azure-pipelines/cts-collect.yml.

.PARAMETER Os
    "windows" or "linux". Used to filter the per-OS artifact names produced
    by the collect pipeline.

.PARAMETER DownloadDir
    Directory the chosen baseline artifact is extracted into.

.PARAMETER MetadataPath
    Where to write the JSON describing the selected baseline. Schema:

        {
          "baselineSha":        "<sha or null>",
          "baselineBuildId":    "<int or null>",
          "baselineArtifact":   "<name or null>",
          "baselineAgeMinutes": <int or null>,
          "fallbackReason":     "<string or null>"
        }

.PARAMETER MaxBuilds
    How many of the most recent successful main-branch collect runs to look
    at. Defaults to 30 (matches the 14-day retention with daily runs and
    leaves a margin for re-runs).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Organization,
    [Parameter(Mandatory)] [string] $Project,
    [Parameter(Mandatory)] [int]    $CollectPipelineId,
    [Parameter(Mandatory)] [ValidateSet('windows','linux')] [string] $Os,
    [Parameter(Mandatory)] [string] $DownloadDir,
    [Parameter(Mandatory)] [string] $MetadataPath,
    [int] $MaxBuilds = 30
)

$ErrorActionPreference = 'Stop'

function Write-Metadata {
    param($obj)
    $dir = Split-Path -Parent $MetadataPath
    if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $obj | ConvertTo-Json -Depth 4 | Set-Content -Path $MetadataPath -Encoding utf8
    Write-Host "##[group]CTS baseline selection"
    $obj | ConvertTo-Json -Depth 4 | Write-Host
    Write-Host "##[endgroup]"
}

function Emit-Fallback($reason) {
    Write-Host "##vso[task.logissue type=warning]CTS baseline fallback: $reason"
    Write-Metadata @{
        baselineSha        = $null
        baselineBuildId    = $null
        baselineArtifact   = $null
        baselineAgeMinutes = $null
        fallbackReason     = $reason
    }
    exit 0
}

# 1. List recent successful main runs of the collect pipeline.
Write-Host "Querying recent runs for pipeline $CollectPipelineId on main..."
$runsJson = az pipelines runs list `
    --organization $Organization --project $Project `
    --pipeline-ids $CollectPipelineId `
    --branch main `
    --status completed --result succeeded `
    --top $MaxBuilds --query-order FinishTimeDesc `
    --output json 2>$null
if ($LASTEXITCODE -ne 0 -or -not $runsJson) { Emit-Fallback 'collect-pipeline-list-failed' }
$runs = $runsJson | ConvertFrom-Json
if (-not $runs) { Emit-Fallback 'no-collect-runs' }

# 2. For each run, list artifacts and pick those tagged for our OS.
#    Artifacts are named "cts-baseline-<sha>-<os>" by cts-collect.yml.
$artifactSuffix = "-$Os"
$shaToBuild = @{}
foreach ($r in $runs) {
    $arts = az pipelines runs artifact list `
        --organization $Organization --project $Project `
        --run-id $r.id --output json 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $arts) { continue }
    foreach ($a in ($arts | ConvertFrom-Json)) {
        if ($a.name -notlike "cts-baseline-*$artifactSuffix") { continue }
        # cts-baseline-<sha>-<os>  ->  <sha>
        $sha = $a.name.Substring('cts-baseline-'.Length)
        $sha = $sha.Substring(0, $sha.Length - $artifactSuffix.Length)
        if (-not $shaToBuild.ContainsKey($sha)) {
            $shaToBuild[$sha] = @{ BuildId = $r.id; ArtifactName = $a.name; FinishTime = $r.finishTime }
        }
    }
}
if ($shaToBuild.Count -eq 0) { Emit-Fallback 'no-baseline-artifacts-for-os' }

# 3. Walk PR commits oldest→newest is wrong: we want the most recent
#    ancestor, so traverse `git rev-list HEAD` (which yields newest first).
$commits = git rev-list HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or -not $commits) { Emit-Fallback 'git-rev-list-failed' }

$chosenSha = $null
foreach ($c in $commits) {
    if ($shaToBuild.ContainsKey($c)) { $chosenSha = $c; break }
}
if (-not $chosenSha) { Emit-Fallback 'no-ancestor-snapshot' }

$chosen = $shaToBuild[$chosenSha]
Write-Host "Selected baseline SHA $chosenSha (build $($chosen.BuildId), artifact $($chosen.ArtifactName))"

# 4. Download the artifact.
New-Item -ItemType Directory -Force -Path $DownloadDir | Out-Null
az pipelines runs artifact download `
    --organization $Organization --project $Project `
    --run-id $chosen.BuildId --artifact-name $chosen.ArtifactName `
    --path $DownloadDir | Out-Host
if ($LASTEXITCODE -ne 0) { Emit-Fallback 'snapshot-download-failed' }

# 5. Compute age in minutes (best effort; falls back to null on parse error).
$ageMinutes = $null
try {
    $finish = [DateTimeOffset]::Parse($chosen.FinishTime)
    $ageMinutes = [int]([DateTimeOffset]::UtcNow - $finish).TotalMinutes
} catch { }

Write-Metadata @{
    baselineSha        = $chosenSha
    baselineBuildId    = $chosen.BuildId
    baselineArtifact   = $chosen.ArtifactName
    baselineAgeMinutes = $ageMinutes
    fallbackReason     = $null
}
