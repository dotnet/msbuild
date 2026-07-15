<#
.SYNOPSIS
    Deterministically resolves the PackageValidationBaselineVersion for a release's main-bump (Phase 3.2).

.DESCRIPTION
    The ApiCompat baseline for the bumped `main` is the latest `<THIS>.0-preview-NNNNN-NN` MSBuild
    package that is BOTH reachable from `vs<THIS>` AND published on the public dotnet-tools feed.

    This script computes it without manual pipeline spelunking:
      1. Finds the branch point: `git merge-base <MainRef> <ReleaseRef>`.
      2. Queries the MSBuild official build pipeline (9434, devdiv) for the successful build at that
         commit (and any successful pre-stabilization preview builds on `vs<THIS>`).
      3. Derives each build's package version from its OfficialBuildId via the Arcade date encoding
         `shortDate = (yy * 1000) + (month * 50) + day`, revision zero-padded to 2 digits.
      4. Verifies the candidate is on the dotnet-tools feed; picks the latest verified `<THIS>.0-preview-*`.

    Requires `az login` with access to the devdiv Azure DevOps organization (see release skill memory).
    See documentation/release-checklist.md (Phase 3.2) and the release skill SKILL.md.

.PARAMETER ThisReleaseVersion
    The release being shipped, e.g. "18.9". Defaults to the major.minor of <ReleaseRef>'s VersionPrefix.

.PARAMETER MainRef
    Git ref for main. Default: origin/main.

.PARAMETER ReleaseRef
    Git ref for the release branch. Default: origin/vs<ThisReleaseVersion>.

.EXAMPLE
    ./Get-PackageValidationBaseline.ps1 -ThisReleaseVersion 18.9
    Prints e.g. 18.9.0-preview-26330-01
#>

[CmdletBinding()]
param(
    [string]$ThisReleaseVersion,
    [string]$MainRef = 'origin/main',
    [string]$ReleaseRef
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

$PipelineId = 9434
$DevDivOrg = 'https://devdiv.visualstudio.com/DevDiv'
$AzureDevOpsResource = '499b84ac-1321-427f-aa17-267ca6975798'
$ToolsFeedBase = 'https://feeds.dev.azure.com/dnceng/public/_apis/packaging/feeds/dotnet-tools'

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }

# Resolve refs / version.
if (-not $ThisReleaseVersion) {
    if (-not $ReleaseRef) { throw "Provide -ThisReleaseVersion (e.g. 18.9) or -ReleaseRef." }
    $props = & git show "${ReleaseRef}:eng/Versions.props"
    if ($props -join "`n" -match '<VersionPrefix>(\d+\.\d+)\.\d+</VersionPrefix>') {
        $ThisReleaseVersion = $Matches[1]
    } else {
        throw "Could not read VersionPrefix from ${ReleaseRef}:eng/Versions.props."
    }
}
if (-not $ReleaseRef) { $ReleaseRef = "origin/vs$ThisReleaseVersion" }

Write-Info "Release: $ThisReleaseVersion   main=$MainRef   release=$ReleaseRef"

# 1. Branch point.
$mergeBase = (& git merge-base $MainRef $ReleaseRef).Trim()
if (-not $mergeBase) { throw "git merge-base $MainRef $ReleaseRef returned nothing (fetch the refs first?)." }
Write-Info "Branch point (merge-base): $mergeBase"

# 2. Auth + pipeline query.
$token = (& az account get-access-token --resource $AzureDevOpsResource --query accessToken -o tsv 2>$null)
if (-not $token) { throw "Could not get an Azure DevOps token. Run 'az login' with devdiv access." }
$headers = @{ Authorization = "Bearer $token" }

function Get-Builds($branch) {
    $u = "$DevDivOrg/_apis/build/builds?definitions=$PipelineId&branchName=refs/heads/$branch&api-version=7.0&`$top=50"
    (Invoke-RestMethod -Uri $u -Headers $headers).value
}

# Arcade date encoding -> package version. OfficialBuildId build number is 'yyyyMMdd.rev'.
function Get-VersionFromBuildNumber([string]$buildNumber) {
    if ($buildNumber -notmatch '^(\d{4})(\d{2})(\d{2})\.(\d+)$') { return $null }
    $yy = [int]$Matches[1] % 100
    $mm = [int]$Matches[2]
    $dd = [int]$Matches[3]
    $rev = [int]$Matches[4]
    $shortDate = ($yy * 1000) + ($mm * 50) + $dd
    $revStr = if ($rev -lt 100) { '{0:D2}' -f $rev } else { "$rev" }
    return "$ThisReleaseVersion.0-preview-$shortDate-$revStr"
}

function Test-Succeeded($build) {
    return ($build.PSObject.Properties.Match('result').Count -gt 0) -and ($build.result -eq 'succeeded')
}

# Collect candidate builds: the merge-base build on main, plus pre-stabilization preview builds on the release branch.
$candidates = [System.Collections.Generic.List[object]]::new()

$mainBuilds = Get-Builds 'main'
$mbBuild = $mainBuilds | Where-Object { $_.sourceVersion -eq $mergeBase -and (Test-Succeeded $_) } | Select-Object -First 1
if ($mbBuild) {
    $candidates.Add([pscustomobject]@{ Source = "main@merge-base"; BuildNumber = $mbBuild.buildNumber; Finish = $mbBuild.finishTime })
} else {
    Write-Warning "No successful main build found at the merge-base SHA in the most recent $PipelineId runs; widen the search if needed."
}

$relBuilds = Get-Builds "vs$ThisReleaseVersion"
foreach ($b in ($relBuilds | Where-Object { Test-Succeeded $_ })) {
    $v = Get-VersionFromBuildNumber $b.buildNumber
    if ($v -and $v -like "$ThisReleaseVersion.0-preview-*") {
        $candidates.Add([pscustomobject]@{ Source = "vs$ThisReleaseVersion"; BuildNumber = $b.buildNumber; Finish = $b.finishTime })
    }
}

if ($candidates.Count -eq 0) { throw "No eligible candidate builds found for $ThisReleaseVersion." }

# 3. Compute versions.
foreach ($c in $candidates) { $c | Add-Member NoteProperty Version (Get-VersionFromBuildNumber $c.BuildNumber) }

# 4. Verify on the dotnet-tools feed.
$pkgId = (Invoke-RestMethod -Uri "$ToolsFeedBase/packages?packageNameQuery=Microsoft.Build&api-version=7.1-preview.1" -Headers $headers).value |
    Where-Object { $_.name -eq 'Microsoft.Build' } | Select-Object -First 1
if (-not $pkgId) { throw "Microsoft.Build not found on the dotnet-tools feed." }
$feedVersions = (Invoke-RestMethod -Uri "$ToolsFeedBase/packages/$($pkgId.id)/versions?api-version=7.1-preview.1" -Headers $headers).value |
    Where-Object { $_.version -like "$ThisReleaseVersion.0-preview-*" } | Select-Object -ExpandProperty version

Write-Info "Candidate builds:"
$candidates | Sort-Object Version | ForEach-Object {
    $onFeed = $feedVersions -contains $_.Version
    Write-Host ("  {0,-18} build {1,-14} -> {2}  [{3}]" -f $_.Source, $_.BuildNumber, $_.Version, ($(if ($onFeed) { 'on feed' } else { 'NOT on feed' })))
}

$verified = $candidates | Where-Object { $feedVersions -contains $_.Version } | Sort-Object Version
if (-not $verified) { throw "No candidate version is present on the dotnet-tools feed; widen the build search or verify feed publication." }

$chosen = ($verified | Select-Object -Last 1).Version
Write-Host ""
Write-Host "PackageValidationBaselineVersion = $chosen" -ForegroundColor Green
# Emit the bare value on the pipeline for scripting.
$chosen
