Import-Module (Join-Path $PSScriptRoot 'BranchFreezeCommentComposer.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'BranchFreezeCommentParser.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GitHubIssuesClient.psm1') -Force

$script:TrackingLabel = 'branch-freeze'

function Get-BranchFreezeIssueTitle {
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Branch)

    return "Branch freeze: $Branch"
}

function Get-BranchFreezeIssue {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch
    )

    $title = Get-BranchFreezeIssueTitle -Branch $Branch
    $matches = @(
        @(Get-GitHubIssue -Repository $Repository -Label $script:TrackingLabel) |
            Where-Object { $_.title -ceq $title }
    )

    if ($matches.Count -gt 1) {
        throw "Multiple branch-freeze tracking issues have the exact title '$title'."
    }

    return $matches | Select-Object -First 1
}

function Test-BranchFreezeIssueOpen {
    [OutputType([bool])]
    param([AllowNull()]$Issue)

    return (
        $null -ne $Issue -and
        [StringComparer]::OrdinalIgnoreCase.Equals([string]$Issue.state, 'OPEN')
    )
}

function Get-BranchFreezeState {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch
    )

    $issue = Get-BranchFreezeIssue -Repository $Repository -Branch $Branch
    $isFrozen = Test-BranchFreezeIssueOpen -Issue $issue
    $actor = ''
    $reason = ''
    $url = ''
    if ($isFrozen) {
        $details = ConvertFrom-BranchFreezeIssueBody -Body ([string]$issue.body)
        $actor = $details.Actor
        $reason = $details.Reason
        $url = $issue.url
    }

    return [pscustomobject]@{
        IsFrozen = $isFrozen
        Actor = $actor
        Reason = $reason
        Url = $url
    }
}

function Add-BranchFreezeHistoryComment {
    [OutputType([void])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$IssueNumber,
        [Parameter(Mandatory)][string]$Comment
    )

    try {
        Add-GitHubIssueComment -Repository $Repository -IssueNumber $IssueNumber `
            -Body $Comment
    }
    catch {
        Write-Warning "Failed to add history comment to issue #$IssueNumber."
    }
}

function Open-BranchFreezeIssue {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Actor,
        [Parameter(Mandatory)][string]$Reason
    )

    Add-GitHubLabel -Repository $Repository -Name $script:TrackingLabel -Color 'B60205' `
        -Description 'Tracks a frozen branch'

    $title = Get-BranchFreezeIssueTitle -Branch $Branch
    $body = New-BranchFreezeIssueBody -Branch $Branch -Actor $Actor -Reason $Reason
    $issue = Get-BranchFreezeIssue -Repository $Repository -Branch $Branch
    $wasCreated = $null -eq $issue

    if ($wasCreated) {
        $url = New-GitHubIssue -Repository $Repository -Label $script:TrackingLabel `
            -Title $title -Body $body
        $number = ($url.TrimEnd('/') -split '/')[-1]
        $wasReopened = $false
    }
    else {
        Set-GitHubIssue -Repository $Repository -IssueNumber ([string]$issue.number) `
            -Body $body -AddLabel $script:TrackingLabel

        $number = $issue.number
        $url = $issue.url
        $wasReopened = -not (Test-BranchFreezeIssueOpen -Issue $issue)
        if ($wasReopened) {
            Open-GitHubIssue -Repository $Repository -IssueNumber ([string]$number)
        }
    }

    $auditComment = New-BranchFreezeAuditComment -Actor $Actor -Reason $Reason
    Add-BranchFreezeHistoryComment -Repository $Repository `
        -IssueNumber ([string]$number) -Comment $auditComment

    return [pscustomobject]@{
        Number = $number
        Url = $url
        WasCreated = $wasCreated
        WasReopened = $wasReopened
    }
}

function Close-BranchFreezeIssue {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Actor
    )

    $issue = Get-BranchFreezeIssue -Repository $Repository -Branch $Branch
    $changed = Test-BranchFreezeIssueOpen -Issue $issue
    $number = if ($null -eq $issue) { $null } else { $issue.number }
    if ($changed) {
        $body = New-BranchOpenIssueBody -Branch $Branch -Actor $Actor
        Set-GitHubIssue -Repository $Repository -IssueNumber ([string]$number) `
            -Body $body
        Close-GitHubIssue -Repository $Repository -IssueNumber ([string]$number)

        $auditComment = New-BranchUnfreezeAuditComment -Actor $Actor
        Add-BranchFreezeHistoryComment -Repository $Repository `
            -IssueNumber ([string]$number) -Comment $auditComment
    }

    return [pscustomobject]@{
        Changed = $changed
        Number = $number
    }
}

Export-ModuleMember -Function @(
    'Close-BranchFreezeIssue',
    'Get-BranchFreezeState',
    'Open-BranchFreezeIssue'
)
