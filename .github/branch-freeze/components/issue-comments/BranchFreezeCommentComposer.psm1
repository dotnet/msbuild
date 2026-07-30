function Get-BranchFreezeUsage {
    [OutputType([string])]
    param()

    return @'
**Usage**
- `/freeze [--branch <name>] <reason>` — freeze a branch (default `main`); a reason is required.
- `/unfreeze [--branch <name>]` — unfreeze a branch (default `main`).
'@
}

function New-BranchFreezeErrorReply {
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Message)

    return "$Message`n`n$(Get-BranchFreezeUsage)"
}

function New-BranchFreezeAuthorizationDenial {
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Actor)

    return "Sorry @$Actor — branch freeze/unfreeze is restricted to accounts listed in ``.github/branch-freeze-allowlist.txt``."
}

function New-BranchFreezeIssueBody {
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Actor,
        [Parameter(Mandatory)][string]$Reason
    )

    return @"
## Current state

Branch ``$Branch`` is **frozen** by @$Actor.

### Reason

$Reason

<!-- branch-freeze-by:$Actor -->
"@
}

function New-BranchOpenIssueBody {
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Actor
    )

    return "Branch ``$Branch`` is currently open.`n`nLast unfrozen by @$Actor."
}

function New-BranchFreezeAuditComment {
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string]$Actor,
        [Parameter(Mandatory)][string]$Reason
    )

    return "Frozen by @$Actor`: $Reason"
}

function New-BranchUnfreezeAuditComment {
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Actor)

    return "Unfrozen by @$Actor via `/unfreeze`."
}

function New-BranchFreezeConfirmation {
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Actor,
        [Parameter(Mandatory)][string]$IssueNumber,
        [Parameter(Mandatory)][string]$ChangeDescription,
        [Parameter(Mandatory)][string]$Reason
    )

    return @'
❄️ **`{0}` is now frozen** by @{1} — permanent tracking issue #{2} ({3}).

> {4}

Pull requests targeting `{0}` will be blocked by the `branch-freeze` check until someone runs `/unfreeze --branch {0}` (or `/unfreeze` for `main`).
'@ -f $Branch, $Actor, $IssueNumber, $ChangeDescription, $Reason
}

function New-BranchAlreadyOpenReply {
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Branch)

    return "``$Branch`` is not currently frozen — nothing to do."
}

function New-BranchUnfreezeConfirmation {
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Actor,
        [Parameter(Mandatory)][string]$IssueNumber
    )

    return (
        '✅ **`{0}` is now unfrozen** by @{1} — closed permanent tracking issue #{2}. The `branch-freeze` check now passes on open PRs targeting `{0}`.' -f
            $Branch, $Actor, $IssueNumber
    )
}

function Get-CodePointStartIndex {
    [OutputType([int])]
    param([Parameter(Mandatory)][string]$Text)

    $indexes = [System.Collections.Generic.List[int]]::new()
    for ($index = 0; $index -lt $Text.Length; $index++) {
        $indexes.Add($index)
        if (
            [char]::IsHighSurrogate($Text[$index]) -and
            $index + 1 -lt $Text.Length -and
            [char]::IsLowSurrogate($Text[$index + 1])
        ) {
            $index++
        }
    }

    return $indexes.ToArray()
}

function Get-BranchFreezeStatusDescription {
    [OutputType([string])]
    param([Parameter(Mandatory)]$Details)

    $description = if ([string]::IsNullOrEmpty($Details.Actor)) {
        "Frozen: $($Details.Reason)"
    }
    else {
        "Frozen by @$($Details.Actor): $($Details.Reason)"
    }

    # GitHub limits status descriptions to 140 characters. Reserve three for
    # the ellipsis and avoid splitting a UTF-16 surrogate pair such as an emoji.
    $codePointIndexes = @(Get-CodePointStartIndex $description)
    if ($codePointIndexes.Count -gt 140) {
        return $description.Substring(0, $codePointIndexes[137]) + '...'
    }

    return $description
}

Export-ModuleMember -Function @(
    'Get-BranchFreezeStatusDescription',
    'Get-BranchFreezeUsage',
    'New-BranchAlreadyOpenReply',
    'New-BranchFreezeAuthorizationDenial',
    'New-BranchFreezeAuditComment',
    'New-BranchFreezeConfirmation',
    'New-BranchFreezeErrorReply',
    'New-BranchFreezeIssueBody',
    'New-BranchOpenIssueBody',
    'New-BranchUnfreezeAuditComment',
    'New-BranchUnfreezeConfirmation'
)