#!/usr/bin/env pwsh
# Handles a /freeze or /unfreeze command. Authorization is performed by the
# calling workflow BEFORE this script runs.
#
# Command is read from the FIRST line of the triggering comment ($env:BODY):
#   /freeze   [--branch <name>] <reason...>   branch defaults to `main`; reason required
#   /unfreeze [--branch <name>]               branch defaults to `main`
#
# Freeze state is stored as an open issue labeled `branch-freeze` whose body is
#   <reason>
#
#   <!-- branch-freeze:<branch> -->
# This script only toggles that state and replies; it emits `branch` and
# `changed` step outputs so the calling workflow can refresh the affected PR
# statuses via branch-freeze-refresh.yml.
#
# Env (required): GH_TOKEN, REPO, ACTOR, ISSUE_NUMBER, COMMENT_ID, BODY
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSUseBOMForUnicodeEncodedFile',
    '',
    Justification = 'A BOM before the shebang prevents direct execution on Linux.'
)]
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Get-RequiredEnvironmentValue {
  param([Parameter(Mandatory)][string]$Name)

  $value = [Environment]::GetEnvironmentVariable($Name)
  if ([string]::IsNullOrEmpty($value)) {
    throw "Environment variable '$Name' is required."
  }

  return $value
}

function Invoke-GitHubCli {
  param(
    [Parameter(Mandatory)][string[]]$Arguments,
    [switch]$DiscardOutput
  )

  $output = & gh @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "gh command failed with exit code $LASTEXITCODE."
  }

  if (-not $DiscardOutput) {
    return $output -join [Environment]::NewLine
  }
}

function Test-BodyContainsMarker {
  param(
    [AllowNull()][string]$Body,
    [Parameter(Mandatory)][string]$Marker
  )

  foreach ($line in [regex]::Split([string]$Body, '\r?\n')) {
    if ($line.Trim() -ceq $Marker) {
      return $true
    }
  }

  return $false
}

# Step 1: Read the values supplied by the command workflow.
$repo = Get-RequiredEnvironmentValue 'REPO'
$actor = Get-RequiredEnvironmentValue 'ACTOR'
$issueNumber = Get-RequiredEnvironmentValue 'ISSUE_NUMBER'
$commentId = Get-RequiredEnvironmentValue 'COMMENT_ID'
$null = Get-RequiredEnvironmentValue 'GH_TOKEN'
$body = if ($null -eq $env:BODY) { '' } else { $env:BODY }
$label = 'branch-freeze'

# Step 2: Define helpers for reacting, replying, and returning workflow outputs.
function Add-Reaction {
  param([Parameter(Mandatory)][string]$Content)

  & gh api -X POST "repos/$repo/issues/comments/$commentId/reactions" -f "content=$Content" *> $null
}

function Add-Reply {
  param([Parameter(Mandatory)][string]$Message)

  & gh issue comment $issueNumber --repo $repo --body $Message *> $null
  if ($LASTEXITCODE -ne 0) {
    Write-Output "::warning::Failed to post confirmation comment on issue #$issueNumber."
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

$usage = @'
**Usage**
- `/freeze [--branch <name>] <reason>` — freeze a branch (default `main`); a reason is required.
- `/unfreeze [--branch <name>]` — unfreeze a branch (default `main`).
'@

function Exit-WithUsage {
  param([Parameter(Mandatory)][string]$Message)

  Add-Reaction 'confused'
  Add-Reply "$Message`n`n$usage"
  exit 0
}

# Step 3: Read the command and arguments from the comment's first line.
$line = ([regex]::Split($body, '\r?\n', 2)[0]).TrimEnd("`r")
if ($line -match '^(\S+)(?:\s+(.*))?$') {
  $command = $Matches[1]
  $remaining = if ($Matches.Count -gt 2) { $Matches[2] } else { '' }
} else {
  $command = ''
  $remaining = ''
}

# Step 4: Decide whether this is a freeze or unfreeze request.
switch -CaseSensitive ($command) {
  '/freeze' { $action = 'freeze' }
  '/unfreeze' { $action = 'unfreeze' }
  default {
    Write-Output "Ignoring non-command first token: '$command'"
    exit 0
  }
}

# Step 5: Read the optional branch and the freeze reason.
$branch = 'main'
if ($remaining -match '^(--branch|-b)$') {
  Exit-WithUsage "Missing a branch name after ``$remaining``."
}

if ($remaining -match '^(?:--branch|-b)\s+$') {
  Exit-WithUsage 'No branch name was given after the `--branch` flag.'
}

if ($remaining -match '^(?:--branch|-b)\s+(\S+)(?:\s+(.*))?$') {
  $branch = $Matches[1]
  $remaining = if ($Matches.Count -gt 2) { $Matches[2] } else { '' }
}

$reason = $remaining.TrimEnd()

# Step 6: Validate the branch name and confirm that the branch exists.
if ($branch -notmatch '^[A-Za-z0-9._/-]+$') {
  Exit-WithUsage "Branch ``$branch`` contains unexpected characters."
}

& gh api "repos/$repo/branches/$branch" *> $null
if ($LASTEXITCODE -ne 0) {
  Exit-WithUsage "Branch ``$branch`` was not found in ``$repo``."
}

$marker = "<!-- branch-freeze:$branch -->"

# Step 7: Ensure the tracking label exists.
& gh label create $label --repo $repo --color B60205 --description 'Tracks a frozen branch' *> $null

# Step 8: Find any open tracking issues for this branch.
$issuesJson = Invoke-GitHubCli -Arguments @(
  'issue', 'list',
  '--repo', $repo,
  '--label', $label,
  '--state', 'open',
  '--limit', '200',
  '--json', 'number,body'
)
$issues = @($issuesJson | ConvertFrom-Json)
$existingNumbers = @(
  $issues |
    Where-Object { Test-BodyContainsMarker -Body $_.body -Marker $marker } |
    ForEach-Object { $_.number }
)

if ($action -eq 'freeze') {
  # Step 9a: Freeze the branch by creating or updating its tracking issue.
  if ([string]::IsNullOrEmpty($reason)) {
    Exit-WithUsage "A reason is required to freeze ``$branch``."
  }

  # The branch marker makes the issue machine-detectable; the actor marker records
  # who froze the branch so the blocking status can name them. Both marker lines
  # are stripped from the human-readable reason by set-pr-status.ps1.
  $issueBody = "$reason`n`n$marker`n<!-- branch-freeze-by:$actor -->"

  # Keep one tracking issue and close any duplicates.
  $primary = $null
  foreach ($number in $existingNumbers) {
    if ($null -eq $primary) {
      $primary = $number
    } else {
      & gh issue close $number --repo $repo --comment (
        'Superseded by #{0} (duplicate `{1}` freeze tracking issue).' -f $primary, $branch
      ) *> $null
    }
  }

  if ($null -ne $primary) {
    Invoke-GitHubCli -Arguments @(
      'issue', 'edit', [string]$primary,
      '--repo', $repo,
      '--body', $issueBody
    ) -DiscardOutput
    $trackingIssueNumber = $primary
    $verb = 'updated'
  } else {
    $url = Invoke-GitHubCli -Arguments @(
      'issue', 'create',
      '--repo', $repo,
      '--label', $label,
      '--title', "Branch frozen: $branch",
      '--body', $issueBody
    )
    $trackingIssueNumber = ($url.TrimEnd('/') -split '/')[-1]
    $verb = 'opened'
  }

  # Tell the workflow to refresh PR statuses, then acknowledge the command.
  Write-WorkflowOutput 'branch' $branch
  Write-WorkflowOutput 'changed' 'true'
  Add-Reaction '+1'
  Add-Reply (@'
❄️ **`{0}` is now frozen** by @{1} — tracking issue #{2} ({3}).

> {4}

Pull requests targeting `{0}` will be blocked by the `branch-freeze` check until someone runs `/unfreeze --branch {0}` (or `/unfreeze` for `main`).
'@ -f $branch, $actor, $trackingIssueNumber, $verb, $reason)
} else {
  # Step 9b: Unfreeze the branch by closing its tracking issues.
  $closedIssues = @()
  foreach ($number in $existingNumbers) {
    Invoke-GitHubCli -Arguments @(
      'issue', 'close', [string]$number,
      '--repo', $repo,
      '--comment', ('Unfrozen by @{0} via `/unfreeze`.' -f $actor)
    ) -DiscardOutput
    $closedIssues += "#$number"
  }

  if ($closedIssues.Count -eq 0) {
    # The branch was already open, so no PR status refresh is needed.
    Write-WorkflowOutput 'changed' 'false'
    Add-Reaction '+1'
    Add-Reply "``$branch`` is not currently frozen — nothing to do."
    exit 0
  }

  # Tell the workflow to refresh PR statuses, then acknowledge the command.
  Write-WorkflowOutput 'branch' $branch
  Write-WorkflowOutput 'changed' 'true'
  Add-Reaction '+1'
  Add-Reply (
    '✅ **`{0}` is now unfrozen** by @{1} — closed tracking issue(s) {2}. The `branch-freeze` check now passes on open PRs targeting `{0}`.' -f
      $branch, $actor, ($closedIssues -join ', ')
  )
}

exit 0
