# Copyright (c) Microsoft. All rights reserved.

<#
.SYNOPSIS
Enriches statistical candidates with exact PerfStar runs and sanitized artifact evidence.

.DESCRIPTION
Raw artifacts exist only in a unique temporary directory managed by ActualRunEvidence.psm1. The
module deletes that directory in a finally block before this entry point writes the public evidence.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputReport,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$OrganizationUri = 'https://dev.azure.com/devdiv',
    [string]$Project = 'DevDiv'
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\components\clients\AzureDevOpsClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\evidence\ActualRunEvidence.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\reporting\RegressionReportWriter.psm1') -Force

# Validate the trusted-job inputs before accessing Azure DevOps.
$accessToken = $env:AZDO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($accessToken))
{
    throw 'AZDO_ACCESS_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $InputReport))
{
    throw "Regression report not found: $InputReport"
}

# Create the read-only client and load the statistical candidates from the previous step.
$client = New-AzureDevOpsClient `
    -OrganizationUri $OrganizationUri `
    -Project $Project `
    -AccessToken $accessToken
$report = Get-Content -LiteralPath $InputReport -Raw | ConvertFrom-Json
$rawDirectory = Join-Path $env:RUNNER_TEMP "mt-regression-raw-$([Guid]::NewGuid().ToString('N'))"

# Resolve exact runs and extract allowlisted evidence; the component always deletes raw artifacts.
$candidates = @(
    Get-ActualRunEvidenceCandidates `
        -Report $report `
        -AzureDevOpsClient $client `
        -RawDirectory $rawDirectory)

# Publish only the bounded JSON and Markdown evidence contract.
Write-ActualRunEvidenceReport -Candidates $candidates -OutputDirectory $OutputDirectory
Write-Host "Wrote actual-run evidence for $($candidates.Count) candidate(s) to $OutputDirectory."
