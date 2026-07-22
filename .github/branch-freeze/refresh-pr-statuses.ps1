#!/usr/bin/env pwsh
# Refreshes the `branch-freeze` status on every open pull request, optionally
# limited to a single base branch.
[CmdletBinding()]
param(
    [Parameter(Position = 0)][AllowEmptyString()][string]$BaseRef = ''
)

function Invoke-Main {
    [OutputType([int])]
    param()

    # Step 1: Load open PRs, optionally limited to one target branch.
    $repository = Get-GitHubRepositoryName
    $env:REPO = $repository
    $pullRequests = @(
        Get-GitHubOpenPullRequest -Repository $repository -BaseRef $BaseRef
    )
    $suffix = if ([string]::IsNullOrEmpty($BaseRef)) { '' } else { " targeting $BaseRef" }
    Write-Host "Stamping $($pullRequests.Count) open PR(s)$suffix"
    if ($pullRequests.Count -ge 1000) {
        Write-Host "::warning::Open PR count hit the query limit ($($pullRequests.Count)); some PRs may not have been stamped."
    }

    # Step 2: Refresh each PR sequentially and remember any failures.
    $failed = $false
    foreach ($pullRequest in $pullRequests) {
        Write-Host "::group::PR #$($pullRequest.number) ($($pullRequest.baseRefName) @ $($pullRequest.headRefOid))"
        try {
            & "$PSScriptRoot/set-pr-status.ps1" -HeadSha $pullRequest.headRefOid `
                -BaseRef $pullRequest.baseRefName
        }
        catch {
            Write-Host "::warning::Failed to stamp PR #$($pullRequest.number)"
            $failed = $true
        }
        Write-Host '::endgroup::'
    }

    # Step 3: Fail the workflow only after every PR has been attempted.
    return [int]$failed
}

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GitHubPullRequestsClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubRepositoryClient.psm1') -Force
$exitCode = Invoke-Main
exit $exitCode
