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

function Get-CodePointStartIndex {
    param([Parameter(Mandatory)][string]$Text)

    $indexes = [System.Collections.Generic.List[int]]::new()
    for ($index = 0; $index -lt $Text.Length; $index++) {
        $indexes.Add($index)
        if (
            [char]::IsHighSurrogate($Text[$index]) -and
            $index + 1 -lt $Text.Length -and
            [char]::IsLowSurrogate($Text[$index + 1])
        ) {
            $index++
        }
    }

    return $indexes.ToArray()
}

function Get-StatusDescription {
    param([Parameter(Mandatory)]$Details)

    $description = if ([string]::IsNullOrEmpty($Details.Actor)) {
        "Frozen: $($Details.Reason)"
    }
    else {
        "Frozen by @$($Details.Actor): $($Details.Reason)"
    }

    $codePointIndexes = @(Get-CodePointStartIndex $description)
    if ($codePointIndexes.Count -gt 140) {
        return $description.Substring(0, $codePointIndexes[137]) + '...'
    }

    return $description
}

function Invoke-Main {
    # Step 1: Locate the permanent tracking issue for the PR's target branch.
    $repository = Get-RepositoryName
    $issue = Get-BranchFreezeIssue -Repository $repository -Branch $BaseRef

    # Step 2: Mark the required status green when the issue is absent or closed.
    if (-not (Test-BranchFreezeIssueOpen -Issue $issue)) {
        Write-Host "Branch '$BaseRef' is open -> reporting success on $HeadSha"
        Set-BranchFreezeCommitStatus -Repository $repository -HeadSha $HeadSha `
            -State 'success' -Description 'Branch open'
        return 0
    }

    # Step 3: Mark the required status red using the issue's current reason.
    $details = Get-BranchFreezeDetails -Issue $issue
    $description = Get-StatusDescription -Details $details
    Write-Host "Branch '$BaseRef' is FROZEN -> reporting failure on $HeadSha"
    Set-BranchFreezeCommitStatus -Repository $repository -HeadSha $HeadSha `
        -State 'failure' -Description $description -TargetUrl $issue.url
    return 0
}

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BranchFreeze.psm1') -Force
$exitCode = Invoke-Main
exit $exitCode
