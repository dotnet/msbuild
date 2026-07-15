Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$hookDirectory = Split-Path -Parent $PSScriptRoot
$handlerPath = Join-Path $hookDirectory 'prefix-github-comments.ps1'
$wrapperPath = Join-Path $hookDirectory 'gh-comment-wrapper.ps1'
$prefixPath = Join-Path $hookDirectory 'github-comment-prefix.txt'
$configPath = Join-Path (Split-Path -Parent $hookDirectory) 'copilot-cli-comment-prefix.json'
$pwshPath = (Get-Command pwsh -CommandType Application -ErrorAction Stop).Source

function Assert-True {
    param(
        [Parameter(Mandatory)][bool] $Condition,
        [Parameter(Mandatory)][string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Test-ObjectProperty {
    param(
        [Parameter(Mandatory)][object] $InputObject,
        [Parameter(Mandatory)][string] $Name
    )

    return $null -ne $InputObject.PSObject.Properties[$Name]
}

function Invoke-Hook {
    param([Parameter(Mandatory)][hashtable] $Payload)

    $inputJson = $Payload | ConvertTo-Json -Compress -Depth 100
    $outputJson = $inputJson | & $pwshPath -NoProfile -NonInteractive -File $handlerPath
    Assert-True ($LASTEXITCODE -eq 0) "Hook process failed for $($Payload.toolName)."
    return $outputJson | ConvertFrom-Json -Depth 100
}

function Get-CapturedBody {
    $bodyIndex = [Array]::IndexOf($script:CapturedGhArguments, '--body')
    Assert-True ($bodyIndex -ge 0) 'Wrapped gh call lost --body.'
    return [string]$script:CapturedGhArguments[$bodyIndex + 1]
}

$prefix = [System.IO.File]::ReadAllText($prefixPath).Replace("`r`n", "`n").TrimEnd([char[]]@("`r", "`n"))
Get-Content -Raw $configPath | ConvertFrom-Json -Depth 100 | Out-Null

$mcpResult = Invoke-Hook @{
    toolName = 'github-mcp-server-add_issue_comment'
    toolArgs = (@{
        owner = 'dotnet'
        repo = 'msbuild'
        issue_number = 1
        body = 'hello'
    } | ConvertTo-Json -Compress)
}
Assert-True ($mcpResult.modifiedArgs.body.Replace("`r`n", "`n").StartsWith($prefix)) 'Target MCP body was not prefixed.'

$otherMcpResult = Invoke-Hook @{
    toolName = 'github-mcp-server-add_issue_comment'
    toolArgs = (@{
        owner = 'JanProvaznik'
        repo = 'msbuild'
        issue_number = 1
        body = 'hello'
    } | ConvertTo-Json -Compress)
}
Assert-True (-not (Test-ObjectProperty -InputObject $otherMcpResult -Name 'modifiedArgs')) 'Another repository MCP body was modified.'

$idempotentResult = Invoke-Hook @{
    toolName = 'github-mcp-server-add_issue_comment'
    toolArgs = (@{
        owner = 'dotnet'
        repo = 'msbuild'
        issue_number = 1
        body = $mcpResult.modifiedArgs.body
    } | ConvertTo-Json -Compress)
}
Assert-True (-not (Test-ObjectProperty -InputObject $idempotentResult -Name 'modifiedArgs')) 'MCP prefix was duplicated.'

$reviewResult = Invoke-Hook @{
    toolName = 'github-mcp-server-create_pull_request_review'
    toolArgs = (@{
        owner = 'dotnet'
        repo = 'msbuild'
        pullNumber = 1
        body = 'summary'
        comments = @(@{
            path = 'file.cs'
            body = 'inline'
        })
    } | ConvertTo-Json -Compress -Depth 20)
}
Assert-True ($reviewResult.modifiedArgs.body.Replace("`r`n", "`n").StartsWith($prefix)) 'MCP review body was not prefixed.'
Assert-True ($reviewResult.modifiedArgs.comments[0].body.Replace("`r`n", "`n").StartsWith($prefix)) 'MCP inline review body was not prefixed.'

$shellResult = Invoke-Hook @{
    toolName = 'powershell'
    toolArgs = (@{
        command = 'gh issue comment 1 --repo dotnet/msbuild --body "hello"'
    } | ConvertTo-Json -Compress)
}
Assert-True ($shellResult.modifiedArgs.command.StartsWith(". '$wrapperPath';")) 'PowerShell gh comment command was not wrapped.'

$readOnlyResult = Invoke-Hook @{
    toolName = 'powershell'
    toolArgs = (@{
        command = 'gh issue view 1 --repo dotnet/msbuild'
    } | ConvertTo-Json -Compress)
}
Assert-True (-not (Test-ObjectProperty -InputObject $readOnlyResult -Name 'modifiedArgs')) 'Read-only gh command was modified.'

$invalidOutput = '{not-json' | & $pwshPath -NoProfile -NonInteractive -File $handlerPath
Assert-True ($LASTEXITCODE -eq 0 -and $invalidOutput.Trim() -eq '{}') 'Malformed hook input did not fail open.'

$emptyPath = Join-Path ([System.IO.Path]::GetTempPath()) "copilot-msbuild-no-gh-$([guid]::NewGuid().ToString('N'))"
[System.IO.Directory]::CreateDirectory($emptyPath) | Out-Null
try {
    $escapedWrapperPath = $wrapperPath.Replace("'", "''")
    $escapedEmptyPath = $emptyPath.Replace("'", "''")
    $noGhOutput = & $pwshPath -NoProfile -NonInteractive -Command "`$env:PATH = '$escapedEmptyPath'; . '$escapedWrapperPath'; Write-Output 'continued'"
    Assert-True ($LASTEXITCODE -eq 0 -and $noGhOutput.Trim() -eq 'continued') 'Missing gh.exe prevented the original PowerShell command from continuing.'
}
finally {
    Remove-Item -LiteralPath $emptyPath -Recurse -Force -ErrorAction SilentlyContinue
}

Assert-True ($null -ne (Get-Command gh.exe -CommandType Application -ErrorAction SilentlyContinue)) 'GitHub CLI is required for wrapper tests.'
. $wrapperPath
. $wrapperPath

$script:CapturedGhArguments = @()
$script:CapturedBodyFileContent = $null
function Invoke-FakeGh {
    $script:CapturedGhArguments = @($args)
    $bodyFileIndex = [Array]::IndexOf($script:CapturedGhArguments, '--body-file')
    if ($bodyFileIndex -ge 0) {
        $script:CapturedBodyFileContent = [System.IO.File]::ReadAllText([string]$script:CapturedGhArguments[$bodyFileIndex + 1])
    }
    $global:LASTEXITCODE = 0
}
$script:CopilotRealGhPath = 'Invoke-FakeGh'

gh pr comment 1 --repo dotnet/msbuild --body 'hello'
$targetBody = Get-CapturedBody
Assert-True ($targetBody.Replace("`r`n", "`n").StartsWith($prefix)) 'Target gh body was not prefixed.'
Assert-True (([regex]::Matches($targetBody, [regex]::Escape($prefix))).Count -eq 1) 'Sourcing the hook twice duplicated the prefix.'

gh pr comment 1 --repo JanProvaznik/msbuild --body 'hello'
Assert-True ((Get-CapturedBody) -ceq 'hello') 'Another repository gh body was modified.'

gh pr comment https://github.com/dotnet/msbuild/pull/1 --body 'hello'
Assert-True ((Get-CapturedBody).Replace("`r`n", "`n").StartsWith($prefix)) 'Target URL gh body was not prefixed.'

gh --repo dotnet/msbuild issue comment 1 --body 'hello'
Assert-True ((Get-CapturedBody).Replace("`r`n", "`n").StartsWith($prefix)) 'Leading --repo gh body was not prefixed.'

gh api --method POST repos/dotnet/msbuild/issues/1/comments -f 'body=hello'
$fieldIndex = [Array]::IndexOf($script:CapturedGhArguments, '-f')
Assert-True ($fieldIndex -ge 0) 'Wrapped gh API call lost its body field.'
Assert-True (([string]$script:CapturedGhArguments[$fieldIndex + 1]).Replace("`r`n", "`n").StartsWith("body=$prefix")) 'Target gh API body was not prefixed.'

gh api --method POST repos/JanProvaznik/msbuild/issues/1/comments -f 'body=hello'
$fieldIndex = [Array]::IndexOf($script:CapturedGhArguments, '-f')
Assert-True (([string]$script:CapturedGhArguments[$fieldIndex + 1]) -ceq 'body=hello') 'Another repository gh API body was modified.'

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "copilot-msbuild-comment-prefix-tests-$([guid]::NewGuid().ToString('N'))"
[System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null

try {
    $bodyFile = Join-Path $temporaryRoot 'body.md'
    [System.IO.File]::WriteAllText($bodyFile, 'file body')
    gh issue comment 1 --body-file $bodyFile
    Assert-True ($script:CapturedBodyFileContent.Replace("`r`n", "`n").StartsWith($prefix)) 'Target gh body file was not prefixed.'
    Assert-True ([System.IO.File]::ReadAllText($bodyFile) -ceq 'file body') 'Original body file was modified.'
    $temporaryBodyPath = [string]$script:CapturedGhArguments[[Array]::IndexOf($script:CapturedGhArguments, '--body-file') + 1]
    Assert-True (-not [System.IO.File]::Exists($temporaryBodyPath)) 'Temporary prefixed body file was not removed.'

    $fieldBody = Join-Path $temporaryRoot 'field-body.md'
    $invalidJson = Join-Path $temporaryRoot 'invalid.json'
    [System.IO.File]::WriteAllText($fieldBody, 'field body')
    [System.IO.File]::WriteAllText($invalidJson, '{not-json')
    $originalTemp = $env:TEMP
    $originalTmp = $env:TMP
    try {
        $env:TEMP = $temporaryRoot
        $env:TMP = $temporaryRoot
        gh api repos/dotnet/msbuild/issues/1/comments -F "body=@$fieldBody" --input $invalidJson
    }
    finally {
        $env:TEMP = $originalTemp
        $env:TMP = $originalTmp
    }
    $leakedTemporaryFiles = @(Get-ChildItem -LiteralPath $temporaryRoot -File | Where-Object {
        $_.FullName -notin @($bodyFile, $fieldBody, $invalidJson)
    })
    Assert-True ($leakedTemporaryFiles.Count -eq 0) 'Partial argument conversion leaked a temporary file.'

    $originalPrefixPath = $script:CopilotCommentPrefixPath
    $script:CopilotCommentPrefixPath = Join-Path $temporaryRoot 'missing-prefix.txt'
    gh pr comment 1 --body hello
    $script:CopilotCommentPrefixPath = $originalPrefixPath
    Assert-True (($script:CapturedGhArguments -join '|') -ceq 'pr|comment|1|--body|hello') 'Wrapper conversion failure did not pass through original arguments.'
}
finally {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'All PowerShell hook tests passed.'
