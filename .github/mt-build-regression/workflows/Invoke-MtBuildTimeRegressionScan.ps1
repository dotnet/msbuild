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

$accessToken = $env:KUSTO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($accessToken))
{
    throw 'KUSTO_ACCESS_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $QueryPath))
{
    throw "Kusto query file not found: $QueryPath"
}

$client = New-KustoClient -ClusterUri $ClusterUri -Database $Database -AccessToken $accessToken
$query = Get-Content -LiteralPath $QueryPath -Raw
$candidates = @(Invoke-KustoQuery -Client $client -Query $query -RequirePrimaryTable $true)
$report = New-RegressionDetectionReport -Candidates $candidates -GeneratedAtUtc ([DateTimeOffset]::UtcNow)

Write-RegressionDetectionReport -Report $report -OutputDirectory $OutputDirectory

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT))
{
    "has_regressions=$($candidates.Count -gt 0 ? 'true' : 'false')" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
    "regression_count=$($candidates.Count)" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
}

Write-Host "Wrote $($candidates.Count) candidate(s) to $OutputDirectory."
