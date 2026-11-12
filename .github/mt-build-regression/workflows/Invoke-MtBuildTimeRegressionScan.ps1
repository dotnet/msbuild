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
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$OrganizationUri = 'https://dev.azure.com/devdiv',
    [string]$Project = 'DevDiv'
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\components\clients\AzureDevOpsClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\clients\KustoClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\evidence\RunSelection.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\evidence\RegressionDetection.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\components\reporting\RegressionReportWriter.psm1') -Force

# Validate the trusted-job inputs before making a Kusto request.
$accessToken = $env:KUSTO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($accessToken))
{
    throw 'KUSTO_ACCESS_TOKEN is required.'
}

$azdoAccessToken = $env:AZDO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($azdoAccessToken))
{
    throw 'AZDO_ACCESS_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $QueryPath))
{
    throw "Kusto query file not found: $QueryPath"
}

# Discover all production runs before selecting the latest pair or calculating any baselines.
$client = New-KustoClient -ClusterUri $ClusterUri -Database $Database -AccessToken $accessToken
$query = Get-Content -LiteralPath $QueryPath -Raw
$runs = @(Invoke-KustoQuery -Client $client -Query "$query`nproductionRows() | distinct BuildId, Backend" -MaximumAttempts 4 -RequirePrimaryTable $true)

# Raw data can include running or canceled builds. Resolve the entire input set, not just
# candidates: otherwise a newer unusable run can hide a regression or pollute its baseline.
# Lookup failures propagate rather than silently suppressing candidates.
$azureDevOpsClient = New-AzureDevOpsClient `
    -OrganizationUri $OrganizationUri `
    -Project $Project `
    -AccessToken $azdoAccessToken
$runCache = New-PerfStarRunCache
$excluded = [System.Collections.Generic.List[object]]::new()

$eligibleBuildIds = @(
    foreach ($run in $runs)
    {
        $metadata = Get-PerfStarRunMetadata `
            -AzureDevOpsClient $azureDevOpsClient `
            -RunCache $runCache `
            -BuildId ([string]$run.BuildId) `
            -Backend ([string]$run.Backend)

        if (-not (Test-PerfStarRunUsable -Run $metadata))
        {
            $excluded.Add([pscustomobject][ordered]@{
                backend = [string]$run.Backend
                os = 'all'
                scenarioPair = 'all'
                run = 'input'
                perfStarBuildNumber = $metadata.perfStarBuildNumber
                perfStarBuildState = $metadata.perfStarBuildState
                perfStarBuildResult = $metadata.perfStarBuildResult
            })
            continue
        }

        [string]$run.BuildId
    })

$eligibleJson = ConvertTo-Json -InputObject $eligibleBuildIds -Compress
$candidates = @(Invoke-KustoQuery -Client $client -Query "$query`ndetectRegressions(dynamic($eligibleJson))" -MaximumAttempts 4 -RequirePrimaryTable $true)
$report = New-RegressionDetectionReport -Candidates $candidates -GeneratedAtUtc ([DateTimeOffset]::UtcNow)
$report.excludedRuns = $excluded.ToArray()

# Write the machine-readable and human-readable views of the same detection result.
Write-RegressionDetectionReport -Report $report -OutputDirectory $OutputDirectory

# Gate the later evidence and agent jobs without treating an empty result as an error.
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT))
{
    "has_regressions=$($candidates.Count -gt 0 ? 'true' : 'false')" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
    "regression_count=$($candidates.Count)" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
}

# A scheduled run is read after the fact, so record how the detector reached this candidate set.
Write-Host "Detector returned $($candidates.Count) pair(s) from $Database on $ClusterUri."
Write-Host "Resolved $($runCache.Count) PerfStar run(s) through $OrganizationUri/$Project."
Write-Host "Excluded $($excluded.Count) of $($runs.Count) production run(s) before detection; $($eligibleBuildIds.Count) run(s) were eligible."
foreach ($entry in $excluded)
{
    Write-Host "  excluded $($entry.run) run $($entry.perfStarBuildNumber) for $($entry.backend)/$($entry.os) '$($entry.scenarioPair)' (state '$($entry.perfStarBuildState)', result '$($entry.perfStarBuildResult)')."
}

foreach ($candidate in $candidates)
{
    Write-Host "  candidate $($candidate.Severity) $($candidate.Backend)/$($candidate.Os) '$($candidate.ScenarioPair)' current build $($candidate.CurrentBuildNumber), $($candidate.CurrentMtSamples) MT sample(s)."
}

Write-Host "Candidate-set key $($report.candidateSetKey)."
Write-Host "Wrote $($candidates.Count) candidate(s) to $OutputDirectory."
