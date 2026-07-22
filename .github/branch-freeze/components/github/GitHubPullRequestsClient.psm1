Import-Module (Join-Path $PSScriptRoot 'GitHubCli.psm1') -Force

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

Export-ModuleMember -Function 'Get-GitHubOpenPullRequest'