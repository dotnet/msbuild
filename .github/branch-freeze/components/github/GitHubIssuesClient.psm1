Import-Module (Join-Path $PSScriptRoot 'GitHubCli.psm1') -Force

function Get-GitHubIssue {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Label
    )

    $issuesJson = Invoke-GitHubCli -Arguments @(
        'issue', 'list',
        '--repo', $Repository,
        '--label', $Label,
        '--state', 'all',
        '--limit', '1000',
        '--json', 'number,title,body,url,state'
    )

    return @($issuesJson | ConvertFrom-Json)
}

function Add-GitHubLabel {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Color,
        [Parameter(Mandatory)][string]$Description
    )

    Invoke-GitHubCli -Arguments @(
        'label', 'create', $Name,
        '--repo', $Repository,
        '--color', $Color,
        '--description', $Description
    ) -DiscardOutput -IgnoreExitCode
}

function New-GitHubIssue {
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Title,
        [Parameter(Mandatory)][string]$Body
    )

    return Invoke-GitHubCli -Arguments @(
        'issue', 'create',
        '--repo', $Repository,
        '--label', $Label,
        '--title', $Title,
        '--body', $Body
    ) -NoRetry
}

function Set-GitHubIssue {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$IssueNumber,
        [Parameter(Mandatory)][string]$Body,
        [string]$AddLabel = ''
    )

    $arguments = @(
        'issue', 'edit', $IssueNumber,
        '--repo', $Repository,
        '--body', $Body
    )
    if (-not [string]::IsNullOrEmpty($AddLabel)) {
        $arguments += @('--add-label', $AddLabel)
    }

    Invoke-GitHubCli -Arguments $arguments -DiscardOutput
}

function Open-GitHubIssue {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$IssueNumber
    )

    Invoke-GitHubCli -Arguments @(
        'issue', 'reopen', $IssueNumber,
        '--repo', $Repository
    ) -DiscardOutput
}

function Close-GitHubIssue {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$IssueNumber
    )

    Invoke-GitHubCli -Arguments @(
        'issue', 'close', $IssueNumber,
        '--repo', $Repository
    ) -DiscardOutput
}

function Add-GitHubIssueComment {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$IssueNumber,
        [Parameter(Mandatory)][string]$Body
    )

    Invoke-GitHubCli -Arguments @(
        'issue', 'comment', $IssueNumber,
        '--repo', $Repository,
        '--body', $Body
    ) -DiscardOutput -NoRetry
}

function Add-GitHubCommentReaction {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$CommentId,
        [Parameter(Mandatory)][string]$Content
    )

    Invoke-GitHubCli -Arguments @(
        'api', '-X', 'POST', "repos/$Repository/issues/comments/$CommentId/reactions",
        '-f', "content=$Content"
    ) -DiscardOutput -NoRetry
}

Export-ModuleMember -Function @(
    'Add-GitHubCommentReaction',
    'Add-GitHubIssueComment',
    'Add-GitHubLabel',
    'Close-GitHubIssue',
    'Get-GitHubIssue',
    'New-GitHubIssue',
    'Open-GitHubIssue',
    'Set-GitHubIssue'
)