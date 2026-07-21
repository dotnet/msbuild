$script:TrackingLabel = 'branch-freeze'
$script:StatusContext = 'branch-freeze'

function Invoke-GitHubCli {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$DiscardOutput
    )

    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "gh command failed with exit code $LASTEXITCODE."
    }

    if (-not $DiscardOutput) {
        return $output -join [Environment]::NewLine
    }
}

function Get-RepositoryName {
    if (-not [string]::IsNullOrEmpty($env:REPO)) {
        return $env:REPO
    }

    if (-not [string]::IsNullOrEmpty($env:GITHUB_REPOSITORY)) {
        return $env:GITHUB_REPOSITORY
    }

    throw 'REPO or GITHUB_REPOSITORY must be set.'
}

function Get-RequiredEnvironmentValue {
    param([Parameter(Mandatory)][string]$Name)

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrEmpty($value)) {
        throw "Environment variable '$Name' is required."
    }

    return $value
}

function Get-BranchFreezeIssueTitle {
    param([Parameter(Mandatory)][string]$Branch)

    return "Branch freeze: $Branch"
}

function Get-BranchFreezeIssue {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch
    )

    $title = Get-BranchFreezeIssueTitle -Branch $Branch
    $issuesJson = Invoke-GitHubCli -Arguments @(
        'issue', 'list',
        '--repo', $Repository,
        '--label', $script:TrackingLabel,
        '--state', 'all',
        '--limit', '1000',
        '--json', 'number,title,body,url,state'
    )
    $matches = @(
        @($issuesJson | ConvertFrom-Json) |
            Where-Object { $_.title -ceq $title }
    )

    if ($matches.Count -gt 1) {
        throw "Multiple branch-freeze tracking issues have the exact title '$title'."
    }

    return $matches | Select-Object -First 1
}

function Get-BranchFreezeDetails {
    param([Parameter(Mandatory)]$Issue)

    $body = [string]$Issue.body
    $actorMatches = [regex]::Matches(
        $body,
        '(?m)^\s*<!--\s*branch-freeze-by:\s*([^\s>]+)\s*-->\s*$'
    )
    $actor = if ($actorMatches.Count -gt 0) {
        $actorMatches[$actorMatches.Count - 1].Groups[1].Value
    }
    else {
        ''
    }

    $reasonMatch = [regex]::Match(
        $body,
        '(?ms)^### Reason\s*\r?\n+(.*)\r?\n+\s*<!--\s*branch-freeze-by:[^\r\n]*-->\s*$'
    )
    $reason = if ($reasonMatch.Success) {
        (($reasonMatch.Groups[1].Value) -replace '\s+', ' ').Trim()
    }
    else {
        $reasonLines = @(
            [regex]::Split($body, '\r?\n') |
                Where-Object {
                    -not [regex]::IsMatch($_, '^\s*<!--\s*branch-freeze-by:.*-->\s*$')
                }
        )
        (($reasonLines -join ' ') -replace '\s+', ' ').Trim()
    }
    if ([string]::IsNullOrEmpty($reason)) {
        $reason = '(no reason provided)'
    }

    return [pscustomobject]@{
        Actor = $actor
        Reason = $reason
    }
}

function Test-BranchFreezeIssueOpen {
    param([AllowNull()]$Issue)

    return (
        $null -ne $Issue -and
        [StringComparer]::OrdinalIgnoreCase.Equals([string]$Issue.state, 'OPEN')
    )
}

function Add-BranchFreezeAuditComment {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$IssueNumber,
        [Parameter(Mandatory)][string]$Actor,
        [Parameter(Mandatory)][string]$Reason
    )

    Invoke-GitHubCli -Arguments @(
        'issue', 'comment', $IssueNumber,
        '--repo', $Repository,
        '--body', ("Frozen by @{0}: {1}" -f $Actor, $Reason)
    ) -DiscardOutput
}

function Open-BranchFreezeIssue {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Actor,
        [Parameter(Mandatory)][string]$Reason
    )

    & gh label create $script:TrackingLabel --repo $Repository --color B60205 `
        --description 'Tracks a frozen branch' *> $null

    $title = Get-BranchFreezeIssueTitle -Branch $Branch
    $body = @"
## Current state

Branch ``$Branch`` is **frozen** by @$Actor.

### Reason

$Reason

<!-- branch-freeze-by:$Actor -->
"@
    $issue = Get-BranchFreezeIssue -Repository $Repository -Branch $Branch

    if ($null -eq $issue) {
        $url = Invoke-GitHubCli -Arguments @(
            'issue', 'create',
            '--repo', $Repository,
            '--label', $script:TrackingLabel,
            '--title', $title,
            '--body', $body
        )
        $number = ($url.TrimEnd('/') -split '/')[-1]
        Add-BranchFreezeAuditComment -Repository $Repository -IssueNumber $number `
            -Actor $Actor -Reason $Reason

        return [pscustomobject]@{
            Number = $number
            Url = $url
            WasCreated = $true
            WasReopened = $false
        }
    }

    Invoke-GitHubCli -Arguments @(
        'issue', 'edit', [string]$issue.number,
        '--repo', $Repository,
        '--add-label', $script:TrackingLabel,
        '--body', $body
    ) -DiscardOutput

    $wasReopened = -not (Test-BranchFreezeIssueOpen -Issue $issue)
    if ($wasReopened) {
        Invoke-GitHubCli -Arguments @(
            'issue', 'reopen', [string]$issue.number,
            '--repo', $Repository
        ) -DiscardOutput
    }

    Add-BranchFreezeAuditComment -Repository $Repository `
        -IssueNumber ([string]$issue.number) -Actor $Actor -Reason $Reason

    return [pscustomobject]@{
        Number = $issue.number
        Url = $issue.url
        WasCreated = $false
        WasReopened = $wasReopened
    }
}

function Close-BranchFreezeIssue {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Actor
    )

    $issue = Get-BranchFreezeIssue -Repository $Repository -Branch $Branch
    if (-not (Test-BranchFreezeIssueOpen -Issue $issue)) {
        return [pscustomobject]@{
            Changed = $false
            Number = if ($null -eq $issue) { $null } else { $issue.number }
        }
    }

    Invoke-GitHubCli -Arguments @(
        'issue', 'edit', [string]$issue.number,
        '--repo', $Repository,
        '--body', "Branch ``$Branch`` is currently open.`n`nLast unfrozen by @$Actor."
    ) -DiscardOutput
    Invoke-GitHubCli -Arguments @(
        'issue', 'close', [string]$issue.number,
        '--repo', $Repository,
        '--comment', ("Unfrozen by @{0} via `/unfreeze`." -f $Actor)
    ) -DiscardOutput

    return [pscustomobject]@{
        Changed = $true
        Number = $issue.number
    }
}

function Set-BranchFreezeCommitStatus {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$State,
        [Parameter(Mandatory)][string]$Description,
        [string]$TargetUrl = ''
    )

    $arguments = @(
        'api', '-X', 'POST', "repos/$Repository/statuses/$HeadSha",
        '-f', "state=$State",
        '-f', "context=$script:StatusContext",
        '-f', "description=$Description"
    )
    if (-not [string]::IsNullOrEmpty($TargetUrl)) {
        $arguments += @('-f', "target_url=$TargetUrl")
    }

    Invoke-GitHubCli -Arguments $arguments -DiscardOutput
}

Export-ModuleMember -Function @(
    'Close-BranchFreezeIssue',
    'Get-BranchFreezeDetails',
    'Get-BranchFreezeIssue',
    'Get-RepositoryName',
    'Get-RequiredEnvironmentValue',
    'Invoke-GitHubCli',
    'Open-BranchFreezeIssue',
    'Set-BranchFreezeCommitStatus',
    'Test-BranchFreezeIssueOpen'
)
