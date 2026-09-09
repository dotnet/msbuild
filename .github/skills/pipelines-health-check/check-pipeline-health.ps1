#requires -Version 5.1
<#
.SYNOPSIS
Reads recent MSBuild pipeline outcomes and the last successful run.
.DESCRIPTION
Read-only. Uses existing az authentication and built-in az rest, not automatic
extension installation. Defaults are discovery hints: default IDs must still
resolve to the expected pipeline names. Required query failures terminate without
success-shaped JSON. The output is always an array; recent runs are a bounded
sample, not exhaustive history.
.PARAMETER IncludeFailureDetails
Read timeline failure records only when deeper detail is requested.
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [int[]]$PipelineIds = @(9434, 17389),
    [string]$Organization = 'https://dev.azure.com/devdiv',
    [string]$Project = 'DevDiv',
    [ValidateNotNullOrEmpty()]
    [string]$Branch = 'main',
    [ValidateRange(1, 100)]
    [int]$Top = 5,
    [switch]$IncludeFailureDetails
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HealthCheck.Common.ps1')

$organizationName = Get-HealthOrganizationName -Organization $Organization
$Organization = "https://dev.azure.com/$organizationName"
$baseUrl = "$Organization/$([Uri]::EscapeDataString($Project))"
$branchRef = if ($Branch.StartsWith('refs/')) { $Branch } else { "refs/heads/$Branch" }
$encodedBranch = [Uri]::EscapeDataString($branchRef)
$expectedNames = @{}
if ($organizationName -eq 'devdiv' -and $Project -eq 'DevDiv')
{
    $expectedNames = @{ 9434 = 'MSBuild'; 17389 = 'MSBuild-OptProf' }
}
$now = [DateTimeOffset]::UtcNow
$results = [System.Collections.Generic.List[object]]::new()

foreach ($pipelineId in $PipelineIds)
{
    if ($pipelineId -le 0) { throw 'Pipeline IDs must be positive.' }
    $definition = Invoke-HealthAzDoGet -Url "$baseUrl/_apis/build/definitions/$pipelineId`?api-version=7.1"
    if ($definition.id -ne $pipelineId -or [string]::IsNullOrWhiteSpace($definition.name))
    {
        throw "Pipeline $pipelineId did not resolve to a valid definition."
    }
    if ($expectedNames.ContainsKey($pipelineId) -and $definition.name -ne $expectedNames[$pipelineId])
    {
        throw "Default pipeline ID $pipelineId no longer resolves to the expected pipeline. Discover the current definition before continuing."
    }

    $runsUrl = "$baseUrl/_apis/build/builds?definitions=$pipelineId&branchName=$encodedBranch&queryOrder=queueTimeDescending&api-version=7.1"
    $response = Invoke-HealthAzDoGet -Url "$runsUrl&`$top=$Top"
    $runs = @(Get-HealthResponseValues -Response $response -Operation "Recent runs for pipeline $pipelineId")
    $runResults = @(
        foreach ($run in $runs)
        {
            if ($run.definition.id -ne $pipelineId -or $run.sourceBranch -ne $branchRef)
            {
                throw 'A returned build does not match the requested pipeline/branch.'
            }
            $item = [ordered]@{
                id = $run.id
                result = $run.result
                status = $run.status
                branch = $run.sourceBranch
                sourceVersion = $run.sourceVersion
                startTime = $run.startTime
                finishTime = $run.finishTime
                reason = $run.reason
                url = "$baseUrl/_build/results?buildId=$($run.id)"
            }
            if ($IncludeFailureDetails -and $run.result -in @('failed', 'partiallySucceeded'))
            {
                $item.failureDetails = @(Get-HealthTimelineDetails -Url "$baseUrl/_apis/build/builds/$($run.id)/timeline?api-version=7.1")
            }
            [pscustomobject]$item
        }
    )

    $successUrl = $runsUrl.Replace('queryOrder=queueTimeDescending', 'queryOrder=finishTimeDescending')
    $successResponse = Invoke-HealthAzDoGet -Url "$successUrl&resultFilter=succeeded&statusFilter=completed&`$top=1"
    $successes = @(Get-HealthResponseValues -Response $successResponse -Operation "Last successful run for pipeline $pipelineId")
    $lastSuccess = $null
    if ($successes.Count -gt 0)
    {
        $run = $successes[0]
        if ($run.definition.id -ne $pipelineId -or $run.sourceBranch -ne $branchRef -or
            $run.result -ne 'succeeded' -or $run.status -ne 'completed')
        {
            throw 'The last-success query returned a nonmatching build.'
        }
        $finish = [DateTimeOffset]::Parse($run.finishTime)
        $lastSuccess = [ordered]@{
            id = $run.id
            finishTime = $run.finishTime
            ageHours = [Math]::Round(($now - $finish).TotalHours, 1)
            url = "$baseUrl/_build/results?buildId=$($run.id)"
        }
    }

    $health = Get-HealthRunState -Runs $runs
    $results.Add([pscustomobject][ordered]@{
        schemaVersion = 2
        collectionStatus = 'complete'
        observedAt = $now.ToString('o')
        organization = $Organization
        project = $Project
        pipelineName = $definition.name
        pipelineId = $pipelineId
        branch = $branchRef
        sampleLimit = $Top
        lastSuccessfulRun = $lastSuccess
        recentRuns = $runResults
        healthState = $health.state
        healthSummary = $health.summary
    })
}

ConvertTo-Json -InputObject @($results.ToArray()) -Depth 10
