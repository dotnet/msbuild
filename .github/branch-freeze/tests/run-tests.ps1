#!/usr/bin/env pwsh
# Security-focused unit tests for the branch-freeze authorization and status logic.
#
# These pin the three behaviors that matter most if they silently break:
#   * is-allowed.ps1     - who may run /freeze /unfreeze (deny-by-default).
#   * set-pr-status.ps1  - a branch is frozen ONLY when an open issue carries the
#                          marker on a WHOLE LINE, so a mere mention cannot freeze.
#   * handle-command.ps1 - notification failure cannot suppress refresh after a
#                          successful state change.
#
# Uses a PowerShell mock of `gh`; no live repository or external JSON tool is needed.
# Run:  pwsh .github/branch-freeze/tests/run-tests.ps1
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSUseBOMForUnicodeEncodedFile',
    '',
    Justification = 'A BOM before the shebang prevents direct execution on Linux.'
)]
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$testsDirectory = $PSScriptRoot
$branchFreezeDirectory = Split-Path -Parent $testsDirectory
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $branchFreezeDirectory)
$powerShell = (Get-Process -Id $PID).Path
$temporaryPaths = [System.Collections.Generic.List[string]]::new()
$passed = 0
$failed = 0

function Register-TestTemporaryFile {
    $path = [IO.Path]::GetTempFileName()
    $temporaryPaths.Add($path)
    return $path
}

function Add-Pass {
    param([Parameter(Mandatory)][string]$Message)

    $script:passed++
    Write-Output "  ok   - $Message"
}

function Add-Failure {
    param(
        [Parameter(Mandatory)][string]$Message,
        [string]$Detail = ''
    )

    $script:failed++
    Write-Output "  FAIL - $Message"
    if (-not [string]::IsNullOrEmpty($Detail)) {
        Write-Output "         $Detail"
    }
}

function Assert-Equal {
    param(
        [AllowNull()]$Actual,
        [AllowNull()]$Expected,
        [Parameter(Mandatory)][string]$Message
    )

    if ($Actual -ceq $Expected) {
        Add-Pass $Message
    }
    else {
        Add-Failure $Message "expected [$Expected] got [$Actual]"
    }
}

function Invoke-TestScript {
    param(
        [Parameter(Mandatory)][string]$Path,
        [string[]]$Arguments = @()
    )

    $output = & $powerShell -NoLogo -NoProfile -File $Path @Arguments
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Information $_ -InformationAction Continue }
    return $exitCode
}

function Initialize-TestStatusFile {
    $path = Register-TestTemporaryFile
    Clear-Content -LiteralPath $path
    $env:GH_STATUS_FILE = $path
    return $path
}

function Get-StatusField {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    $prefix = "$Name="
    $line = Get-Content -LiteralPath $Path |
        Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) } |
        Select-Object -First 1

    if ($null -eq $line) {
        return $null
    }

    return $line.Substring($prefix.Length)
}

function Get-WorkflowOutput {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Name
    )

    $prefix = "$Name="
    $line = Get-Content -LiteralPath $Path |
        Where-Object { $_.StartsWith($prefix, [StringComparison]::Ordinal) } |
        Select-Object -First 1

    if ($null -eq $line) {
        return $null
    }

    return $line.Substring($prefix.Length)
}

try {
    $mockDirectory = Join-Path ([IO.Path]::GetTempPath()) "branch-freeze-mock-$PID"
    New-Item -ItemType Directory -Path $mockDirectory | Out-Null
    $temporaryPaths.Add($mockDirectory)

    if ($IsWindows) {
        Copy-Item (Join-Path $testsDirectory 'mock-gh.ps1') (Join-Path $mockDirectory 'gh.ps1')
    }
    else {
        $mockPath = Join-Path $mockDirectory 'gh'
        $mockContent = (Get-Content -LiteralPath (Join-Path $testsDirectory 'mock-gh.ps1') -Raw).Replace("`r`n", "`n")
        [IO.File]::WriteAllText($mockPath, $mockContent, [Text.UTF8Encoding]::new($false))
        & chmod +x $mockPath
    }
    $env:PATH = "$mockDirectory$([IO.Path]::PathSeparator)$env:PATH"

    Write-Output '== is-allowed.ps1 (authorization boundary) =='
    $allowlist = Join-Path $repositoryRoot '.github/branch-freeze-allowlist.txt'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'is-allowed.ps1') @('rainersigwald', $allowlist)
    if ($code -eq 0) { Add-Pass 'listed login is allowed' } else { Add-Failure 'listed login is allowed' }

    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'is-allowed.ps1') @('RAINERSIGWALD', $allowlist)
    if ($code -eq 0) { Add-Pass 'match is case-insensitive' } else { Add-Failure 'match is case-insensitive' }

    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'is-allowed.ps1') @('not-a-real-user', $allowlist)
    if ($code -ne 0) { Add-Pass 'unknown login is denied' } else { Add-Failure 'unknown login is denied' }

    $emptyAllowlist = Register-TestTemporaryFile
    Set-Content -LiteralPath $emptyAllowlist -Value "# only a comment`n" -Encoding utf8
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'is-allowed.ps1') @('anyone', $emptyAllowlist)
    if ($code -ne 0) { Add-Pass 'empty allowlist denies (deny-by-default)' } else { Add-Failure 'empty allowlist denies (deny-by-default)' }

    $missingAllowlist = "$emptyAllowlist.missing"
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'is-allowed.ps1') @('anyone', $missingAllowlist)
    if ($code -eq 2) { Add-Pass 'missing allowlist file denies (exit 2)' } else { Add-Failure 'missing allowlist file denies (exit 2)' }

    Write-Output '== set-pr-status.ps1 (freeze detection) =='
    $env:REPO = 'o/r'

    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'set-pr-status.ps1') @('sha-open', 'main')
    Assert-Equal $code 0 'open branch status command succeeds'
    Assert-Equal (Get-StatusField $statusFile 'state') 'success' 'no tracking issue -> branch open (success)'

    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[{"number":7,"url":"https://github.com/o/r/issues/7","body":"SDK insertion broke\n\n<!-- branch-freeze:main -->\n<!-- branch-freeze-by:rainersigwald -->"}]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'set-pr-status.ps1') @('sha-frozen', 'main')
    Assert-Equal $code 0 'frozen branch status command succeeds'
    Assert-Equal (Get-StatusField $statusFile 'state') 'failure' 'whole-line marker -> branch frozen (failure)'
    Assert-Equal (Get-StatusField $statusFile 'description') 'Frozen by @rainersigwald: SDK insertion broke' 'status names who froze it'
    Assert-Equal (Get-StatusField $statusFile 'target_url') 'https://github.com/o/r/issues/7' 'status links the tracking issue'

    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[{"number":9,"url":"u","body":"heads up: <!-- branch-freeze:main --> is the marker"}]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'set-pr-status.ps1') @('sha-mention', 'main')
    Assert-Equal $code 0 'marker mention status command succeeds'
    Assert-Equal (Get-StatusField $statusFile 'state') 'success' 'marker mentioned mid-line does NOT freeze'

    $statusFile = Initialize-TestStatusFile
    $longReason = ('a' * 128) + '😀' + ('b' * 10)
    $env:MOCK_ISSUES = @(
        @{
            number = 10
            url = 'u'
            body = "$longReason`n`n<!-- branch-freeze:main -->"
        }
    ) | ConvertTo-Json -Compress
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'set-pr-status.ps1') @('sha-long', 'main')
    Assert-Equal $code 0 'long Unicode status command succeeds'
    Assert-Equal (
        Get-StatusField $statusFile 'description'
    ) ("Frozen: " + ('a' * 128) + '😀...') 'status truncation does not split a Unicode character'

    Write-Output '== handle-command.ps1 (tracking issue state) =='
    $commandOutput = Register-TestTemporaryFile
    $issueOperations = Register-TestTemporaryFile
    $env:MOCK_ISSUES = '[]'
    $env:MOCK_ISSUE_CREATE_URL = 'https://github.com/o/r/issues/123'
    $env:MOCK_ISSUE_COMMENT_FAILURE = '0'
    $env:GH_ISSUE_FILE = $issueOperations
    $env:GH_TOKEN = 'test-token'
    $env:ACTOR = 'rainersigwald'
    $env:ISSUE_NUMBER = '42'
    $env:COMMENT_ID = '99'
    $env:BODY = '/freeze --branch main SDK insertion broke'
    $env:GITHUB_OUTPUT = $commandOutput

    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'handle-command.ps1')
    Assert-Equal $code 0 'freeze command succeeds'
    Assert-Equal (Get-WorkflowOutput $commandOutput 'changed') 'true' 'freeze requests a PR status refresh'
    Assert-Equal (Get-WorkflowOutput $commandOutput 'branch') 'main' 'freeze returns the affected branch'
    $createdIssue = Get-Content -LiteralPath $issueOperations | Select-Object -First 1 | ConvertFrom-Json
    Assert-Equal $createdIssue.command 'create' 'freeze creates a tracking issue when none exists'
    Assert-Equal $createdIssue.label 'branch-freeze' 'freeze labels the tracking issue'
    Assert-Equal $createdIssue.title 'Branch frozen: main' 'freeze names the tracking issue'
    Assert-Equal $createdIssue.body "SDK insertion broke`n`n<!-- branch-freeze:main -->`n<!-- branch-freeze-by:rainersigwald -->" 'freeze stores reason, branch, and actor'

    $commandOutput = Register-TestTemporaryFile
    $env:MOCK_ISSUES = '[]'
    $env:BODY = '/unfreeze'
    $env:GITHUB_OUTPUT = $commandOutput
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'handle-command.ps1')
    Assert-Equal $code 0 'already-unfrozen command succeeds'
    Assert-Equal (Get-WorkflowOutput $commandOutput 'changed') 'false' 'already-unfrozen command skips PR status refresh'

    $commandOutput = Register-TestTemporaryFile
    $issueOperations = Register-TestTemporaryFile
    $env:MOCK_ISSUES = '[]'
    $env:GH_ISSUE_FILE = $issueOperations
    $env:BODY = '/freeze --branch   '
    $env:GITHUB_OUTPUT = $commandOutput
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'handle-command.ps1')
    Assert-Equal $code 0 'missing branch name is handled as a usage error'
    Assert-Equal (Get-WorkflowOutput $commandOutput 'changed') $null 'missing branch name does not request a refresh'
    if ([string]::IsNullOrEmpty((Get-Content -LiteralPath $issueOperations -Raw))) {
        Add-Pass 'missing branch name does not change tracking issue state'
    } else {
        Add-Failure 'missing branch name does not change tracking issue state'
    }

    Write-Output '== handle-command.ps1 (best-effort notification) =='
    $commandOutput = Register-TestTemporaryFile
    $env:MOCK_ISSUES = '[{"number":7,"body":"SDK insertion broke\n\n<!-- branch-freeze:main -->"}]'
    $env:MOCK_ISSUE_COMMENT_FAILURE = '1'
    $env:GH_TOKEN = 'test-token'
    $env:ACTOR = 'rainersigwald'
    $env:ISSUE_NUMBER = '42'
    $env:COMMENT_ID = '99'
    $env:BODY = '/unfreeze'
    $env:GITHUB_OUTPUT = $commandOutput

    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'handle-command.ps1')
    Assert-Equal $code 0 'failed confirmation reply does not abort unfreeze'
    Assert-Equal (Get-WorkflowOutput $commandOutput 'changed') 'true' 'failed confirmation reply preserves refresh output'
    Assert-Equal (Get-WorkflowOutput $commandOutput 'branch') 'main' 'failed confirmation reply preserves refresh branch'

    Write-Output '== refresh-pr-statuses.ps1 (bulk refresh) =='
    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[]'
    $env:MOCK_PRS = '[{"number":1,"headRefOid":"sha-1","baseRefName":"main"},{"number":2,"headRefOid":"sha-2","baseRefName":"main"}]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'refresh-pr-statuses.ps1') @('main')
    Assert-Equal $code 0 'bulk refresh succeeds'
    $statusWrites = @(Get-Content -LiteralPath $statusFile | Where-Object { $_ -eq 'state=success' })
    Assert-Equal $statusWrites.Count 2 'bulk refresh stamps every matching PR'
}
finally {
    foreach ($path in $temporaryPaths) {
        Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Output ''
Write-Output "Passed: $passed   Failed: $failed"
if ($failed -ne 0) {
    exit 1
}

exit 0
