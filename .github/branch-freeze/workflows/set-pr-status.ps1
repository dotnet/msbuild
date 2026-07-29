#!/usr/bin/env pwsh
# Posts the `branch-freeze` commit status for one pull request head commit.
#
# A branch is frozen when its permanent `Branch freeze: <branch>` tracking issue
# is open. The current issue body supplies the actor and reason shown in the
# required status description.
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0, ParameterSetName = 'Commit')][string]$HeadSha,
    [Parameter(Mandatory, Position = 1, ParameterSetName = 'Commit')][string]$BaseRef,
    [Parameter(Mandatory, ParameterSetName = 'PullRequest')][string]$PullRequestNumber,
    [Parameter(ParameterSetName = 'PullRequest')][AllowEmptyString()][string]$FallbackHeadSha = '',
    [Parameter(ParameterSetName = 'PullRequest')][AllowEmptyString()][string]$FallbackBaseRef = ''
)

function Invoke-Main {
    [OutputType([int])]
    param()

    # Step 1: Locate the permanent tracking issue for the PR's target branch.
    $repository = Get-GitHubRepositoryName
    $freezeState = Get-BranchFreezeState -Repository $repository -Branch $BaseRef

    # Step 2: Mark the required status green when the issue is absent or closed.
    if (-not $freezeState.IsFrozen) {
        Write-Host "Branch '$BaseRef' is open -> reporting success on $HeadSha"
        Set-GitHubCommitStatus -Repository $repository -HeadSha $HeadSha `
            -State 'success' -Context 'branch-freeze' -Description 'Branch open'
        return 0
    }

    # Step 3: Mark the required status red using the issue's current reason.
    $description = Get-BranchFreezeStatusDescription -Details $freezeState
    Write-Host "Branch '$BaseRef' is FROZEN -> reporting failure on $HeadSha"
    Set-GitHubCommitStatus -Repository $repository -HeadSha $HeadSha `
        -State 'failure' -Context 'branch-freeze' -Description $description `
        -TargetUrl $freezeState.Url
    return 0
}

$ErrorActionPreference = 'Stop'
$componentsDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'components'
Import-Module (Join-Path $componentsDirectory 'BranchFreeze.psm1') -Force
Import-Module (Join-Path $componentsDirectory 'issue-comments/BranchFreezeCommentComposer.psm1') -Force
Import-Module (Join-Path $componentsDirectory 'github/GitHubPullRequestsClient.psm1') -Force
Import-Module (Join-Path $componentsDirectory 'github/GitHubRepositoryClient.psm1') -Force
Import-Module (Join-Path $componentsDirectory 'github/GitHubStatusChecksClient.psm1') -Force

if ($PSCmdlet.ParameterSetName -eq 'PullRequest') {
    # Resolve these after the workflow has acquired the global status-writer
    # lock. Event payloads can be stale when a PR is retargeted repeatedly, and
    # GitHub does not guarantee that queued workflow jobs start in dispatch order.
    $HeadSha = ''
    $BaseRef = ''
    try {
        $pullRequest = Get-GitHubPullRequest -Repository (Get-GitHubRepositoryName) `
            -Number $PullRequestNumber
        $HeadSha = [string]$pullRequest.headRefOid
        $BaseRef = [string]$pullRequest.baseRefName
    }
    catch {
        Write-Host "::warning::Could not read pull request #$PullRequestNumber ($($_.Exception.Message))."
    }

    # The live read is authoritative but must never be able to prevent a write,
    # for the same reason the current-status read cannot: a stale status is
    # corrected by the next event or by the scheduled sweep, while a missing one
    # leaves the required check unanswered and the pull request unmergeable.
    if ([string]::IsNullOrEmpty($HeadSha) -or [string]::IsNullOrEmpty($BaseRef)) {
        if ([string]::IsNullOrEmpty($FallbackHeadSha) -or [string]::IsNullOrEmpty($FallbackBaseRef)) {
            throw "Could not resolve the head commit and base branch of pull request #$PullRequestNumber."
        }

        Write-Host "::warning::Falling back to the event payload head and base for pull request #$PullRequestNumber."
        $HeadSha = $FallbackHeadSha
        $BaseRef = $FallbackBaseRef
    }
}

$exitCode = Invoke-Main
exit $exitCode
