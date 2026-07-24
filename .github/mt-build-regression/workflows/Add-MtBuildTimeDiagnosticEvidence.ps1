# Copyright (c) Microsoft. All rights reserved.

<#
.SYNOPSIS
Adds task, target, evaluation, and migration evidence from scheduled diagnostic runs.

.DESCRIPTION
Diagnostic runs are matched to current and healthy candidates by exact MSBuild source SHA. This
script queries already-ingested Kusto data; it never downloads raw binlogs.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputEvidence,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$OrganizationUri = 'https://dev.azure.com/devdiv',
    [string]$Project = 'DevDiv',
    [string]$ClusterUri = 'https://perfstar-experimental.swedencentral.kusto.windows.net',
    [string]$Database = 'perfstar-dev',
    [int]$DiagnosticPipelineId = 28394,
    [int]$MaximumRunsToInspect = 24
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\components\clients\AzureDevOpsClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\clients\KustoClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\evidence\DiagnosticEvidence.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\reporting\RegressionReportWriter.psm1') -Force

# Validate both credential boundaries and the actual-run evidence from the previous step.
$azdoAccessToken = $env:AZDO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($azdoAccessToken))
{
    throw 'AZDO_ACCESS_TOKEN is required.'
}

$kustoAccessToken = $env:KUSTO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($kustoAccessToken))
{
    throw 'KUSTO_ACCESS_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $InputEvidence))
{
    throw "Actual-run evidence not found: $InputEvidence"
}

# Create read-only clients for run discovery and already-ingested diagnostic data.
$azureDevOpsClient = New-AzureDevOpsClient `
    -OrganizationUri $OrganizationUri `
    -Project $Project `
    -AccessToken $azdoAccessToken
$kustoClient = New-KustoClient `
    -ClusterUri $ClusterUri `
    -Database $Database `
    -AccessToken $kustoAccessToken
$evidence = Get-Content -LiteralPath $InputEvidence -Raw | ConvertFrom-Json

# Match diagnostic runs by exact source SHA and select bounded task, target, and evaluation deltas.
$candidates = @(
    Get-DiagnosticEvidenceCandidates `
        -Evidence $evidence `
        -AzureDevOpsClient $azureDevOpsClient `
        -KustoClient $kustoClient `
        -DiagnosticPipelineId $DiagnosticPipelineId `
        -MaximumRunsToInspect $MaximumRunsToInspect)

# Publish supporting evidence only; raw binlogs are never downloaded by this workflow.
Write-DiagnosticEvidenceReport `
    -Candidates $candidates `
    -DiagnosticPipelineId $DiagnosticPipelineId `
    -MaximumRunsToInspect $MaximumRunsToInspect `
    -OutputDirectory $OutputDirectory
Write-Host "Wrote scheduled-binlog evidence for $($candidates.Count) candidate(s) to $OutputDirectory."
