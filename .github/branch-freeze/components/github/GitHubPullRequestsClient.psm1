Import-Module (Join-Path $PSScriptRoot 'GitHubCli.psm1') -Force

function Get-GitHubPullRequest {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Number
    )

    $pullRequestJson = Invoke-GitHubCli -Arguments @(
        'pr', 'view', $Number,
        '--repo', $Repository,
        '--json', 'number,headRefOid,baseRefName'
    )
    return $pullRequestJson | ConvertFrom-Json
}

function Get-GitHubOpenPullRequest {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [AllowEmptyString()][string]$BaseRef = ''
    )

    $arguments = @(
        'pr', 'list',
        '--repo', $Repository,
        '--state', 'open',
        '--limit', '1000',
        '--json', 'number,headRefOid,baseRefName'
    )
    if (-not [string]::IsNullOrEmpty($BaseRef)) {
        $arguments += @('--base', $BaseRef)
    }

    return @(Invoke-GitHubCli -Arguments $arguments | ConvertFrom-Json)
}

Export-ModuleMember -Function @(
    'Get-GitHubOpenPullRequest',
    'Get-GitHubPullRequest'
)