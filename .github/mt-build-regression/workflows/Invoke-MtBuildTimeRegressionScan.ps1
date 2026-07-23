# Copyright (c) Microsoft. All rights reserved.

<#
.SYNOPSIS
Runs the deterministic PerfStar MT regression query and writes the initial evidence contract.

.DESCRIPTION
This trusted-job entry point owns orchestration only. Kusto transport, candidate identity, and
report formatting live in purpose-specific modules under ../components.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ClusterUri,
    [Parameter(Mandatory)][string]$Database,
    [Parameter(Mandatory)][string]$QueryPath,
    [Parameter(Mandatory)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\components\clients\KustoClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\evidence\RegressionDetection.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\reporting\RegressionReportWriter.psm1') -Force

# Validate the trusted-job inputs before making a Kusto request.
$accessToken = $env:KUSTO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($accessToken))
{
    throw 'KUSTO_ACCESS_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $QueryPath))
{
    throw "Kusto query file not found: $QueryPath"
}

# Execute the detector and turn its tabular result into the stable candidate contract.
$client = New-KustoClient -ClusterUri $ClusterUri -Database $Database -AccessToken $accessToken
$query = Get-Content -LiteralPath $QueryPath -Raw
$candidates = @(Invoke-KustoQuery -Client $client -Query $query -RequirePrimaryTable $true)
$report = New-RegressionDetectionReport -Candidates $candidates -GeneratedAtUtc ([DateTimeOffset]::UtcNow)

# Write the machine-readable and human-readable views of the same detection result.
Write-RegressionDetectionReport -Report $report -OutputDirectory $OutputDirectory

# Gate the later evidence and agent jobs without treating an empty result as an error.
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT))
{
    "has_regressions=$($candidates.Count -gt 0 ? 'true' : 'false')" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
    "regression_count=$($candidates.Count)" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
}

Write-Host "Wrote $($candidates.Count) candidate(s) to $OutputDirectory."
