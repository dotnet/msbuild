#!/usr/bin/env pwsh
# Refreshes the `branch-freeze` status on every open pull request, optionally
# limited to a single base branch. Calls set-pr-status.ps1 once per PR and
# aggregates failures into a non-zero exit. Used by branch-freeze-refresh.yml
# for rollout seeding, manual re-sync, and /freeze or /unfreeze fan-out.
#
# Usage:  refresh-pr-statuses.ps1 [base-ref] # blank base-ref = all open PRs
# Env:    GH_TOKEN (required), REPO (default: $env:GITHUB_REPOSITORY)
[CmdletBinding()]
param(
  [Parameter(Position = 0)][AllowEmptyString()][string]$BaseRef = ''
)

$ErrorActionPreference = 'Stop'

function Invoke-GitHubCli {
  param([Parameter(Mandatory)][string[]]$Arguments)

  $output = & gh @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "gh command failed with exit code $LASTEXITCODE."
  }

  return $output -join [Environment]::NewLine
}

# Step 1: Read the optional branch filter and repository.
$repo = if (-not [string]::IsNullOrEmpty($env:REPO)) {
  $env:REPO
} elseif (-not [string]::IsNullOrEmpty($env:GITHUB_REPOSITORY)) {
  $env:GITHUB_REPOSITORY
} else {
  throw 'REPO or GITHUB_REPOSITORY must be set.'
}
$env:REPO = $repo

# Step 2: Build a query for open PRs, optionally limited to one base branch.
$arguments = @(
  'pr', 'list',
  '--repo', $repo,
  '--state', 'open',
  '--limit', '1000',
  '--json', 'number,headRefOid,baseRefName'
)
if (-not [string]::IsNullOrEmpty($BaseRef)) {
  $arguments += @('--base', $BaseRef)
}

# Step 3: Load the matching PRs and report how many will be refreshed.
$pullRequestsJson = Invoke-GitHubCli -Arguments $arguments
$pullRequests = @($pullRequestsJson | ConvertFrom-Json)
$suffix = if ([string]::IsNullOrEmpty($BaseRef)) { '' } else { " targeting $BaseRef" }
Write-Output "Stamping $($pullRequests.Count) open PR(s)$suffix"
if ($pullRequests.Count -ge 1000) {
  Write-Output "::warning::Open PR count hit the query limit ($($pullRequests.Count)); some PRs may not have been stamped."
}

# Step 4: Refresh the required status on each PR's current head commit.
$failed = $false
foreach ($pullRequest in $pullRequests) {
  $number = $pullRequest.number
  $headSha = $pullRequest.headRefOid
  $base = $pullRequest.baseRefName
  Write-Output "::group::PR #$number ($base @ $headSha)"

  # Remember any failure, but continue refreshing the remaining PRs.
  try {
    & "$PSScriptRoot/set-pr-status.ps1" -HeadSha $headSha -BaseRef $base
  } catch {
    Write-Output "::warning::Failed to stamp PR #$number"
    $failed = $true
  }

  Write-Output '::endgroup::'
}

# Step 5: Fail the workflow if any PR status could not be refreshed.
if ($failed) {
  exit 1
}

exit 0
