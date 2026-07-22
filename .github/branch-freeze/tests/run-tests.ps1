#!/usr/bin/env pwsh
# Security-focused unit tests for the branch-freeze authorization and status logic.
#
# These pin the behaviors that matter most if they silently break:
#   * GitHubCli.psm1     - transient GitHub CLI failures are retried three times.
#   * is-allowed.ps1     - who may run /freeze /unfreeze (deny-by-default).
#   * handle-command.ps1 - untrusted branch and reason text cannot execute PowerShell.
#   * set-pr-status.ps1  - only the open permanent issue for the exact branch title
#                          freezes that branch.
#   * handle-command.ps1 - one issue is created per branch, then reopened/closed.
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

    Write-Output '== GitHub CLI transport (transient failures) =='
    Import-Module (Join-Path $branchFreezeDirectory 'GitHubCli.psm1') -Force
    $transientFailureFile = Register-TestTemporaryFile
    Set-Content -LiteralPath $transientFailureFile -Value 2 -Encoding utf8
    $env:MOCK_TRANSIENT_FAILURE_FILE = $transientFailureFile
    try {
        $issuesJson = Invoke-GitHubCli -Arguments @('issue', 'list')
        Assert-Equal $issuesJson '[]' 'CLI succeeds after two transient failures'
        Assert-Equal (
            [int](Get-Content -LiteralPath $transientFailureFile -Raw)
        ) 0 'CLI makes all three attempts'
    }
    finally {
        $env:MOCK_TRANSIENT_FAILURE_FILE = ''
    }

    Write-Output '== comment modules (pure parsing and composition) =='
    $branchFreezeModule = Import-Module `
        (Join-Path $branchFreezeDirectory 'BranchFreeze.psm1') -Force -PassThru
    Assert-Equal (
        @($branchFreezeModule.ExportedFunctions.Keys | Sort-Object) -join ','
    ) (
        'Close-BranchFreezeIssue,Get-BranchFreezeState,Open-BranchFreezeIssue'
    ) 'domain module exports only semantic branch-freeze operations'
    Import-Module (Join-Path $branchFreezeDirectory 'BranchFreezeCommentParser.psm1') -Force
    Import-Module (Join-Path $branchFreezeDirectory 'BranchFreezeCommentComposer.psm1') -Force

    $request = ConvertFrom-BranchFreezeCommand -Body '/freeze --branch vs17.14 SDK insertion broke'
    Assert-Equal $request.Action 'freeze' 'parser recognizes the freeze action'
    Assert-Equal $request.Branch 'vs17.14' 'parser reads an explicit branch'
    Assert-Equal $request.Reason 'SDK insertion broke' 'parser preserves the freeze reason'

    $request = ConvertFrom-BranchFreezeCommand -Body '/unfreeze'
    Assert-Equal $request.Action 'unfreeze' 'parser recognizes the unfreeze action'
    Assert-Equal $request.Branch 'main' 'parser defaults the branch to main'
    Assert-Equal $request.Reason '' 'parser returns no unfreeze reason'

    $issueBody = New-BranchFreezeIssueBody -Branch 'main' -Actor 'rainersigwald' `
        -Reason 'SDK insertion broke'
    $details = ConvertFrom-BranchFreezeIssueBody -Body $issueBody
    Assert-Equal $details.Actor 'rainersigwald' 'issue-body parser reads the actor'
    Assert-Equal $details.Reason 'SDK insertion broke' 'issue-body parser reads the reason'
    Assert-Equal (
        New-BranchFreezeAuditComment -Actor 'rainersigwald' -Reason 'SDK insertion broke'
    ) 'Frozen by @rainersigwald: SDK insertion broke' 'composer creates the audit comment'

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

    Write-Output '== deny-command.ps1 (unauthorized commenter) =='
    $issueOperations = Register-TestTemporaryFile
    $env:GH_ISSUE_FILE = $issueOperations
    $env:MOCK_ISSUE_COMMENT_FAILURE = '0'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'deny-command.ps1') @(
        '-Repository', 'o/r',
        '-CommentId', '99',
        '-IssueNumber', '42',
        '-Actor', 'not-allowed'
    )
    Assert-Equal $code 0 'authorization denial command succeeds'
    $denialComment = Get-Content -LiteralPath $issueOperations |
        ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object command -eq 'comment' |
        Select-Object -First 1
    Assert-Equal $denialComment.number '42' 'authorization denial replies on the source issue'
    Assert-Equal (
        $denialComment.body
    ) (
        'Sorry @not-allowed — branch freeze/unfreeze is restricted to accounts listed in `.github/branch-freeze-allowlist.txt`.'
    ) 'authorization denial explains the allowlist restriction'

    Write-Output '== set-pr-status.ps1 (freeze detection) =='
    $env:REPO = 'o/r'

    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'set-pr-status.ps1') @('sha-open', 'main')
    Assert-Equal $code 0 'open branch status command succeeds'
    Assert-Equal (Get-StatusField $statusFile 'state') 'success' 'no tracking issue -> branch open (success)'

    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[{"number":7,"title":"Branch freeze: main","url":"https://github.com/o/r/issues/7","state":"OPEN","body":"## Current state\n\nBranch `main` is **frozen** by @rainersigwald.\n\n### Reason\n\nSDK insertion broke\n\n<!-- branch-freeze-by:rainersigwald -->"}]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'set-pr-status.ps1') @('sha-frozen', 'main')
    Assert-Equal $code 0 'frozen branch status command succeeds'
    Assert-Equal (Get-StatusField $statusFile 'state') 'failure' 'open permanent issue -> branch frozen (failure)'
    Assert-Equal (Get-StatusField $statusFile 'description') 'Frozen by @rainersigwald: SDK insertion broke' 'status names who froze it'
    Assert-Equal (Get-StatusField $statusFile 'target_url') 'https://github.com/o/r/issues/7' 'status links the tracking issue'

    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[{"number":9,"title":"Branch freeze: main","url":"u","state":"CLOSED","body":"old reason"}]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'set-pr-status.ps1') @('sha-closed', 'main')
    Assert-Equal $code 0 'closed permanent issue status command succeeds'
    Assert-Equal (Get-StatusField $statusFile 'state') 'success' 'closed permanent issue -> branch open'

    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[{"number":9,"title":"Branch freeze: release","url":"u","state":"OPEN","body":"release only"}]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'set-pr-status.ps1') @('sha-other', 'main')
    Assert-Equal $code 0 'different branch issue status command succeeds'
    Assert-Equal (Get-StatusField $statusFile 'state') 'success' 'different exact title does not freeze the branch'

    $statusFile = Initialize-TestStatusFile
    $longReason = ('a' * 128) + '😀' + ('b' * 10)
    $env:MOCK_ISSUES = @(
        @{
            number = 10
            title = 'Branch freeze: main'
            url = 'u'
            state = 'OPEN'
            body = "## Current state`n`nBranch ``main`` is **frozen**.`n`n### Reason`n`n$longReason`n`n<!-- branch-freeze-by: -->"
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
    Assert-Equal $createdIssue.title 'Branch freeze: main' 'freeze gives the permanent issue a deterministic title'
    $expectedBody = @'
## Current state

Branch `main` is **frozen** by @rainersigwald.

### Reason

SDK insertion broke

<!-- branch-freeze-by:rainersigwald -->
'@
    Assert-Equal (
        ($createdIssue.body -replace "`r`n", "`n").TrimEnd("`r", "`n")
    ) (
        ($expectedBody -replace "`r`n", "`n").TrimEnd("`r", "`n")
    ) 'freeze stores a readable current state, reason, and actor'
    $createOperations = @(
        Get-Content -LiteralPath $issueOperations |
            ForEach-Object { $_ | ConvertFrom-Json }
    )
    Assert-Equal (
        @($createOperations | Where-Object command -eq 'comment').Count
    ) 2 'initial freeze records audit history and confirms the command'

    $commandOutput = Register-TestTemporaryFile
    $issueOperations = Register-TestTemporaryFile
    $env:MOCK_ISSUES = '[{"number":123,"title":"Branch freeze: main","url":"https://github.com/o/r/issues/123","state":"CLOSED","body":"Branch `main` is currently open."}]'
    $env:GH_ISSUE_FILE = $issueOperations
    $env:BODY = '/freeze New failure'
    $env:GITHUB_OUTPUT = $commandOutput
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'handle-command.ps1')
    Assert-Equal $code 0 'freeze reuses a closed permanent issue'
    $operations = @(Get-Content -LiteralPath $issueOperations | ForEach-Object { $_ | ConvertFrom-Json })
    Assert-Equal (@($operations | Where-Object command -eq 'create').Count) 0 'freeze does not create a second issue'
    Assert-Equal (@($operations | Where-Object command -eq 'edit').Count) 1 'freeze replaces the issue body with current state'
    Assert-Equal (@($operations | Where-Object command -eq 'reopen').Count) 1 'freeze reopens the permanent issue'
    Assert-Equal (@($operations | Where-Object command -eq 'comment').Count) 2 'freeze records history and confirms the command'
    $editedBody = ($operations | Where-Object command -eq 'edit' | Select-Object -First 1).body
    if ($editedBody -like '*New failure*') {
        Add-Pass 'reopened issue contains the new reason'
    }
    else {
        Add-Failure 'reopened issue contains the new reason'
    }

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
    $stateChanges = @(
        Get-Content -LiteralPath $issueOperations |
            ForEach-Object { $_ | ConvertFrom-Json } |
            Where-Object command -In @('create', 'edit', 'reopen', 'close')
    )
    if ($stateChanges.Count -eq 0) {
        Add-Pass 'missing branch name does not change tracking issue state'
    } else {
        Add-Failure 'missing branch name does not change tracking issue state'
    }

    Write-Output '== handle-command.ps1 (injection resistance) =='
    $commandOutput = Register-TestTemporaryFile
    $issueOperations = Register-TestTemporaryFile
    $injectionMarker = Register-TestTemporaryFile
    Remove-Item -LiteralPath $injectionMarker
    $payload = '$(Set-Content -LiteralPath "{0}" -Value injected)' -f $injectionMarker
    $env:MOCK_ISSUES = '[]'
    $env:GH_ISSUE_FILE = $issueOperations
    $env:BODY = "/freeze $payload"
    $env:GITHUB_OUTPUT = $commandOutput
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'handle-command.ps1')
    Assert-Equal $code 0 'PowerShell-looking freeze reason is accepted as data'
    if (Test-Path -LiteralPath $injectionMarker) {
        Add-Failure 'PowerShell-looking freeze reason remains inert'
    } else {
        Add-Pass 'PowerShell-looking freeze reason remains inert'
    }
    $operations = @(
        Get-Content -LiteralPath $issueOperations |
            ForEach-Object { $_ | ConvertFrom-Json }
    )
    $injectionBody = ($operations | Where-Object command -eq 'create' | Select-Object -First 1).body
    if ($injectionBody.Contains($payload, [StringComparison]::Ordinal)) {
        Add-Pass 'PowerShell-looking freeze reason is preserved literally'
    } else {
        Add-Failure 'PowerShell-looking freeze reason is preserved literally'
    }

    $commandOutput = Register-TestTemporaryFile
    $issueOperations = Register-TestTemporaryFile
    $env:MOCK_ISSUES = '[]'
    $env:GH_ISSUE_FILE = $issueOperations
    $env:BODY = '/freeze --branch main;Write-Output injected'
    $env:GITHUB_OUTPUT = $commandOutput
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'handle-command.ps1')
    Assert-Equal $code 0 'command-like branch name is handled as invalid input'
    Assert-Equal (
        Get-WorkflowOutput $commandOutput 'changed'
    ) $null 'command-like branch name does not request a refresh'
    $stateChanges = @(
        Get-Content -LiteralPath $issueOperations |
            ForEach-Object { $_ | ConvertFrom-Json } |
            Where-Object command -In @('create', 'edit', 'reopen', 'close')
    )
    Assert-Equal $stateChanges.Count 0 'command-like branch name cannot change tracking issue state'

    Write-Output '== handle-command.ps1 (best-effort notification) =='
    $commandOutput = Register-TestTemporaryFile
    $issueOperations = Register-TestTemporaryFile
    $env:GH_ISSUE_FILE = $issueOperations
    $env:MOCK_ISSUES = '[{"number":7,"title":"Branch freeze: main","url":"u","state":"OPEN","body":"## Current state\n\nBranch `main` is **frozen** by @rainersigwald.\n\n### Reason\n\nSDK insertion broke\n\n<!-- branch-freeze-by:rainersigwald -->"}]'
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
    $operations = @(Get-Content -LiteralPath $issueOperations | ForEach-Object { $_ | ConvertFrom-Json })
    Assert-Equal (@($operations | Where-Object command -eq 'edit').Count) 1 'unfreeze updates the issue body to the open state'
    Assert-Equal (@($operations | Where-Object command -eq 'close').Count) 1 'unfreeze closes the permanent issue'

    Write-Output '== refresh-pr-statuses.ps1 (bulk refresh) =='
    $statusFile = Initialize-TestStatusFile
    $env:MOCK_ISSUES = '[]'
    $env:MOCK_PRS = '[{"number":1,"headRefOid":"sha-1","baseRefName":"main"},{"number":2,"headRefOid":"sha-2","baseRefName":"main"}]'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'refresh-pr-statuses.ps1') @('main')
    Assert-Equal $code 0 'bulk refresh succeeds'
    $statusWrites = @(Get-Content -LiteralPath $statusFile | Where-Object { $_ -eq 'state=success' })
    Assert-Equal $statusWrites.Count 2 'bulk refresh stamps every matching PR'

    $statusFile = Initialize-TestStatusFile
    $env:MOCK_STATUS_FAILURE_SHA = 'sha-1'
    $code = Invoke-TestScript (Join-Path $branchFreezeDirectory 'refresh-pr-statuses.ps1') @('main')
    Assert-Equal $code 1 'bulk refresh fails after a PR status write fails'
    $statusWrites = @(Get-Content -LiteralPath $statusFile | Where-Object { $_ -eq 'state=success' })
    Assert-Equal $statusWrites.Count 1 'bulk refresh attempts later PRs after a status write fails'
    $env:MOCK_STATUS_FAILURE_SHA = ''
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
