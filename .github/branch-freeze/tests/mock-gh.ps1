#!/usr/bin/env pwsh
# Minimal mock of the GitHub CLI (`gh`) for the branch-freeze tests.
# Implements ONLY the gh surface used by the scripts under test:
#   * `gh issue list ... --json ...`                   -> prints $env:MOCK_ISSUES (default [])
#   * `gh issue create/edit/reopen/close/comment ...`  -> records the operation in
#                                                         $env:GH_ISSUE_FILE
#   * `gh issue comment ...`                           -> fails when
#                                                         $env:MOCK_ISSUE_COMMENT_FAILURE=1
#   * `gh api -X POST .../statuses/<sha> -f key=value` -> appends each key=value
#                                                        line to $env:GH_STATUS_FILE.
#   * any command                                      -> fails while the counter in
#                                                        $env:MOCK_TRANSIENT_FAILURE_FILE
#                                                        is greater than zero.
[CmdletBinding()]
param(
  [Parameter(ValueFromRemainingArguments)][string[]]$CliArguments
)

$ErrorActionPreference = 'Stop'

if (-not [string]::IsNullOrEmpty($env:MOCK_TRANSIENT_FAILURE_FILE)) {
  $failuresRemaining = [int](Get-Content -LiteralPath $env:MOCK_TRANSIENT_FAILURE_FILE -Raw)
  if ($failuresRemaining -gt 0) {
    Set-Content -LiteralPath $env:MOCK_TRANSIENT_FAILURE_FILE `
      -Value ($failuresRemaining - 1) -Encoding utf8
    exit 1
  }
}

function Get-OptionValue {
  param(
    [Parameter(Mandatory)][string[]]$Arguments,
    [Parameter(Mandatory)][string]$Name
  )

  $index = [Array]::IndexOf($Arguments, $Name)
  if ($index -ge 0 -and $index + 1 -lt $Arguments.Count) {
    return $Arguments[$index + 1]
  }

  return $null
}

function Write-IssueOperation {
  param(
    [Parameter(Mandatory)][string]$Command,
    [Parameter(Mandatory)][string[]]$Arguments
  )

  if ([string]::IsNullOrEmpty($env:GH_ISSUE_FILE)) {
    return
  }

  $record = [ordered]@{
    command = $Command
    number = if ($Arguments.Count -gt 0 -and -not $Arguments[0].StartsWith('-')) { $Arguments[0] } else { $null }
    label = if ($null -ne (Get-OptionValue $Arguments '--label')) {
        Get-OptionValue $Arguments '--label'
    } else {
        Get-OptionValue $Arguments '--add-label'
    }
    title = Get-OptionValue $Arguments '--title'
    body = Get-OptionValue $Arguments '--body'
    comment = Get-OptionValue $Arguments '--comment'
  }
  Add-Content -LiteralPath $env:GH_ISSUE_FILE -Value ($record | ConvertTo-Json -Compress) -Encoding utf8
}

if ($CliArguments.Count -eq 0) {
  exit 0
}

$command = $CliArguments[0]
switch -CaseSensitive ($command) {
  'issue' {
    $subcommand = if ($CliArguments.Count -gt 1) { $CliArguments[1] } else { '' }
    switch -CaseSensitive ($subcommand) {
      'list' {
        Write-Output $(if ([string]::IsNullOrEmpty($env:MOCK_ISSUES)) { '[]' } else { $env:MOCK_ISSUES })
      }
      'comment' {
        if ($env:MOCK_ISSUE_COMMENT_FAILURE -eq '1') {
          exit 1
        }
        Write-IssueOperation -Command $subcommand -Arguments $CliArguments[2..($CliArguments.Count - 1)]
      }
      'create' {
        Write-IssueOperation -Command $subcommand -Arguments $CliArguments[2..($CliArguments.Count - 1)]
        Write-Output $(if ([string]::IsNullOrEmpty($env:MOCK_ISSUE_CREATE_URL)) {
          'https://github.com/o/r/issues/123'
        } else {
          $env:MOCK_ISSUE_CREATE_URL
        })
      }
      'edit' {
        Write-IssueOperation -Command $subcommand -Arguments $CliArguments[2..($CliArguments.Count - 1)]
      }
      'reopen' {
        Write-IssueOperation -Command $subcommand -Arguments $CliArguments[2..($CliArguments.Count - 1)]
      }
      'close' {
        Write-IssueOperation -Command $subcommand -Arguments $CliArguments[2..($CliArguments.Count - 1)]
      }
    }
  }
  'pr' {
    if ($CliArguments.Count -gt 1 -and $CliArguments[1] -eq 'list') {
      Write-Output $(if ([string]::IsNullOrEmpty($env:MOCK_PRS)) { '[]' } else { $env:MOCK_PRS })
    }
  }
  'api' {
    $endpoint = ''
    $fields = @()

    for ($index = 1; $index -lt $CliArguments.Count; $index++) {
      switch ($CliArguments[$index]) {
        '-f' {
          $fields += $CliArguments[$index + 1]
          $index++
        }
        '-X' {
          $index++
        }
        '--jq' {
          $index++
        }
        default {
          if (-not $CliArguments[$index].StartsWith('-')) {
            $endpoint = $CliArguments[$index]
          }
        }
      }
    }

    if ($endpoint -like '*/statuses/*') {
      if (
        -not [string]::IsNullOrEmpty($env:MOCK_STATUS_FAILURE_SHA) -and
        $endpoint.EndsWith("/$($env:MOCK_STATUS_FAILURE_SHA)", [StringComparison]::Ordinal)
      ) {
        exit 1
      }
      if (-not [string]::IsNullOrEmpty($env:GH_STATUS_FILE)) {
        Add-Content -LiteralPath $env:GH_STATUS_FILE -Value $fields -Encoding utf8
      }
    } else {
      Write-Output '{}'
    }
  }
}

exit 0
