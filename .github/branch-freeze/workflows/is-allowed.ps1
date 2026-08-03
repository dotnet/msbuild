#!/usr/bin/env pwsh
# Exits 0 if <actor-login> appears in the branch-freeze allowlist, non-zero otherwise.
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][string]$Actor,
    [Parameter(Position = 1)][string]$AllowlistPath = '.github/branch-freeze-allowlist.txt'
)

function Test-BranchFreezeAuthorization {
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string]$Login,
        [Parameter(Mandatory)][string]$AllowlistPath
    )

    foreach ($line in Get-Content -LiteralPath $AllowlistPath) {
        $allowedLogin = [regex]::Replace(($line -split '#', 2)[0], '\s', '')
        if (
            -not [string]::IsNullOrEmpty($allowedLogin) -and
            [StringComparer]::OrdinalIgnoreCase.Equals($allowedLogin, $Login)
        ) {
            return $true
        }
    }

    return $false
}

function Invoke-Main {
    [OutputType([int])]
    param()

    # Step 1: Deny safely when the checked-in allowlist is unavailable.
    if (-not (Test-Path -LiteralPath $AllowlistPath -PathType Leaf)) {
        [Console]::Error.WriteLine("Allowlist file '$AllowlistPath' not found.")
        return 2
    }

    # Step 2: Return an exit code that the command workflow can authorize.
    return [int](-not (
        Test-BranchFreezeAuthorization -Login $Actor -AllowlistPath $AllowlistPath
    ))
}

$ErrorActionPreference = 'Stop'
$exitCode = Invoke-Main
exit $exitCode
