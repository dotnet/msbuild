#!/usr/bin/env pwsh
# Exits 0 if <actor-login> appears in the branch-freeze allowlist, non-zero otherwise.
#
# Usage:   is-allowed.ps1 <actor-login> [allowlist-path]
#          allowlist-path defaults to .github/branch-freeze-allowlist.txt
#
# The allowlist has one GitHub login per line; blank lines and `#` comments are
# ignored, matching is case-insensitive, and all whitespace is removed.
# Exit codes: 0 = allowed, 1 = not allowed, 2 = allowlist file missing.
[CmdletBinding()]
param(
  [Parameter(Mandatory, Position = 0)][string]$Actor,
  [Parameter(Position = 1)][string]$AllowlistPath = '.github/branch-freeze-allowlist.txt'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $AllowlistPath -PathType Leaf)) {
  [Console]::Error.WriteLine("Allowlist file '$AllowlistPath' not found.")
  exit 2
}

foreach ($line in Get-Content -LiteralPath $AllowlistPath) {
  $login = [regex]::Replace(($line -split '#', 2)[0], '\s', '')
  if ([string]::IsNullOrEmpty($login)) {
    continue
  }

  if ([StringComparer]::OrdinalIgnoreCase.Equals($login, $Actor)) {
    exit 0
  }
}

exit 1
