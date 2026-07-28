Import-Module (Join-Path $PSScriptRoot 'GitHubCli.psm1') -Force

function Get-GitHubCommitStatus {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$Context
    )

    # Use the list endpoint rather than the combined `/status` one: only the list
    # reports `creator`, which is what proves a status came from this workflow.
    # Entries are newest first, so the first context match is the current status.
    $statusesJson = Invoke-GitHubCli -Arguments @(
        'api', "repos/$Repository/commits/$HeadSha/statuses?per_page=100"
    )
    return @($statusesJson | ConvertFrom-Json) |
        Where-Object {
            [StringComparer]::OrdinalIgnoreCase.Equals([string]$_.context, $Context)
        } |
        Select-Object -First 1
}

function Test-GitHubCommitStatusMatches {
    [OutputType([bool])]
    param(
        [AllowNull()]$Status,
        [Parameter(Mandatory)][string]$State,
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][AllowEmptyString()][string]$TargetUrl
    )

    if ($null -eq $Status) {
        return $false
    }

    # The required check is satisfied only by a status from the GitHub Actions
    # integration, so anything posted by someone else -- including a status whose
    # creator the API did not report -- counts as a mismatch and is replaced.
    $creatorLogin = if ($null -eq $Status.creator) { '' } else { [string]$Status.creator.login }
    if (-not [StringComparer]::OrdinalIgnoreCase.Equals($creatorLogin, 'github-actions[bot]')) {
        return $false
    }

    $currentTargetUrl = if ($null -eq $Status.target_url) { '' } else { [string]$Status.target_url }
    return (
        [StringComparer]::OrdinalIgnoreCase.Equals([string]$Status.state, $State) -and
        [StringComparer]::Ordinal.Equals([string]$Status.description, $Description) -and
        [StringComparer]::Ordinal.Equals($currentTargetUrl, $TargetUrl)
    )
}

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

    # Reading the current status only avoids burning the 1000-statuses-per-context
    # API budget on no-op writes, so it must never be able to prevent a write:
    # a redundant status is harmless, a missing one leaves a required check
    # unanswered and the pull request unmergeable.
    $currentStatus = $null
    try {
        $currentStatus = Get-GitHubCommitStatus -Repository $Repository `
            -HeadSha $HeadSha -Context $Context
    }
    catch {
        Write-Host "::warning::Could not read the current '$Context' status on $HeadSha; posting it anyway."
    }

    if (
        Test-GitHubCommitStatusMatches -Status $currentStatus -State $State `
            -Description $Description -TargetUrl $TargetUrl
    ) {
        Write-Host "Commit status '$Context' on $HeadSha is already up to date."
        return
    }

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