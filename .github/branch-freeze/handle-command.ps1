#!/usr/bin/env pwsh
# Handles a /freeze or /unfreeze command. Authorization is performed by the
# calling workflow BEFORE this script runs.
#
# Command is read from the FIRST line of the triggering comment ($env:BODY):
#   /freeze   [--branch <name>] <reason...>   branch defaults to `main`; reason required
#   /unfreeze [--branch <name>]               branch defaults to `main`
#
# Each branch has one permanent tracking issue named `Branch freeze: <branch>`.
# Open means frozen; closed means open. The issue body shows the current state,
# while timeline comments preserve previous freeze and unfreeze transitions.
#
# Env (required): GH_TOKEN, REPO, ACTOR, ISSUE_NUMBER, COMMENT_ID, BODY
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSUseBOMForUnicodeEncodedFile',
    '',
    Justification = 'A BOM before the shebang prevents direct execution on Linux.'
)]
[CmdletBinding()]
param()

function Get-RequiredEnvironmentValue {
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Name)

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrEmpty($value)) {
        throw "Environment variable '$Name' is required."
    }

    return $value
}

function Add-Reaction {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$CommentId,
        [Parameter(Mandatory)][string]$Content
    )

    try {
        Add-GitHubCommentReaction -Repository $Repository -CommentId $CommentId `
            -Content $Content
    }
    catch {
        return
    }
}

function Add-Reply {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$IssueNumber,
        [Parameter(Mandatory)][string]$Message
    )

    try {
        Add-GitHubIssueComment -Repository $Repository -IssueNumber $IssueNumber `
            -Body $Message
    }
    catch {
        Write-Host "::warning::Failed to post confirmation comment on issue #$IssueNumber."
    }
}

function Write-WorkflowOutput {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Value
    )

    if (-not [string]::IsNullOrEmpty($env:GITHUB_OUTPUT)) {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$Name=$Value" -Encoding utf8
    }
}

function Invoke-Main {
    [OutputType([int])]
    param()

    # Step 1: Read values supplied by the command workflow.
    $repository = Get-RequiredEnvironmentValue 'REPO'
    $actor = Get-RequiredEnvironmentValue 'ACTOR'
    $sourceIssueNumber = Get-RequiredEnvironmentValue 'ISSUE_NUMBER'
    $commentId = Get-RequiredEnvironmentValue 'COMMENT_ID'
    $null = Get-RequiredEnvironmentValue 'GH_TOKEN'
    $body = if ($null -eq $env:BODY) { '' } else { $env:BODY }

    # Step 2: Parse the first line into an action, branch, and optional reason.
    try {
        $request = ConvertFrom-BranchFreezeCommand -Body $body
    }
    catch {
        Add-Reaction -Repository $repository -CommentId $commentId -Content 'confused'
        $message = New-BranchFreezeErrorReply -Message $_.Exception.Message
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber -Message $message
        return 0
    }
    if ($null -eq $request) {
        Write-Host 'Ignoring comment because its first token is not a freeze command.'
        return 0
    }

    # Step 3: Validate the requested branch and freeze reason.
    if (-not (Test-GitHubBranchExists -Repository $repository -Branch $request.Branch)) {
        Add-Reaction -Repository $repository -CommentId $commentId -Content 'confused'
        $message = New-BranchFreezeErrorReply `
            -Message "Branch ``$($request.Branch)`` was not found in ``$repository``."
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber -Message $message
        return 0
    }
    if ($request.Action -eq 'freeze' -and [string]::IsNullOrEmpty($request.Reason)) {
        Add-Reaction -Repository $repository -CommentId $commentId -Content 'confused'
        $message = New-BranchFreezeErrorReply `
            -Message "A reason is required to freeze ``$($request.Branch)``."
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber -Message $message
        return 0
    }

    # Step 4: Reopen or close the branch's permanent tracking issue.
    if ($request.Action -eq 'freeze') {
        $issue = Open-BranchFreezeIssue -Repository $repository -Branch $request.Branch `
            -Actor $actor -Reason $request.Reason

        Write-WorkflowOutput 'branch' $request.Branch
        Write-WorkflowOutput 'changed' 'true'
        Add-Reaction -Repository $repository -CommentId $commentId -Content '+1'
        $verb = if ($issue.WasCreated) {
            'created'
        }
        elseif ($issue.WasReopened) {
            'reopened'
        }
        else {
            'updated'
        }
        $message = New-BranchFreezeConfirmation -Branch $request.Branch -Actor $actor `
            -IssueNumber ([string]$issue.Number) -ChangeDescription $verb `
            -Reason $request.Reason
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber -Message $message
        return 0
    }

    $result = Close-BranchFreezeIssue -Repository $repository -Branch $request.Branch `
        -Actor $actor
    if (-not $result.Changed) {
        Write-WorkflowOutput 'changed' 'false'
        Add-Reaction -Repository $repository -CommentId $commentId -Content '+1'
        $message = New-BranchAlreadyOpenReply -Branch $request.Branch
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber -Message $message
        return 0
    }

    Write-WorkflowOutput 'branch' $request.Branch
    Write-WorkflowOutput 'changed' 'true'
    Add-Reaction -Repository $repository -CommentId $commentId -Content '+1'
    $message = New-BranchUnfreezeConfirmation -Branch $request.Branch -Actor $actor `
        -IssueNumber ([string]$result.Number)
    Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber -Message $message
    return 0
}

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BranchFreeze.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'BranchFreezeCommentComposer.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'BranchFreezeCommentParser.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubIssuesClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubRepositoryClient.psm1') -Force
$exitCode = Invoke-Main
exit $exitCode
