function ConvertFrom-BranchFreezeCommand {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param([AllowEmptyString()][string]$Body)

    $line = ([regex]::Split($Body, '\r?\n', 2)[0]).TrimEnd("`r")
    if ($line -notmatch '^(\S+)(?:\s+(.*))?$') {
        return $null
    }

    $command = $Matches[1]
    $remaining = if ($Matches.Count -gt 2) { $Matches[2] } else { '' }
    $action = switch -CaseSensitive ($command) {
        '/freeze' { 'freeze' }
        '/unfreeze' { 'unfreeze' }
        default { return $null }
    }

    if ($remaining -match '^(--branch|-b)$') {
        throw "Missing a branch name after ``$remaining``."
    }
    if ($remaining -match '^(?:--branch|-b)\s+$') {
        throw 'No branch name was given after the `--branch` flag.'
    }

    $branch = 'main'
    if ($remaining -match '^(?:--branch|-b)\s+(\S+)(?:\s+(.*))?$') {
        $branch = $Matches[1]
        $remaining = if ($Matches.Count -gt 2) { $Matches[2] } else { '' }
    }

    return [pscustomobject]@{
        Action = $action
        Branch = $branch
        Reason = $remaining.TrimEnd()
    }
}

function ConvertFrom-BranchFreezeIssueBody {
    [OutputType([System.Management.Automation.PSCustomObject])]
    param([AllowEmptyString()][string]$Body)

    $actorMatches = [regex]::Matches(
        $Body,
        '(?m)^\s*<!--\s*branch-freeze-by:\s*([^\s>]+)\s*-->\s*$'
    )
    $actor = if ($actorMatches.Count -gt 0) {
        $actorMatches[$actorMatches.Count - 1].Groups[1].Value
    }
    else {
        ''
    }

    $reasonMatch = [regex]::Match(
        $Body,
        '(?ms)^### Reason\s*\r?\n+(.*)\r?\n+\s*<!--\s*branch-freeze-by:[^\r\n]*-->\s*$'
    )
    $reason = if ($reasonMatch.Success) {
        (($reasonMatch.Groups[1].Value) -replace '\s+', ' ').Trim()
    }
    else {
        $reasonLines = @(
            [regex]::Split($Body, '\r?\n') |
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

Export-ModuleMember -Function @(
    'ConvertFrom-BranchFreezeCommand',
    'ConvertFrom-BranchFreezeIssueBody'
)