#!/usr/bin/env pwsh
# Rejects a branch-freeze command from a commenter who is not on the allowlist.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$CommentId,
    [Parameter(Mandatory)][string]$IssueNumber,
    [Parameter(Mandatory)][string]$Actor
)

function Invoke-Main {
    [OutputType([int])]
    param()

    try {
        Add-GitHubCommentReaction -Repository $Repository -CommentId $CommentId `
            -Content '-1'
    }
    catch {
        Write-Warning "Failed to react to comment $CommentId."
    }

    $message = New-BranchFreezeAuthorizationDenial -Actor $Actor
    Add-GitHubIssueComment -Repository $Repository -IssueNumber $IssueNumber `
        -Body $message
    return 0
}

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BranchFreezeCommentComposer.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubIssuesClient.psm1') -Force
$exitCode = Invoke-Main
exit $exitCode