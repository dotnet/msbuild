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

.EXAMPLE
    .\check-vs-pr-status.ps1
#>
[CmdletBinding()]
param(
    [string]$Organization = "https://dev.azure.com/devdiv",
    [string]$Project = "DevDiv",
    [string]$RepositoryId = "a290117c-5a8a-40f7-bc2c-f14dbe3acf6d",
    [string]$ReviewerId = "66cc9d27-aef7-4399-ba2c-3dccb4489098"
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

# Azure DevOps first-party Entra app ID (used by az rest --resource)
$script:AzDoResource = "499b84ac-1321-427f-aa17-267ca6975798"

function Get-ActivePRs {
    $json = az repos pr list `
        --repository $RepositoryId `
        --status active `
        --reviewer $ReviewerId `
        --top 25 `
        --organization $Organization `
        --project $Project `
        -o json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        Write-Warning "Failed to list active PRs (exit code: $LASTEXITCODE). Is 'az extension add --name azure-devops' installed and 'az login' done?"
        return @()
    }
    return $json | ConvertFrom-Json
}

function Get-CompletedPRs {
    param([int]$Top = 10)
    $json = az repos pr list `
        --repository $RepositoryId `
        --status completed `
        --reviewer $ReviewerId `
        --target-branch main `
        --top $Top `
        --organization $Organization `
        --project $Project `
        -o json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        Write-Warning "Failed to list completed PRs (exit code: $LASTEXITCODE)."
        return @()
    }
    return $json | ConvertFrom-Json
}

function Get-PRStatuses {
    param([int]$PullRequestId)
    $url = "$Organization/$Project/_apis/git/repositories/$RepositoryId/pullrequests/$PullRequestId/statuses" +
           "?api-version=7.1"
    $json = az rest --method get --url $url --resource $script:AzDoResource 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        Write-Warning "Failed to get statuses for PR $PullRequestId (exit code: $LASTEXITCODE)."
        return @()
    }
    $resp = $json | ConvertFrom-Json
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

    $hasFailedRequired = @($failedChecks | Where-Object { $_.isRequired }).Count -gt 0

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

$now = [DateTimeOffset]::UtcNow
$prBaseUrl = "$Organization/$Project/_git/VS/pullrequest"

# Get active PRs and filter out Experimental
$activePRs = Get-ActivePRs
$nonExperimentalPRs = @($activePRs | Where-Object { -not (Test-IsExperimental -Title $_.title) })

# Build status info for each non-experimental PR
$prResults = @()
foreach ($pr in $nonExperimentalPRs) {
    $statuses = Get-PRStatuses -PullRequestId $pr.pullRequestId
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
$completedPRs = Get-CompletedPRs
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
