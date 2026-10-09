# Copyright (c) Microsoft. All rights reserved.

<#
.SYNOPSIS
Discovers trusted workflow-created GitHub items for a detected candidate set.

.DESCRIPTION
This trusted-job entry point reads only open issue and pull-request descriptions authored by the
workflow bot. It never requests comments, reviews, or review comments, and writes only structured
metadata for the AI job.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputReport,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$Repository
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\components\clients\GitHubClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\evidence\ExistingWork.psm1') -Force

$accessToken = $env:GITHUB_TOKEN
if ([string]::IsNullOrWhiteSpace($accessToken))
{
    throw 'GITHUB_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $InputReport))
{
    throw "Regression report not found: $InputReport"
}

$regressionReport = Get-Content -LiteralPath $InputReport -Raw | ConvertFrom-Json
$candidateSetKey = [string]$regressionReport.candidateSetKey
if ($candidateSetKey -notmatch '^[0-9a-f]{16}$')
{
    throw 'Regression report contains an invalid candidate-set key.'
}

if ([int]$regressionReport.candidateCount -le 0)
{
    throw 'Existing-work discovery requires at least one regression candidate.'
}

$client = New-GitHubClient -Repository $Repository -AccessToken $accessToken
$items = @(Get-GitHubOpenItemsByCreator `
    -Client $client `
    -Creator 'github-actions[bot]' `
    -Labels @('Area: PerfStar', 'Area: Performance', 'automation'))
$report = New-ExistingWorkReport `
    -Items $items `
    -CandidateSetKey $candidateSetKey `
    -Repository $Repository
Write-ExistingWorkReport -Report $report -OutputDirectory $OutputDirectory

Write-Host "Inspected $($items.Count) open workflow-authored GitHub item(s)."
Write-Host "Found $(@($report.items).Count) trusted item(s) for candidate set $candidateSetKey."
