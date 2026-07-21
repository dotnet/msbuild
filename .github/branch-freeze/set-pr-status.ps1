#!/usr/bin/env pwsh
# Posts the `branch-freeze` commit status for a single pull request head commit.
#
# Usage:   set-pr-status.ps1 <head-sha> <base-ref>
# Env:     GH_TOKEN  (required) token with `statuses: write` + `issues: read`
#          REPO      (optional) owner/repo; defaults to $env:GITHUB_REPOSITORY
#
# A branch is considered FROZEN while an open issue labeled `branch-freeze`
# contains the marker `<!-- branch-freeze:<branch> -->` on a line by itself
# (surrounding whitespace / CR tolerated). The remaining issue body is used as
# the human-readable reason, and an optional `<!-- branch-freeze-by:<login> -->`
# marker names who froze it (surfaced in the status description). Matching the
# marker as a WHOLE LINE - not a substring - prevents an issue that merely
# mentions the marker from freezing a branch.
[CmdletBinding()]
param(
  [Parameter(Mandatory, Position = 0)][string]$HeadSha,
  [Parameter(Mandatory, Position = 1)][string]$BaseRef
)

$ErrorActionPreference = 'Stop'

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

# Step 1: Read the PR commit, target branch, and repository.
$repo = if (-not [string]::IsNullOrEmpty($env:REPO)) {
  $env:REPO
} elseif (-not [string]::IsNullOrEmpty($env:GITHUB_REPOSITORY)) {
  $env:GITHUB_REPOSITORY
} else {
  throw 'REPO or GITHUB_REPOSITORY must be set.'
}

# Step 2: Define the required check name and tracking issue marker.
$context = 'branch-freeze'
$label = 'branch-freeze'
$marker = "<!-- branch-freeze:$BaseRef -->"

# Step 3: Load all open branch-freeze tracking issues.
$issuesJson = Invoke-GitHubCli -Arguments @(
  'issue', 'list',
  '--repo', $repo,
  '--label', $label,
  '--state', 'open',
  '--limit', '200',
  '--json', 'number,body,url'
)
$issues = @($issuesJson | ConvertFrom-Json)

# Step 4: Find the tracking issue for this PR's target branch.
$trackingIssue = $issues |
  Where-Object { Test-BodyContainsMarker -Body $_.body -Marker $marker } |
  Select-Object -First 1

if ($null -ne $trackingIssue) {
  # Step 5a: The branch is frozen. Read the issue details for the check message.
  $issueUrl = $trackingIssue.url
  $body = [string]$trackingIssue.body

  # Who froze it, from the machine-readable actor marker (if present).
  $actorMatch = [regex]::Match(
    $body,
    '(?m)^\s*<!--\s*branch-freeze-by:\s*([^\s>]+)\s*-->\s*$'
  )
  $actor = if ($actorMatch.Success) { $actorMatch.Groups[1].Value } else { '' }

  # Human-readable reason = body minus any branch-freeze marker lines.
  $reasonLines = @(
    [regex]::Split($body, '\r?\n') |
      Where-Object {
        -not [regex]::IsMatch($_, '^\s*<!--\s*branch-freeze.*-->\s*$')
      }
  )
  $reason = (($reasonLines -join ' ') -replace '\s+', ' ').Trim()
  if ([string]::IsNullOrEmpty($reason)) {
    $reason = '(no reason provided)'
  }

  $description = if ([string]::IsNullOrEmpty($actor)) {
    "Frozen: $reason"
  } else {
    "Frozen by @$actor`: $reason"
  }

  # GitHub truncates status descriptions at 140 chars; trim without splitting
  # a Unicode surrogate pair, matching Bash's character-based slicing.
  $codePointIndexes = @(Get-CodePointStartIndex $description)
  if ($codePointIndexes.Count -gt 140) {
    $description = $description.Substring(0, $codePointIndexes[137]) + '...'
  }

  # Step 6a: Mark the required check as failed and link the tracking issue.
  Write-Output "Branch '$BaseRef' is FROZEN -> reporting failure on $HeadSha"
  Invoke-GitHubCli -Arguments @(
    'api', '-X', 'POST', "repos/$repo/statuses/$HeadSha",
    '-f', 'state=failure',
    '-f', "context=$context",
    '-f', "description=$description",
    '-f', "target_url=$issueUrl"
  ) -DiscardOutput
} else {
  # Step 5b: The branch is open, so mark the required check as successful.
  Write-Output "Branch '$BaseRef' is open -> reporting success on $HeadSha"
  Invoke-GitHubCli -Arguments @(
    'api', '-X', 'POST', "repos/$repo/statuses/$HeadSha",
    '-f', 'state=success',
    '-f', "context=$context",
    '-f', 'description=Branch open'
  ) -DiscardOutput
}
