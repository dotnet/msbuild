Import-Module (Join-Path $PSScriptRoot 'GitHubCli.psm1') -Force

function Set-GitHubCommitStatus {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$State,
        [Parameter(Mandatory)][string]$Context,
        [Parameter(Mandatory)][string]$Description,
        [string]$TargetUrl = ''
    )

    $arguments = @(
        'api', '-X', 'POST', "repos/$Repository/statuses/$HeadSha",
        '-f', "state=$State",
        '-f', "context=$Context",
        '-f', "description=$Description"
    )
    if (-not [string]::IsNullOrEmpty($TargetUrl)) {
        $arguments += @('-f', "target_url=$TargetUrl")
    }

    Invoke-GitHubCli -Arguments $arguments -DiscardOutput -NoRetry
}

Export-ModuleMember -Function 'Set-GitHubCommitStatus'