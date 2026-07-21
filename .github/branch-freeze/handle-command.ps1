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

function Add-Reaction {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$CommentId,
        [Parameter(Mandatory)][string]$Content
    )

    & gh api -X POST "repos/$Repository/issues/comments/$CommentId/reactions" `
        -f "content=$Content" *> $null
}

function Add-Reply {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$IssueNumber,
        [Parameter(Mandatory)][string]$Message
    )

    & gh issue comment $IssueNumber --repo $Repository --body $Message *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "::warning::Failed to post confirmation comment on issue #$IssueNumber."
    }
}

function Write-WorkflowOutput {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Value
    )

    if (-not [string]::IsNullOrEmpty($env:GITHUB_OUTPUT)) {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "$Name=$Value" -Encoding utf8
    }
}

function Get-CommandRequest {
    param([AllowEmptyString()][string]$Body)

    $line = ([regex]::Split($Body, '\r?\n', 2)[0]).TrimEnd("`r")
    if ($line -notmatch '^(\S+)(?:\s+(.*))?$') {
        return $null
    }

    $command = $Matches[1]
    $remaining = if ($Matches.Count -gt 2) { $Matches[2] } else { '' }
    $action = switch -CaseSensitive ($command) {
        '/freeze' { 'freeze' }
        '/unfreeze' { 'unfreeze' }
        default { return $null }
    }

    if ($remaining -match '^(--branch|-b)$') {
        throw "Missing a branch name after ``$remaining``."
    }
    if ($remaining -match '^(?:--branch|-b)\s+$') {
        throw 'No branch name was given after the `--branch` flag.'
    }

    $branch = 'main'
    if ($remaining -match '^(?:--branch|-b)\s+(\S+)(?:\s+(.*))?$') {
        $branch = $Matches[1]
        $remaining = if ($Matches.Count -gt 2) { $Matches[2] } else { '' }
    }

    return [pscustomobject]@{
        Action = $action
        Branch = $branch
        Reason = $remaining.TrimEnd()
    }
}

function Test-BranchExists {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch
    )

    if ($Branch -notmatch '^[A-Za-z0-9._/-]+$') {
        return $false
    }

    $encodedBranch = [uri]::EscapeDataString($Branch)
    & gh api "repos/$Repository/branches/$encodedBranch" *> $null
    return $LASTEXITCODE -eq 0
}

function Get-Usage {
    return @'
**Usage**
- `/freeze [--branch <name>] <reason>` — freeze a branch (default `main`); a reason is required.
- `/unfreeze [--branch <name>]` — unfreeze a branch (default `main`).
'@
}

function Invoke-Main {
    # Step 1: Read values supplied by the command workflow.
    $repository = Get-RequiredEnvironmentValue 'REPO'
    $actor = Get-RequiredEnvironmentValue 'ACTOR'
    $sourceIssueNumber = Get-RequiredEnvironmentValue 'ISSUE_NUMBER'
    $commentId = Get-RequiredEnvironmentValue 'COMMENT_ID'
    $null = Get-RequiredEnvironmentValue 'GH_TOKEN'
    $body = if ($null -eq $env:BODY) { '' } else { $env:BODY }

    # Step 2: Parse the first line into an action, branch, and optional reason.
    try {
        $request = Get-CommandRequest -Body $body
    }
    catch {
        Add-Reaction -Repository $repository -CommentId $commentId -Content 'confused'
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber `
            -Message "$($_.Exception.Message)`n`n$(Get-Usage)"
        return 0
    }
    if ($null -eq $request) {
        Write-Host 'Ignoring comment because its first token is not a freeze command.'
        return 0
    }

    # Step 3: Validate the requested branch and freeze reason.
    if (-not (Test-BranchExists -Repository $repository -Branch $request.Branch)) {
        Add-Reaction -Repository $repository -CommentId $commentId -Content 'confused'
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber `
            -Message "Branch ``$($request.Branch)`` was not found in ``$repository``.`n`n$(Get-Usage)"
        return 0
    }
    if ($request.Action -eq 'freeze' -and [string]::IsNullOrEmpty($request.Reason)) {
        Add-Reaction -Repository $repository -CommentId $commentId -Content 'confused'
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber `
            -Message "A reason is required to freeze ``$($request.Branch)``.`n`n$(Get-Usage)"
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
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber -Message (@'
❄️ **`{0}` is now frozen** by @{1} — permanent tracking issue #{2} ({3}).

> {4}

Pull requests targeting `{0}` will be blocked by the `branch-freeze` check until someone runs `/unfreeze --branch {0}` (or `/unfreeze` for `main`).
'@ -f $request.Branch, $actor, $issue.Number, $verb, $request.Reason)
        return 0
    }

    $result = Close-BranchFreezeIssue -Repository $repository -Branch $request.Branch `
        -Actor $actor
    if (-not $result.Changed) {
        Write-WorkflowOutput 'changed' 'false'
        Add-Reaction -Repository $repository -CommentId $commentId -Content '+1'
        Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber `
            -Message "``$($request.Branch)`` is not currently frozen — nothing to do."
        return 0
    }

    Write-WorkflowOutput 'branch' $request.Branch
    Write-WorkflowOutput 'changed' 'true'
    Add-Reaction -Repository $repository -CommentId $commentId -Content '+1'
    Add-Reply -Repository $repository -IssueNumber $sourceIssueNumber -Message (
        '✅ **`{0}` is now unfrozen** by @{1} — closed permanent tracking issue #{2}. The `branch-freeze` check now passes on open PRs targeting `{0}`.' -f
            $request.Branch, $actor, $result.Number
    )
    return 0
}

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BranchFreeze.psm1') -Force
$exitCode = Invoke-Main
exit $exitCode
