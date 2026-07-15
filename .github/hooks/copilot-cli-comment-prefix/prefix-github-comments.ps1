Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

trap {
    [Console]::Out.WriteLine('{}')
    exit 0
}

function Write-HookOutput {
    param([Parameter(Mandatory)][object] $Value)

    $Value | ConvertTo-Json -Compress -Depth 100
    exit 0
}

function Get-CommentPrefix {
    $prefixPath = Join-Path $PSScriptRoot 'github-comment-prefix.txt'
    $prefix = [System.IO.File]::ReadAllText($prefixPath).Replace("`r`n", "`n").TrimEnd([char[]]@("`r", "`n"))

    if ([string]::IsNullOrWhiteSpace($prefix)) {
        throw "The GitHub comment prefix file is empty: $prefixPath"
    }

    return $prefix
}

function Add-CommentPrefix {
    param([AllowEmptyString()][string] $Body)

    $prefix = Get-CommentPrefix
    $normalizedBody = $Body.Replace("`r`n", "`n")
    if ($normalizedBody.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
        return $Body
    }

    return "$prefix`n`n$Body"
}

function Get-NormalizedRepository {
    param(
        [AllowNull()][object] $Owner,
        [AllowNull()][object] $Repository
    )

    if ($Repository -isnot [string]) {
        return $null
    }

    $repositoryValue = $Repository.Trim().Trim('/')
    if ($repositoryValue.Contains('/')) {
        return ($repositoryValue -replace '(?i)\.git$', '').ToLowerInvariant()
    }

    if ($Owner -isnot [string] -or [string]::IsNullOrWhiteSpace($Owner)) {
        return $null
    }

    return "$($Owner.Trim())/$repositoryValue".ToLowerInvariant()
}

function Test-TargetsMsbuild {
    param([Parameter(Mandatory)][object] $ToolArgs)

    $ownerProperty = $ToolArgs.PSObject.Properties['owner']
    $repoProperty = $ToolArgs.PSObject.Properties['repo']
    $repositoryProperty = $ToolArgs.PSObject.Properties['repository']

    $owner = if ($null -ne $ownerProperty) { $ownerProperty.Value } else { $null }
    $repository = if ($null -ne $repoProperty) {
        $repoProperty.Value
    }
    elseif ($null -ne $repositoryProperty) {
        $repositoryProperty.Value
    }
    else {
        $null
    }

    return (Get-NormalizedRepository -Owner $owner -Repository $repository) -ceq 'dotnet/msbuild'
}

function Test-GhCommentWriteCommand {
    param([Parameter(Mandatory)][string] $Command)

    $ghCommandPrefix = '(?<![\w.-])gh(?:\.exe)?(?:\s+(?:(?:-R|--repo|--hostname)(?:=|\s+)\S+))*'

    if ($Command -match "(?is)$ghCommandPrefix\s+(?:pr|issue)\s+comment\b") {
        return $true
    }

    if (
        $Command -match "(?is)$ghCommandPrefix\s+pr\s+review\b" -and
        $Command -match '(?is)(?:--comment|--request-changes|--approve|--body(?:-file)?\b|-b\b|-F\b)'
    ) {
        return $true
    }

    if (
        $Command -match "(?is)$ghCommandPrefix\s+api\b" -and
        $Command -match '(?is)(?:/issues/\d+/comments|/issues/comments/\d+|/pulls/\d+/(?:comments|reviews)|/pulls/comments/\d+|/comments/\d+/replies|/reviews/\d+/comments)' -and
        $Command -match '(?is)(?:(?:-X|--method)\s*(?:POST|PUT|PATCH)\b|(?:-f|--raw-field|-F|--field|--input)\b)'
    ) {
        return $true
    }

    return $false
}

$inputJson = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($inputJson)) {
    throw 'The preToolUse hook received no input.'
}

$hookInput = $inputJson | ConvertFrom-Json -Depth 100
$toolName = [string]$hookInput.toolName
$toolArgs = $hookInput.toolArgs

if ($null -eq $toolArgs) {
    Write-HookOutput @{}
}

if ($toolArgs -is [string]) {
    $toolArgs = $toolArgs | ConvertFrom-Json -Depth 100
}

$mcpCommentToolPattern = '(?:^|[-_])(?:add_issue_comment|add_reply_to_pull_request_comment|add_comment_to_pending_review|add_pull_request_review_comment|create_pull_request_review|submit_pending_pull_request_review|pull_request_review_write|discussion_comment_write)$'

if ($toolName -match $mcpCommentToolPattern) {
    if (-not (Test-TargetsMsbuild -ToolArgs $toolArgs)) {
        Write-HookOutput @{}
    }

    $changed = $false
    $bodyProperty = $toolArgs.PSObject.Properties['body']
    if ($null -ne $bodyProperty -and $bodyProperty.Value -is [string]) {
        $prefixedBody = Add-CommentPrefix -Body $bodyProperty.Value
        if ($prefixedBody -cne $bodyProperty.Value) {
            $bodyProperty.Value = $prefixedBody
            $changed = $true
        }
    }

    $commentsProperty = $toolArgs.PSObject.Properties['comments']
    if ($null -ne $commentsProperty -and $null -ne $commentsProperty.Value) {
        foreach ($comment in @($commentsProperty.Value)) {
            $commentBodyProperty = $comment.PSObject.Properties['body']
            if ($null -eq $commentBodyProperty -or $commentBodyProperty.Value -isnot [string]) {
                continue
            }

            $prefixedCommentBody = Add-CommentPrefix -Body $commentBodyProperty.Value
            if ($prefixedCommentBody -cne $commentBodyProperty.Value) {
                $commentBodyProperty.Value = $prefixedCommentBody
                $changed = $true
            }
        }
    }

    if ($changed) {
        Write-HookOutput @{ modifiedArgs = $toolArgs }
    }

    Write-HookOutput @{}
}

if ($toolName -notin @('powershell', 'bash')) {
    Write-HookOutput @{}
}

$commandProperty = $toolArgs.PSObject.Properties['command']
if ($null -eq $commandProperty -or $commandProperty.Value -isnot [string]) {
    Write-HookOutput @{}
}

$command = [string]$commandProperty.Value
if (-not (Test-GhCommentWriteCommand -Command $command)) {
    Write-HookOutput @{}
}

if ($toolName -eq 'bash') {
    Write-HookOutput @{}
}

$ghCommandPrefix = '(?<![\w.-])gh(?:\.exe)?(?:\s+(?:(?:-R|--repo|--hostname)(?:=|\s+)\S+))*'
if (
    $command -match "(?is)$ghCommandPrefix\s+api\s+graphql\b" -and
    $command -match '(?is)\b(?:addComment|addPullRequestReview|submitPullRequestReview|addPullRequestReviewComment)\b'
) {
    Write-HookOutput @{}
}

$wrapperPath = Join-Path $PSScriptRoot 'gh-comment-wrapper.ps1'
if (-not (Test-Path -LiteralPath $wrapperPath -PathType Leaf)) {
    throw "The GitHub CLI comment wrapper is missing: $wrapperPath"
}

$escapedWrapperPath = $wrapperPath.Replace("'", "''")
$rewrittenCommand = [regex]::Replace($command, '(?i)(?<![\w.\\/])gh\.exe(?=\s)', 'gh')
$commandProperty.Value = ". '$escapedWrapperPath'; $rewrittenCommand"

Write-HookOutput @{ modifiedArgs = $toolArgs }
