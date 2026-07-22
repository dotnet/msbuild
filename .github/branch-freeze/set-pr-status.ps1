#!/usr/bin/env pwsh
# Posts the `branch-freeze` commit status for one pull request head commit.
#
# A branch is frozen when its permanent `Branch freeze: <branch>` tracking issue
# is open. The current issue body supplies the actor and reason shown in the
# required status description.
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$HeadSha,
    [Parameter(Mandatory, Position = 1)][string]$BaseRef
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
Import-Module (Join-Path $PSScriptRoot 'BranchFreeze.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'BranchFreezeCommentComposer.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubRepositoryClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubStatusChecksClient.psm1') -Force
$exitCode = Invoke-Main
exit $exitCode
