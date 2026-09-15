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

# Execute the detector and turn its tabular result into the stable candidate contract.
$client = New-KustoClient -ClusterUri $ClusterUri -Database $Database -AccessToken $accessToken
$query = Get-Content -LiteralPath $QueryPath -Raw
$detected = @(Invoke-KustoQuery -Client $client -Query $query -MaximumAttempts 4 -RequirePrimaryTable $true)

# PerfStarDataRaw is ingested while a run executes, so the detector can select a build that has not
# finished and therefore reported only part of its scenarios. Resolve each run's real state here, so
# that the candidate set, its identity key, and this job's outputs all describe usable runs only.
# Azure DevOps lookup failures propagate: a transient error must fail the scan rather than silently
# suppress candidates.
$azureDevOpsClient = New-AzureDevOpsClient `
    -OrganizationUri $OrganizationUri `
    -Project $Project `
    -AccessToken $azdoAccessToken
$runCache = New-PerfStarRunCache
$excluded = [System.Collections.Generic.List[object]]::new()

$candidates = @(
    foreach ($candidate in $detected)
    {
        $currentRun = Get-PerfStarRunMetadata `
            -AzureDevOpsClient $azureDevOpsClient `
            -RunCache $runCache `
            -BuildId ([string]$candidate.CurrentBuildId) `
            -Backend ([string]$candidate.Backend)

        if (-not (Test-PerfStarRunUsable -Run $currentRun))
        {
            Write-Warning ("Excluding {0}/{1} '{2}': current PerfStar run {3} is not usable (state '{4}', result '{5}')." -f `
                $candidate.Backend,
                $candidate.Os,
                $candidate.ScenarioPair,
                $currentRun.perfStarBuildNumber,
                $currentRun.perfStarBuildState,
                $currentRun.perfStarBuildResult)
            $excluded.Add([pscustomobject][ordered]@{
                backend = [string]$candidate.Backend
                os = [string]$candidate.Os
                scenarioPair = [string]$candidate.ScenarioPair
                run = 'current'
                perfStarBuildNumber = $currentRun.perfStarBuildNumber
                perfStarBuildState = $currentRun.perfStarBuildState
                perfStarBuildResult = $currentRun.perfStarBuildResult
            })
            continue
        }

        # An unusable last-healthy run only invalidates the comparison, not the candidate. Drop the
        # reference so the evidence step reports the candidate without a healthy baseline.
        if (-not [string]::IsNullOrWhiteSpace([string]$candidate.HealthyBuildId))
        {
            $healthyRun = Get-PerfStarRunMetadata `
                -AzureDevOpsClient $azureDevOpsClient `
                -RunCache $runCache `
                -BuildId ([string]$candidate.HealthyBuildId) `
                -Backend ([string]$candidate.Backend)

            if (-not (Test-PerfStarRunUsable -Run $healthyRun))
            {
                Write-Warning ("Dropping the last-healthy comparison for {0}/{1} '{2}': run {3} is not usable (state '{4}', result '{5}')." -f `
                    $candidate.Backend,
                    $candidate.Os,
                    $candidate.ScenarioPair,
                    $healthyRun.perfStarBuildNumber,
                    $healthyRun.perfStarBuildState,
                    $healthyRun.perfStarBuildResult)
                $excluded.Add([pscustomobject][ordered]@{
                    backend = [string]$candidate.Backend
                    os = [string]$candidate.Os
                    scenarioPair = [string]$candidate.ScenarioPair
                    run = 'healthy'
                    perfStarBuildNumber = $healthyRun.perfStarBuildNumber
                    perfStarBuildState = $healthyRun.perfStarBuildState
                    perfStarBuildResult = $healthyRun.perfStarBuildResult
                })
                $candidate.HealthyBuildId = ''
                foreach ($healthyField in @(
                    'HealthyBuildNumber',
                    'HealthyTimestamp',
                    'HealthyMtMedianMs',
                    'HealthyNonMtMedianMs',
                    'HealthyMtVsNonMtDeltaMs'))
                {
                    if ($null -ne $candidate.PSObject.Properties[$healthyField])
                    {
                        $candidate.$healthyField = $null
                    }
                }
            }
        }

        $candidate
    })

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
Write-Host "Detector returned $($detected.Count) pair(s) from $Database on $ClusterUri."
Write-Host "Resolved $($runCache.Count) PerfStar run(s) through $OrganizationUri/$Project."
Write-Host "Excluded $($excluded.Count) unusable run reference(s); $($candidates.Count) candidate(s) remain."
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
