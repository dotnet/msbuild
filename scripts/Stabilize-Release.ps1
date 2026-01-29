<#
.SYNOPSIS
    Performs the stabilization portion of the MSBuild release process.

.DESCRIPTION
    This script modifies eng/Versions.props to:
    1. Add <DotNetFinalVersionKind>release</DotNetFinalVersionKind> on the same line as VersionPrefix
       (to create a merge conflict in forward-flow, as per release documentation)
    2. Change PreReleaseVersionLabel from 'preview' to 'servicing'

    See documentation/release.md and documentation/release-checklist.md for details.

.PARAMETER DryRun
    If specified, shows what changes would be made without actually modifying the file.

.EXAMPLE
    .\Stabilize-Release.ps1
    Performs the stabilization changes to eng/Versions.props.

.EXAMPLE
    .\Stabilize-Release.ps1 -DryRun
    Shows what changes would be made without modifying any files.
#>

[CmdletBinding()]
param(
    [switch]$DryRun
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

# Find repo root by looking for eng/Versions.props
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

$versionsPropsPath = Join-Path $repoRoot 'eng\Versions.props'

if (-not (Test-Path $versionsPropsPath)) {
    Write-Error "Could not find eng/Versions.props at expected location: '$versionsPropsPath'. Repository structure may have changed."
    exit 1
}

Write-Host "Processing: $versionsPropsPath" -ForegroundColor Cyan

$content = Get-Content $versionsPropsPath -Raw

# Pattern 1: Add DotNetFinalVersionKind on the same line as VersionPrefix
# Match: <VersionPrefix>X.Y.Z</VersionPrefix>
# Replace with: <VersionPrefix>X.Y.Z</VersionPrefix><DotNetFinalVersionKind>release</DotNetFinalVersionKind><!-- Keep next to VersionPrefix to create a conflict in forward-flow -->
$versionPrefixPattern = '(<VersionPrefix>[^<]+</VersionPrefix>)(\s*\r?\n)'

if ($content -match '<DotNetFinalVersionKind>release</DotNetFinalVersionKind>') {
    Write-Error "DotNetFinalVersionKind is already set to 'release'. Has stabilization already been done?"
    exit 1
} elseif ($content -match $versionPrefixPattern) {
    $newVersionPrefixLine = '$1<DotNetFinalVersionKind>release</DotNetFinalVersionKind><!-- Keep next to VersionPrefix to create a conflict in forward-flow -->$2'
    $content = $content -replace $versionPrefixPattern, $newVersionPrefixLine
    Write-Host "  Added DotNetFinalVersionKind=release on VersionPrefix line." -ForegroundColor Green
} else {
    Write-Error "Could not find VersionPrefix element in expected format. Expected pattern like: <VersionPrefix>X.Y.Z</VersionPrefix>"
    exit 1
}

# Pattern 2: Change PreReleaseVersionLabel from 'preview' to 'servicing'
# Match: <PreReleaseVersionLabel>preview</PreReleaseVersionLabel>
# Replace with: <PreReleaseVersionLabel>servicing</PreReleaseVersionLabel>
$preReleasePattern = '<PreReleaseVersionLabel>preview</PreReleaseVersionLabel>'

if ($content -match '<PreReleaseVersionLabel>servicing</PreReleaseVersionLabel>') {
    Write-Error "PreReleaseVersionLabel is already 'servicing'. Has stabilization already been done?"
    exit 1
} elseif ($content -match $preReleasePattern) {
    $content = $content -replace $preReleasePattern, '<PreReleaseVersionLabel>servicing</PreReleaseVersionLabel>'
    Write-Host "  Changed PreReleaseVersionLabel from 'preview' to 'servicing'." -ForegroundColor Green
} else {
    Write-Error "Could not find PreReleaseVersionLabel with value 'preview'. Current value may not be 'preview'."
    exit 1
}

# Extract version for commit message (e.g., "18.4" from "18.4.0")
$versionForCommit = ""
if ($content -match '<VersionPrefix>(\d+\.\d+)\.\d+</VersionPrefix>') {
    $versionForCommit = $Matches[1]
} else {
    Write-Error "Could not extract VersionPrefix for commit message."
    exit 1
}

if ($DryRun) {
    Write-Host "`n=== DRY RUN - No changes written ===" -ForegroundColor Magenta
    Write-Host "`nResulting content of eng/Versions.props (first 30 lines):" -ForegroundColor Cyan
    $content -split "`n" | Select-Object -First 30 | ForEach-Object { Write-Host $_ }
} else {
    [System.IO.File]::WriteAllText($versionsPropsPath, $content, [System.Text.Encoding]::UTF8)
    Write-Host "`nStabilization complete. Changes written to: $versionsPropsPath" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "  1. Review the changes: git diff eng/Versions.props"
    Write-Host "  2. Commit: git commit -am 'Stable branding for $versionForCommit release'"
    Write-Host "  3. Create PR to the release branch (e.g., vs$versionForCommit)"
}
