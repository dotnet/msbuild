$script:CopilotCommentPrefixPath = Join-Path $PSScriptRoot 'github-comment-prefix.txt'
$copilotGhCommand = Get-Command gh.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
$script:CopilotRealGhPath = if ($null -ne $copilotGhCommand) { $copilotGhCommand.Source } else { $null }
$script:CopilotTargetRepository = 'dotnet/msbuild'

function Get-CopilotCommentPrefix {
    $prefix = [System.IO.File]::ReadAllText($script:CopilotCommentPrefixPath).Replace("`r`n", "`n").TrimEnd([char[]]@("`r", "`n"))
    if ([string]::IsNullOrWhiteSpace($prefix)) {
        throw "The GitHub comment prefix file is empty: $script:CopilotCommentPrefixPath"
    }

    return $prefix
}

function Add-CopilotCommentPrefix {
    param([AllowEmptyString()][string] $Body)

    $prefix = Get-CopilotCommentPrefix
    $normalizedBody = $Body.Replace("`r`n", "`n")
    if ($normalizedBody.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
        return $Body
    }

    return "$prefix`n`n$Body"
}

function ConvertTo-CopilotRepositorySlug {
    param([Parameter(Mandatory)][string] $Value)

    $candidate = $Value.Trim()
    foreach ($prefix in @(
        'https://github.com/',
        'http://github.com/',
        'git@github.com:',
        'github.com/',
        'https://api.github.com/',
        'http://api.github.com/'
    )) {
        if ($candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $candidate = $candidate.Substring($prefix.Length)
            break
        }
    }

    $candidate = $candidate.Trim('/')
    if ($candidate.StartsWith('repos/', [System.StringComparison]::OrdinalIgnoreCase)) {
        $candidate = $candidate.Substring('repos/'.Length)
    }

    $parts = $candidate.Split('/')
    if ($parts.Count -lt 2 -or [string]::IsNullOrWhiteSpace($parts[0]) -or [string]::IsNullOrWhiteSpace($parts[1])) {
        return $null
    }

    $repository = ($parts[1] -split '[?#]', 2)[0] -replace '(?i)\.git$', ''
    return "$($parts[0])/$repository".ToLowerInvariant()
}

function Get-CopilotExplicitRepository {
    param([Parameter(Mandatory)][object[]] $Arguments)

    $optionsWithValues = @(
        '-F', '-H', '-X', '-b', '-f', '-q', '-t',
        '--body', '--body-file', '--cache', '--field', '--header', '--hostname',
        '--input', '--jq', '--method', '--raw-field', '--template'
    )

    $index = 0
    while ($index -lt $Arguments.Count) {
        $argument = [string]$Arguments[$index]
        if ($argument -in @('-R', '--repo')) {
            if ($index + 1 -ge $Arguments.Count) {
                return [pscustomobject]@{ Found = $true; Repository = $null }
            }

            return [pscustomobject]@{
                Found = $true
                Repository = ConvertTo-CopilotRepositorySlug -Value ([string]$Arguments[$index + 1])
            }
        }

        if ($argument -match '^--repo=(.*)$') {
            return [pscustomobject]@{
                Found = $true
                Repository = ConvertTo-CopilotRepositorySlug -Value $Matches[1]
            }
        }

        if ($argument -match '^-R(.+)$') {
            return [pscustomobject]@{
                Found = $true
                Repository = ConvertTo-CopilotRepositorySlug -Value $Matches[1]
            }
        }

        if ($argument -in $optionsWithValues -and $index + 1 -lt $Arguments.Count) {
            $index += 2
        }
        else {
            $index++
        }
    }

    return [pscustomobject]@{ Found = $false; Repository = $null }
}

function Get-CopilotCommandOffset {
    param([Parameter(Mandatory)][object[]] $Arguments)

    $index = 0
    while ($index -lt $Arguments.Count) {
        $argument = [string]$Arguments[$index]
        if ($argument -in @('-R', '--repo', '--hostname')) {
            if ($index + 1 -ge $Arguments.Count) {
                return -1
            }

            $index += 2
            continue
        }

        if ($argument -match '^(?:--repo|--hostname)=') {
            $index++
            continue
        }

        if ($argument -match '^-R.+$') {
            $index++
            continue
        }

        return $index
    }

    return -1
}

function Get-CopilotApiEndpoint {
    param(
        [Parameter(Mandatory)][object[]] $Arguments,
        [Parameter(Mandatory)][int] $CommandOffset
    )

    $optionsWithValues = @(
        '-F', '-H', '-R', '-X', '-f', '-q', '-t',
        '--cache', '--field', '--header', '--hostname', '--input', '--jq',
        '--method', '--raw-field', '--repo', '--template'
    )

    $index = $CommandOffset + 1
    while ($index -lt $Arguments.Count) {
        $argument = [string]$Arguments[$index]
        if ($argument -in $optionsWithValues) {
            $index += 2
            continue
        }

        if ($argument.StartsWith('-')) {
            $index++
            continue
        }

        return $argument
    }

    return $null
}

function Test-CopilotTargetsRepository {
    param([Parameter(Mandatory)][object[]] $Arguments)

    $explicitRepository = Get-CopilotExplicitRepository -Arguments $Arguments
    if ($explicitRepository.Found) {
        return $explicitRepository.Repository -ceq $script:CopilotTargetRepository
    }

    $commandOffset = Get-CopilotCommandOffset -Arguments $Arguments
    if ($commandOffset -lt 0 -or $commandOffset -ge $Arguments.Count) {
        return $false
    }

    $command = [string]$Arguments[$commandOffset]
    $subcommand = if ($commandOffset + 1 -lt $Arguments.Count) {
        [string]$Arguments[$commandOffset + 1]
    }
    else {
        ''
    }

    if (
        ($command -in @('issue', 'pr') -and $subcommand -eq 'comment') -or
        ($command -eq 'pr' -and $subcommand -eq 'review')
    ) {
        $targetIndex = $commandOffset + 2
        if ($targetIndex -lt $Arguments.Count) {
            $target = [string]$Arguments[$targetIndex]
            if (
                -not $target.StartsWith('-') -and
                $target -match '^(?i:https?://github\.com/|git@github\.com:|github\.com/)'
            ) {
                return (ConvertTo-CopilotRepositorySlug -Value $target) -ceq $script:CopilotTargetRepository
            }
        }

        return $true
    }

    if ($command -eq 'api') {
        $endpoint = Get-CopilotApiEndpoint -Arguments $Arguments -CommandOffset $commandOffset
        if ([string]::IsNullOrWhiteSpace($endpoint)) {
            return $false
        }

        if ($endpoint -match '(?i)(?:^|/)repos/([^/]+)/([^/?#]+)(?:/|$)') {
            if ($Matches[1] -ceq '{owner}' -and $Matches[2] -ceq '{repo}') {
                return $true
            }

            return "$($Matches[1])/$($Matches[2])".ToLowerInvariant() -ceq $script:CopilotTargetRepository
        }

        return $true
    }

    return $false
}

function New-CopilotPrefixedBodyFile {
    param([Parameter(Mandatory)][string] $Path)

    if ($Path -eq '-') {
        return [pscustomobject]@{
            Path = $Path
            TemporaryPath = $null
        }
    }

    $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    if (-not [System.IO.File]::Exists($resolvedPath)) {
        return [pscustomobject]@{
            Path = $Path
            TemporaryPath = $null
        }
    }

    $body = [System.IO.File]::ReadAllText($resolvedPath)
    $prefixedBody = Add-CopilotCommentPrefix -Body $body
    if ($prefixedBody -ceq $body) {
        return [pscustomobject]@{
            Path = $Path
            TemporaryPath = $null
        }
    }

    $temporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) "$([System.IO.Path]::GetRandomFileName()).md"
    [System.IO.File]::WriteAllText($temporaryPath, $prefixedBody, [System.Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{
        Path = $temporaryPath
        TemporaryPath = $temporaryPath
    }
}

function New-CopilotPrefixedJsonFile {
    param([Parameter(Mandatory)][string] $Path)

    if ($Path -eq '-') {
        return [pscustomobject]@{
            Path = $Path
            TemporaryPath = $null
        }
    }

    $resolvedPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    if (-not [System.IO.File]::Exists($resolvedPath)) {
        return [pscustomobject]@{
            Path = $Path
            TemporaryPath = $null
        }
    }

    $payload = [System.IO.File]::ReadAllText($resolvedPath) | ConvertFrom-Json -Depth 100
    $bodyProperty = $payload.PSObject.Properties['body']
    if ($null -eq $bodyProperty -or $bodyProperty.Value -isnot [string]) {
        return [pscustomobject]@{
            Path = $Path
            TemporaryPath = $null
        }
    }

    $prefixedBody = Add-CopilotCommentPrefix -Body $bodyProperty.Value
    if ($prefixedBody -ceq $bodyProperty.Value) {
        return [pscustomobject]@{
            Path = $Path
            TemporaryPath = $null
        }
    }

    $bodyProperty.Value = $prefixedBody
    $temporaryPath = Join-Path ([System.IO.Path]::GetTempPath()) "$([System.IO.Path]::GetRandomFileName()).json"
    $payloadJson = $payload | ConvertTo-Json -Compress -Depth 100
    [System.IO.File]::WriteAllText($temporaryPath, $payloadJson, [System.Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{
        Path = $temporaryPath
        TemporaryPath = $temporaryPath
    }
}

function ConvertTo-CopilotPrefixedGhArguments {
    param(
        [Parameter(Mandatory)][object[]] $GhArguments,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.List[string]] $TemporaryPaths
    )

    $arguments = [System.Collections.Generic.List[object]]::new()
    foreach ($argument in $GhArguments) {
        $arguments.Add($argument)
    }

    if (-not (Test-CopilotTargetsRepository -Arguments $arguments.ToArray())) {
        return [pscustomobject]@{
            Arguments = $arguments.ToArray()
        }
    }

    $commandOffset = Get-CopilotCommandOffset -Arguments $arguments.ToArray()
    $isIssueOrPrComment = (
        $commandOffset -ge 0 -and
        $commandOffset + 1 -lt $arguments.Count -and
        [string]$arguments[$commandOffset] -in @('issue', 'pr') -and
        [string]$arguments[$commandOffset + 1] -eq 'comment'
    )
    $isPrReview = (
        $commandOffset -ge 0 -and
        $commandOffset + 1 -lt $arguments.Count -and
        [string]$arguments[$commandOffset] -eq 'pr' -and
        [string]$arguments[$commandOffset + 1] -eq 'review'
    )

    if ($isIssueOrPrComment -or $isPrReview) {
        for ($index = $commandOffset + 2; $index -lt $arguments.Count; $index++) {
            $argument = [string]$arguments[$index]

            if ($argument -in @('--body', '-b')) {
                if ($index + 1 -ge $arguments.Count) {
                    continue
                }

                $arguments[$index + 1] = Add-CopilotCommentPrefix -Body ([string]$arguments[$index + 1])
                $index++
                continue
            }

            if ($argument -match '^--body=(.*)$') {
                $arguments[$index] = "--body=$(Add-CopilotCommentPrefix -Body $Matches[1])"
                continue
            }

            if ($argument -in @('--body-file', '-F')) {
                if ($index + 1 -ge $arguments.Count) {
                    continue
                }

                $file = New-CopilotPrefixedBodyFile -Path ([string]$arguments[$index + 1])
                $arguments[$index + 1] = $file.Path
                if ($null -ne $file.TemporaryPath) {
                    $TemporaryPaths.Add($file.TemporaryPath)
                }

                $index++
                continue
            }

            if ($argument -match '^--body-file=(.*)$') {
                $file = New-CopilotPrefixedBodyFile -Path $Matches[1]
                $arguments[$index] = "--body-file=$($file.Path)"
                if ($null -ne $file.TemporaryPath) {
                    $TemporaryPaths.Add($file.TemporaryPath)
                }
            }
        }
    }

    $isApi = $commandOffset -ge 0 -and [string]$arguments[$commandOffset] -eq 'api'
    if ($isApi) {
        $endpoint = Get-CopilotApiEndpoint -Arguments $arguments.ToArray() -CommandOffset $commandOffset
        $isCommentEndpoint = $endpoint -match '(?i)(?:/issues/\d+/comments|/issues/comments/\d+|/pulls/\d+/(?:comments|reviews)|/pulls/comments/\d+|/comments/\d+/replies|/reviews/\d+/comments)'
        $explicitMethod = $null
        $hasWriteField = $false

        for ($index = $commandOffset + 1; $index -lt $arguments.Count; $index++) {
            $argument = [string]$arguments[$index]

            if ($argument -in @('-X', '--method')) {
                if ($index + 1 -lt $arguments.Count) {
                    $explicitMethod = ([string]$arguments[$index + 1]).ToUpperInvariant()
                }
                continue
            }

            if ($argument -match '^(?:-X|--method)=(.+)$') {
                $explicitMethod = $Matches[1].ToUpperInvariant()
                continue
            }

            if ($argument -in @('-f', '--raw-field', '-F', '--field', '--input')) {
                $hasWriteField = $true
            }
        }

        $isApiWrite = $isCommentEndpoint -and (
            $explicitMethod -in @('POST', 'PUT', 'PATCH') -or
            ($null -eq $explicitMethod -and $hasWriteField)
        )

        if ($isApiWrite) {
            for ($index = $commandOffset + 1; $index -lt $arguments.Count; $index++) {
                $argument = [string]$arguments[$index]

                if ($argument -in @('-f', '--raw-field', '-F', '--field')) {
                    if ($index + 1 -ge $arguments.Count) {
                        continue
                    }

                    $field = [string]$arguments[$index + 1]
                    if ($field -notmatch '^body=(.*)$') {
                        $index++
                        continue
                    }

                    $fieldValue = $Matches[1]
                    if ($argument -in @('-F', '--field') -and $fieldValue.StartsWith('@')) {
                        $file = New-CopilotPrefixedBodyFile -Path $fieldValue.Substring(1)
                        $arguments[$index + 1] = "body=@$($file.Path)"
                        if ($null -ne $file.TemporaryPath) {
                            $TemporaryPaths.Add($file.TemporaryPath)
                        }
                    }
                    else {
                        $arguments[$index + 1] = "body=$(Add-CopilotCommentPrefix -Body $fieldValue)"
                    }

                    $index++
                    continue
                }

                if ($argument -eq '--input') {
                    if ($index + 1 -ge $arguments.Count) {
                        continue
                    }

                    $file = New-CopilotPrefixedJsonFile -Path ([string]$arguments[$index + 1])
                    $arguments[$index + 1] = $file.Path
                    if ($null -ne $file.TemporaryPath) {
                        $TemporaryPaths.Add($file.TemporaryPath)
                    }

                    $index++
                }
            }
        }
    }

    return [pscustomobject]@{
        Arguments = $arguments.ToArray()
    }
}

if ($null -ne $script:CopilotRealGhPath) {
    function gh {
        $originalArguments = @($args)
        $temporaryPaths = [System.Collections.Generic.List[string]]::new()
        try {
            $converted = ConvertTo-CopilotPrefixedGhArguments -GhArguments $originalArguments -TemporaryPaths $temporaryPaths
        }
        catch {
            $converted = [pscustomobject]@{
                Arguments = $originalArguments
            }
        }

        $invokeArguments = @($converted.Arguments)
        try {
            & $script:CopilotRealGhPath @invokeArguments
            $exitCode = $LASTEXITCODE
        }
        finally {
            foreach ($temporaryPath in $temporaryPaths) {
                Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
            }
        }

        $global:LASTEXITCODE = $exitCode
    }
}
