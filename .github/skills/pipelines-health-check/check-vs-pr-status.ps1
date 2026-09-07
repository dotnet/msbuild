#requires -Version 5.1
<#
.SYNOPSIS
Reads VS insertion PR checks, current blocking policies, and recent merged PRs.
.DESCRIPTION
Read-only, using existing az authentication. Verifies repository/reviewer
identities, paginates the selected PR/policy collections, and keeps missing
evidence distinct from success. Last-merged coverage is limited to the explicitly
reported CompletedWithinDays window. Failures terminate without success JSON.
#>
[CmdletBinding()]
param(
    [string]$Organization = 'https://dev.azure.com/devdiv',
    [string]$Project = 'DevDiv',
    [string]$RepositoryName = 'VS',
    [string]$RepositoryId,
    [guid]$ReviewerId = '66cc9d27-aef7-4399-ba2c-3dccb4489098',
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedReviewerName = 'MSBuild',
    [ValidateNotNullOrEmpty()]
    [string]$TargetBranch = 'main',
    [ValidateRange(1, 3650)]
    [int]$CompletedWithinDays = 30,
    [ValidateRange(1, 1000)]
    [int]$MaxPages = 100,
    [switch]$IncludeExperimental
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HealthCheck.Common.ps1')

function Get-DeduplicatedStatuses
{
    param(
        [object[]]$Statuses,
        [int]$CurrentIteration
    )

    $latest = @{}
    foreach ($status in $Statuses)
    {
        $iteration = [int]$status.iterationId
        if ($iteration -gt 0 -and $iteration -ne $CurrentIteration) { continue }
        if ([string]::IsNullOrWhiteSpace($status.context.name))
        {
            throw 'A PR status has no context identity.'
        }
        $key = "$($status.context.genre)/$($status.context.name)"
        $timestamp = if ($status.updatedDate) { [DateTimeOffset]::Parse($status.updatedDate) }
                     elseif ($status.creationDate) { [DateTimeOffset]::Parse($status.creationDate) }
                     else { [DateTimeOffset]::MinValue }
        $existing = $latest[$key]
        if ($null -eq $existing -or $timestamp -gt $existing.timestamp -or
            ($timestamp -eq $existing.timestamp -and $status.id -gt $existing.status.id))
        {
            $latest[$key] = @{ timestamp = $timestamp; status = $status }
        }
    }
    $latest.Values | ForEach-Object { $_.status }
}

function Build-PRCheckSummary
{
    param([object[]]$Statuses)

    $knownStates = @('succeeded', 'failed', 'error', 'pending', 'notSet', 'notApplicable')
    [pscustomobject][ordered]@{
        total = $Statuses.Count
        succeeded = @($Statuses | Where-Object { $_.state -eq 'succeeded' }).Count
        failed = @($Statuses | Where-Object { $_.state -in @('failed', 'error') }).Count
        pending = @($Statuses | Where-Object { $_.state -in @('pending', 'notSet') }).Count
        notApplicable = @($Statuses | Where-Object { $_.state -eq 'notApplicable' }).Count
        unknown = @($Statuses | Where-Object { $_.state -notin $knownStates }).Count
        scope = 'current iteration and PR-wide statuses; not a merge-eligibility decision'
        details = @(
            $Statuses | Sort-Object { "$($_.context.genre)/$($_.context.name)" } | ForEach-Object {
                [pscustomobject]@{
                    genre = $_.context.genre
                    name = $_.context.name
                    state = $_.state
                    iterationId = $_.iterationId
                    description = ConvertTo-HealthText -Text $_.description
                    targetUrl = $_.targetUrl
                }
            }
        )
    }
}

$organizationName = Get-HealthOrganizationName -Organization $Organization
$Organization = "https://dev.azure.com/$organizationName"
$baseUrl = "$Organization/$([Uri]::EscapeDataString($Project))"
$repositoryKey = if ($RepositoryId) { $RepositoryId } else { $RepositoryName }
$repository = Invoke-HealthAzDoGet -Url "$baseUrl/_apis/git/repositories/$([Uri]::EscapeDataString($repositoryKey))?api-version=7.1"
if (!$repository.id -or !$repository.project.id -or $repository.name -ne $RepositoryName)
{
    throw 'The selected repository does not match the expected repository name/project.'
}
$RepositoryId = $repository.id
$repoUrl = "$baseUrl/_git/$([Uri]::EscapeDataString($repository.name))"
$apiUrl = "$baseUrl/_apis/git/repositories/$RepositoryId/pullRequests"

$identitiesResponse = Invoke-HealthAzDoGet -Url "https://vssps.dev.azure.com/$organizationName/_apis/identities?identityIds=$ReviewerId&queryMembership=None&api-version=7.1"
$identities = @(Get-HealthResponseValues -Response $identitiesResponse -Operation 'Reviewer identity lookup')
if ($identities.Count -ne 1 -or $identities[0].id -ne $ReviewerId.ToString() -or
    !$identities[0].isActive -or
    ([string]$identities[0].providerDisplayName).IndexOf($ExpectedReviewerName, [StringComparison]::OrdinalIgnoreCase) -lt 0)
{
    throw 'The reviewer identity is unavailable or does not match the expected team. Discover the current reviewer before continuing.'
}

$now = [DateTimeOffset]::UtcNow
$since = $now.AddDays(-$CompletedWithinDays)
$branchRef = if ($TargetBranch.StartsWith('refs/')) { $TargetBranch } else { "refs/heads/$TargetBranch" }
$criteria = "searchCriteria.reviewerId=$ReviewerId&searchCriteria.targetRefName=$([Uri]::EscapeDataString($branchRef))&api-version=7.1"
$active = @(Get-HealthPagedValues -Url "$apiUrl`?searchCriteria.status=active&$criteria" -MaxPages $MaxPages)
$selected = @($active | Where-Object { $IncludeExperimental -or $_.title -notmatch '\[Experimental\]' })
$results = @(
    foreach ($pr in $selected)
    {
        $id = $pr.pullRequestId
        if ($pr.repository.id -ne $RepositoryId -or $pr.targetRefName -ne $branchRef)
        {
            throw 'A returned PR does not match the selected repository/branch.'
        }
        $iterationResponse = Invoke-HealthAzDoGet -Url "$apiUrl/$id/iterations?api-version=7.1"
        $iterations = @(Get-HealthResponseValues -Response $iterationResponse -Operation "Iterations for PR $id")
        $currentIteration = if ($iterations.Count -gt 0) { [int]($iterations | Measure-Object -Property id -Maximum).Maximum } else { 0 }
        $statusResponse = Invoke-HealthAzDoGet -Url "$apiUrl/$id/statuses?api-version=7.1"
        $statuses = @(Get-HealthResponseValues -Response $statusResponse -Operation "Statuses for PR $id")
        $currentStatuses = @(Get-DeduplicatedStatuses -Statuses $statuses -CurrentIteration $currentIteration)
        $checks = Build-PRCheckSummary -Statuses $currentStatuses

        $artifact = [Uri]::EscapeDataString("vstfs:///CodeReview/CodeReviewId/$($repository.project.id)/$id")
        $policies = @(Get-HealthPagedValues -Url "$baseUrl/_apis/policy/evaluations?artifactId=$artifact&includeNotApplicable=true&api-version=7.1-preview.1" -MaxPages $MaxPages)
        $blocking = @($policies | Where-Object { $_.configuration.isEnabled -eq $true -and $_.configuration.isBlocking -eq $true })
        $blocked = @($blocking | Where-Object { $_.status -in @('rejected', 'broken') }).Count
        $waiting = @($blocking | Where-Object { $_.status -in @('queued', 'running') }).Count
        $unknownPolicies = @($policies | Where-Object {
            $null -eq $_.configuration.isEnabled -or $null -eq $_.configuration.isBlocking -or
            $_.status -notin @('approved', 'notApplicable', 'queued', 'running', 'rejected', 'broken')
        }).Count

        $state = 'HEALTHY'
        $reason = 'observed checks and blocking policies passed; merge eligibility is not assessed'
        if ($blocked -gt 0 -or $checks.failed -gt 0 -or $pr.mergeStatus -eq 'conflicts')
        {
            $state = 'UNHEALTHY'
            $reason = 'a blocking policy, observed check, or merge conflict needs attention'
        }
        elseif ($currentIteration -eq 0 -or $unknownPolicies -gt 0 -or $checks.unknown -gt 0 -or
                ($checks.total -eq 0 -and $blocking.Count -eq 0))
        {
            $state = 'UNKNOWN'
            $reason = 'current iteration, policy, or check evidence is missing or unrecognized'
        }
        elseif ($waiting -gt 0 -or $checks.pending -gt 0)
        {
            $state = 'IN_PROGRESS'
            $reason = 'checks or blocking policies are pending'
        }

        [pscustomobject][ordered]@{
            id = $id
            title = $pr.title
            url = "$repoUrl/pullrequest/$id"
            createdDate = $pr.creationDate
            ageHours = [Math]::Round(($now - [DateTimeOffset]::Parse($pr.creationDate)).TotalHours, 1)
            mergeStatus = $pr.mergeStatus
            targetBranch = $pr.targetRefName
            currentIteration = $currentIteration
            checks = $checks
            blockingPolicies = @($blocking | ForEach-Object {
                [pscustomobject]@{ id = $_.configuration.id; name = $_.configuration.type.displayName; status = $_.status }
            })
            unknownPolicyCount = $unknownPolicies
            actionNeeded = $state -in @('UNHEALTHY', 'UNKNOWN')
            healthState = $state
            healthSummary = "$state - $reason"
        }
    }
)

$completedUrl = "$apiUrl`?searchCriteria.status=completed&$criteria&searchCriteria.queryTimeRangeType=closed&searchCriteria.minTime=$([Uri]::EscapeDataString($since.ToString('o')))"
$completed = @(Get-HealthPagedValues -Url $completedUrl -MaxPages $MaxPages)
$last = $completed | Where-Object { $IncludeExperimental -or $_.title -notmatch '\[Experimental\]' } |
    Sort-Object { [DateTimeOffset]::Parse($_.closedDate) } -Descending | Select-Object -First 1
$lastMerged = $null
if ($last)
{
    if ($last.repository.id -ne $RepositoryId -or $last.targetRefName -ne $branchRef)
    {
        throw 'A completed PR does not match the selected repository/branch.'
    }
    $lastMerged = [ordered]@{
        id = $last.pullRequestId
        title = $last.title
        closedDate = $last.closedDate
        ageHours = [Math]::Round(($now - [DateTimeOffset]::Parse($last.closedDate)).TotalHours, 1)
        url = "$repoUrl/pullrequest/$($last.pullRequestId)"
    }
}

[pscustomobject][ordered]@{
    schemaVersion = 2
    collectionStatus = 'complete'
    observedAt = $now.ToString('o')
    organization = $Organization
    project = $Project
    repository = $repository.name
    repositoryId = $RepositoryId
    reviewerId = $ReviewerId.ToString()
    reviewerName = $identities[0].providerDisplayName
    targetBranch = $branchRef
    experimentalIncluded = [bool]$IncludeExperimental
    prs = $results
    lastMergedPr = $lastMerged
    lastMergedSearchSince = $since.ToString('o')
    summary = "$($results.Count) selected active PRs. Last-merged coverage starts $($since.ToString('yyyy-MM-dd')); ages are elapsed hours."
} | ConvertTo-Json -Depth 10
