Import-Module (Join-Path $PSScriptRoot 'GitHubCli.psm1') -Force

function Get-GitHubRepositoryName {
    [OutputType([string])]
    param()

    if (-not [string]::IsNullOrEmpty($env:REPO)) {
        return $env:REPO
    }

    if (-not [string]::IsNullOrEmpty($env:GITHUB_REPOSITORY)) {
        return $env:GITHUB_REPOSITORY
    }

    throw 'REPO or GITHUB_REPOSITORY must be set.'
}

function Test-GitHubBranchExists {
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch
    )

    if ($Branch -notmatch '^[A-Za-z0-9._/-]+$') {
        return $false
    }

    $encodedBranch = [uri]::EscapeDataString($Branch)
    $exitCode = Invoke-GitHubCli -Arguments @(
        'api', "repos/$Repository/branches/$encodedBranch"
    ) -DiscardOutput -IgnoreExitCode -PassThruExitCode

    return $exitCode -eq 0
}

Export-ModuleMember -Function @(
    'Get-GitHubRepositoryName',
    'Test-GitHubBranchExists'
)