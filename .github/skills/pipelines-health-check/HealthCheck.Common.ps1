#requires -Version 5.1

function Invoke-HealthJsonCommand
{
    [CmdletBinding()]
    param(
        [ValidateSet('az', 'gh')]
        [string]$Command,
        [string[]]$Arguments,
        [string]$Operation
    )

    if (!(Get-Command $Command -ErrorAction SilentlyContinue))
    {
        throw "$Operation is unavailable: '$Command' is not installed. No installation was attempted."
    }

    # Capture native failures without depending on the caller's PowerShell version/preferences.
    $PSNativeCommandUseErrorActionPreference = $false
    $ErrorActionPreference = 'Continue'
    $output = @(& $Command @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($exitCode -ne 0)
    {
        throw "$Operation failed (exit $exitCode). Check the configured service and existing access; raw CLI diagnostics are withheld to avoid exposing credentials."
    }

    $json = ($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) -join "`n"
    if ([string]::IsNullOrWhiteSpace($json))
    {
        throw "$Operation returned no JSON; the requested data is unavailable."
    }

    # PowerShell 5.1 preserves root arrays; newer versions enumerate them by default.
    $jsonOptions = @{ InputObject = $json; ErrorAction = 'Stop' }
    if ((Get-Command ConvertFrom-Json).Parameters.ContainsKey('NoEnumerate'))
    {
        $jsonOptions.NoEnumerate = $true
    }
    $parsed = ConvertFrom-Json @jsonOptions
    Write-Output -InputObject $parsed
}

function Get-HealthOrganizationName
{
    param([string]$Organization)

    $uri = [uri]$Organization
    if (!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo)
    {
        throw 'Use an HTTPS Azure DevOps organization URL without credentials.'
    }

    if ($uri.Host -eq 'dev.azure.com')
    {
        $name = $uri.AbsolutePath.Trim('/')
    }
    elseif ($uri.Host.EndsWith('.visualstudio.com', [StringComparison]::OrdinalIgnoreCase))
    {
        $name = $uri.Host.Substring(0, $uri.Host.Length - '.visualstudio.com'.Length)
    }
    else
    {
        throw 'The organization must use dev.azure.com or an Azure DevOps visualstudio.com host.'
    }

    if ($name -notmatch '^[a-zA-Z0-9][a-zA-Z0-9-]*$')
    {
        throw 'The organization URL must identify one Azure DevOps organization.'
    }

    $name
}

function Invoke-HealthAzDoGet
{
    param(
        [string]$Url,
        [switch]$Public
    )

    $uri = [uri]$Url
    if (!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or
        ($uri.Host -notin @('dev.azure.com', 'vssps.dev.azure.com') -and
         !$uri.Host.EndsWith('.visualstudio.com', [StringComparison]::OrdinalIgnoreCase)))
    {
        throw 'Refusing a request outside the configured HTTPS Azure DevOps service.'
    }

    if ($Public)
    {
        Invoke-RestMethod -Uri $Url -Method Get -ErrorAction Stop
    }
    else
    {
        Invoke-HealthJsonCommand -Command az -Operation 'Azure DevOps GET' -Arguments @(
            'rest', '--method', 'get', '--url', $Url,
            '--resource', '499b84ac-1321-427f-aa17-267ca6975798', '-o', 'json'
        )
    }
}

function Get-HealthResponseValues
{
    param(
        $Response,
        [string]$Operation
    )

    if ($null -eq $Response -or $null -eq $Response.PSObject.Properties['value'] -or
        $Response.value -isnot [System.Array])
    {
        throw "$Operation returned an invalid collection; coverage is unknown."
    }

    $Response.value
}

function Get-HealthPagedValues
{
    param(
        [string]$Url,
        [ValidateRange(1, 1000)]
        [int]$MaxPages = 100
    )

    $results = [System.Collections.Generic.List[object]]::new()
    $previousPage = $null
    for ($pageNumber = 0; $pageNumber -lt $MaxPages; $pageNumber++)
    {
        $separator = if ($Url.Contains('?')) { '&' } else { '?' }
        $pageUrl = "$Url${separator}`$top=100&`$skip=$($results.Count)"
        $response = Invoke-HealthAzDoGet -Url $pageUrl
        $page = @(Get-HealthResponseValues -Response $response -Operation 'Azure DevOps paginated GET')
        if ($page.Count -eq 0)
        {
            return $results.ToArray()
        }
        $fingerprint = ConvertTo-Json -InputObject $page -Depth 20 -Compress
        if ($fingerprint -eq $previousPage)
        {
            throw 'Azure DevOps pagination did not advance; coverage is incomplete.'
        }

        foreach ($item in $page)
        {
            $results.Add($item)
        }

        $previousPage = $fingerprint
    }

    throw "Collection exceeded $MaxPages pages; narrow the scope or explicitly increase MaxPages. No complete result was produced."
}

function ConvertTo-HealthText
{
    param(
        [string]$Text,
        [int]$MaxLength = 500
    )

    if ([string]::IsNullOrEmpty($Text)) { return '' }
    $text = $Text -replace '[\x00-\x1F\x7F]', ' '
    $text = $text -replace '(?i)\b(Bearer|Basic)\s+\S+', '$1 [redacted]'
    $text = $text -replace '(?i)([?&](?:sig|token|access_token|code|key|client_secret)=)[^&\s]+', '$1[redacted]'
    $text = $text -replace '(?i)\b(password|passwd|secret|authorization)\s*[:=]\s*\S+', '$1=[redacted]'
    $text = ($text -replace '\s+', ' ').Trim()
    if ($text.Length -gt $MaxLength) { return $text.Substring(0, $MaxLength) + '...' }
    $text
}

function Get-HealthRunState
{
    param([object[]]$Runs = @())

    if ($Runs.Count -eq 0)
    {
        return [pscustomobject]@{ state = 'UNKNOWN'; summary = 'UNKNOWN - no matching runs in the requested sample' }
    }

    $latest = $Runs[0]
    $state = 'UNKNOWN'
    $reason = 'latest run has an unrecognized status/result'
    switch ([string]$latest.status)
    {
        'notStarted' { $state = 'PENDING'; $reason = 'latest run is queued' }
        'postponed' { $state = 'PENDING'; $reason = 'latest run is postponed' }
        'inProgress' { $state = 'IN_PROGRESS'; $reason = 'latest run is building' }
        'cancelling' { $state = 'IN_PROGRESS'; $reason = 'latest run is cancelling' }
        'completed'
        {
            switch ([string]$latest.result)
            {
                'succeeded' { $state = 'HEALTHY'; $reason = 'latest run succeeded' }
                'failed' { $state = 'UNHEALTHY'; $reason = 'latest run failed' }
                'partiallySucceeded' { $state = 'UNHEALTHY'; $reason = 'latest run only partially succeeded' }
                'canceled' { $state = 'CANCELED'; $reason = 'latest run was canceled; no success is implied' }
            }
        }
    }

    [pscustomobject]@{ state = $state; summary = "$state - $reason" }
}

function Get-HealthTimelineDetails
{
    param(
        [string]$Url,
        [switch]$Public
    )

    $timeline = Invoke-HealthAzDoGet -Url $Url -Public:$Public
    if ($null -eq $timeline -or $null -eq $timeline.PSObject.Properties['records'] -or
        $timeline.records -isnot [System.Array])
    {
        throw 'The build timeline is unavailable or malformed.'
    }

    foreach ($record in $timeline.records)
    {
        $issues = @($record.issues | Where-Object { $_.type -eq 'error' })
        if ($record.result -eq 'failed' -or $issues.Count -gt 0)
        {
            [pscustomobject][ordered]@{
                id = $record.id
                parentId = $record.parentId
                type = $record.type
                name = $record.name
                result = $record.result
                logId = if ($record.log) { $record.log.id } else { $null }
                errors = @($issues | ForEach-Object { ConvertTo-HealthText -Text $_.message })
            }
        }
    }
}
