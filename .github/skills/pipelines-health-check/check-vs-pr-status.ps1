<#
.SYNOPSIS
    Checks status of active VS repo PRs assigned to MSBuild team.

.DESCRIPTION
    Queries active pull requests in the VS repository that are assigned to the
    MSBuild team as reviewer, filters out Experimental PRs, retrieves check
    statuses for each, and finds the last successfully merged non-Experimental PR.
    Outputs structured JSON for agent consumption.

.PARAMETER Organization
    Azure DevOps organization URL.

.PARAMETER Project
    Azure DevOps project name.

.PARAMETER RepositoryId
    VS repository GUID.

.PARAMETER ReviewerId
    MSBuild team reviewer GUID.

.PARAMETER ProjectId
    DevDiv project GUID (used for constructing PR URLs).

.EXAMPLE
    .\check-vs-pr-status.ps1
#>
[CmdletBinding()]
param(
    [string]$Organization = "https://dev.azure.com/devdiv",
    [string]$Project = "DevDiv",
    [string]$RepositoryId = "a290117c-5a8a-40f7-bc2c-f14dbe3acf6d",
    [string]$ReviewerId = "66cc9d27-aef7-4399-ba2c-3dccb4489098",
    [string]$ProjectId = "0bdbc590-a062-4c3f-b0f6-9383f67865ee"
)

$ErrorActionPreference = "Stop"

# Known check genres that are considered required / important for merge
$script:RequiredCheckGenres = @(
    "required tests"
    "dd-cb-pr"
    "signcheck (yaml)"
    "symbolcheck (yaml)"
    "hashcheck (yaml)"
    "sbomcheck (yaml)"
    "msbuild retail validation - pr"
)

function Get-AzDoToken {
    // Entra app ID
    return (az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv).Trim()
}

function Get-AuthHeaders {
    param([string]$Token)
    return @{ Authorization = "Bearer $Token" }
}

function Get-ActivePRs {
    param([string]$Token)
    $headers = Get-AuthHeaders -Token $Token
    $url = "$Organization/$Project/_apis/git/repositories/$RepositoryId/pullrequests" +
           "?searchCriteria.status=active" +
           "&searchCriteria.reviewerId=$ReviewerId" +
           "&`$top=25" +
           "&api-version=7.1"
    $resp = Invoke-RestMethod -Uri $url -Headers $headers
    return $resp.value
}

function Get-CompletedPRs {
    param([string]$Token, [int]$Top = 10)
    $headers = Get-AuthHeaders -Token $Token
    $url = "$Organization/$Project/_apis/git/repositories/$RepositoryId/pullrequests" +
           "?searchCriteria.status=completed" +
           "&searchCriteria.reviewerId=$ReviewerId" +
           "&searchCriteria.targetRefName=refs/heads/main" +
           "&`$top=$Top" +
           "&api-version=7.1"
    $resp = Invoke-RestMethod -Uri $url -Headers $headers
    return $resp.value
}

function Get-PRStatuses {
    param([string]$Token, [int]$PullRequestId)
    $headers = Get-AuthHeaders -Token $Token
    $url = "$Organization/$Project/_apis/git/repositories/$RepositoryId/pullrequests/$PullRequestId/statuses" +
           "?api-version=7.1"
    $resp = Invoke-RestMethod -Uri $url -Headers $headers
    return $resp.value
}

function Test-IsExperimental {
    param([string]$Title)
    return $Title -match "\[Experimental\]"
}

function Test-IsRequiredCheck {
    param([string]$Genre)
    $genreLower = $Genre.ToLowerInvariant()
    return $script:RequiredCheckGenres -contains $genreLower
}

function Get-DeduplicatedStatuses {
    param($Statuses)
    # Keep the latest status per unique context (genre/name), preferring higher iterationId
    $latest = @{}
    foreach ($s in $Statuses) {
        $key = "$($s.context.genre)/$($s.context.name)"
        $existingIter = if ($latest.ContainsKey($key)) { $latest[$key].iterationId } else { -1 }
        $currentIter = if ($null -ne $s.iterationId) { $s.iterationId } else { 0 }
        if ($currentIter -ge $existingIter) {
            $latest[$key] = $s
        }
    }
    return $latest.Values
}

function Build-PRCheckSummary {
    param($Statuses)

    $deduped = Get-DeduplicatedStatuses -Statuses $Statuses
    $succeeded = @($deduped | Where-Object { $_.state -eq "succeeded" })
    $failed = @($deduped | Where-Object { $_.state -eq "failed" -or $_.state -eq "error" })
    $pending = @($deduped | Where-Object { $_.state -eq "pending" -or $_.state -eq "notSet" -or [string]::IsNullOrEmpty($_.state) })
    $notApplicable = @($deduped | Where-Object { $_.state -eq "notApplicable" })

    $failedChecks = @($failed | ForEach-Object {
        [ordered]@{
            genre       = $_.context.genre
            name        = $_.context.name
            description = $_.description
            isRequired  = (Test-IsRequiredCheck -Genre $_.context.genre)
        }
    })

    $pendingChecks = @($pending | ForEach-Object {
        [ordered]@{
            genre       = $_.context.genre
            name        = $_.context.name
            description = $_.description
            isRequired  = (Test-IsRequiredCheck -Genre $_.context.genre)
        }
    })

    $hasFailedRequired = ($failedChecks | Where-Object { $_.isRequired }) -ne $null

    return [ordered]@{
        total          = $deduped.Count
        succeeded      = $succeeded.Count
        pending        = $pending.Count
        failed         = $failed.Count
        notApplicable  = $notApplicable.Count
        failedChecks   = $failedChecks
        pendingChecks  = $pendingChecks
        hasFailedRequired = $hasFailedRequired
    }
}

# --- Main ---

$token = Get-AzDoToken
$now = [DateTimeOffset]::UtcNow
$prBaseUrl = "$Organization/$Project/_git/VS/pullrequest"

# Get active PRs and filter out Experimental
$activePRs = Get-ActivePRs -Token $token
$nonExperimentalPRs = @($activePRs | Where-Object { -not (Test-IsExperimental -Title $_.title) })

# Build status info for each non-experimental PR
$prResults = @()
foreach ($pr in $nonExperimentalPRs) {
    $statuses = Get-PRStatuses -Token $token -PullRequestId $pr.pullRequestId
    $checkSummary = Build-PRCheckSummary -Statuses $statuses

    $createdDate = [DateTimeOffset]::Parse($pr.creationDate)
    $ageHours = [math]::Round(($now - $createdDate).TotalHours, 1)

    $prResults += [PSCustomObject][ordered]@{
        id          = $pr.pullRequestId
        title       = $pr.title
        url         = "$prBaseUrl/$($pr.pullRequestId)"
        createdDate = $pr.creationDate
        ageHours    = $ageHours
        mergeStatus = $pr.mergeStatus
        targetBranch = $pr.targetRefName
        checks      = $checkSummary
        actionNeeded = $checkSummary.hasFailedRequired
    }
}

# Get last merged non-Experimental PR into main
$completedPRs = Get-CompletedPRs -Token $token
$lastMergedPr = $null
foreach ($pr in $completedPRs) {
    if (-not (Test-IsExperimental -Title $pr.title)) {
        $closedDate = [DateTimeOffset]::Parse($pr.closedDate)
        $ageHours = [math]::Round(($now - $closedDate).TotalHours, 1)
        $lastMergedPr = [ordered]@{
            id         = $pr.pullRequestId
            title      = $pr.title
            closedDate = $pr.closedDate
            ageHours   = $ageHours
            url        = "$prBaseUrl/$($pr.pullRequestId)"
        }
        break
    }
}

# Build summary
$failingPrCount = @($prResults | Where-Object { $_.actionNeeded }).Count
$summaryParts = @("$($prResults.Count) active non-experimental PRs", "$failingPrCount with failing required checks")
if ($lastMergedPr) {
    $ageDays = [math]::Round($lastMergedPr.ageHours / 24, 1)
    $summaryParts += "Last merged PR: $ageDays days ago"
}

$output = [PSCustomObject][ordered]@{
    prs          = @($prResults)
    lastMergedPr = $lastMergedPr
    summary      = ($summaryParts -join ". ") + "."
}

$output | ConvertTo-Json -Depth 6
