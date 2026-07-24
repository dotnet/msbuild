# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

function Get-SafeFileName
{
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Value)

    $safeName = $Value -replace '[^A-Za-z0-9_.-]', '_'
    if ($safeName.Length -gt 120)
    {
        $safeName = $safeName.Substring(0, 120)
    }

    $safeName
}

function Test-SafeMetricName
{
    [OutputType([bool])]
    param([Parameter(Mandatory)][string]$Name)

    $Name -match '^(build-time|evaluation-time(?:-.+)?|exit-code|recollected-attempts|msbuild-(?:display-)?version|dotnet-version|crank-netSdkVersion|info/(?:test-asset|test-scenario|msbuild-app|test-version|iterations-number))$'
}

function ConvertTo-AllowlistedMetrics
{
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param([Parameter(Mandatory)]$Properties)

    $metrics = [ordered]@{}
    foreach ($property in $Properties)
    {
        if (-not (Test-SafeMetricName -Name $property.Name))
        {
            continue
        }

        # Nested objects and arrays are deliberately excluded from public evidence.
        if ($property.Value -is [string] -or $property.Value -is [ValueType])
        {
            $metrics[$property.Name] = $property.Value
        }
    }

    $metrics
}

function Read-HostedMetrics
{
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param([Parameter(Mandatory)][string]$Path)

    $metrics = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $Path)
    {
        if ($line -match '^##METRIC##\s+([^=]+)=(.*)$')
        {
            $name = $Matches[1].Trim()
            if (Test-SafeMetricName -Name $name)
            {
                $metrics[$name] = $Matches[2].Trim()
            }
        }
    }

    $metrics
}

function Get-HostedLogExcerpt
{
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$Path)

    # Only status, heartbeat, and timing lines cross the trusted-job boundary.
    $safeLinePattern = '(?i)^\s*(\[heartbeat\].*|Build succeeded\.|Build FAILED\.|Time Elapsed .+|\d+\s+Warning\(s\)|\d+\s+Error\(s\)|Shutting down .+|.+ server shut down successfully\.|Test .+ run was completed\.|Clean up for test .+ was completed\.)\s*$'
    $selected = [System.Collections.Generic.List[string]]::new()

    foreach ($line in Get-Content -LiteralPath $Path)
    {
        if ($line -match $safeLinePattern)
        {
            $selected.Add($line.Trim())
        }
    }

    $bounded = if ($selected.Count -le 80)
    {
        @($selected)
    }
    else
    {
        @($selected | Select-Object -First 40) + @('[... excerpt truncated ...]') + @($selected | Select-Object -Last 40)
    }

    $excerpt = $bounded -join "`n"
    if ($excerpt.Length -gt 8000)
    {
        $excerpt = $excerpt.Substring(0, 8000) + "`n[... excerpt truncated by character limit ...]"
    }

    $excerpt
}

Export-ModuleMember -Function @(
    'Get-SafeFileName',
    'Test-SafeMetricName',
    'ConvertTo-AllowlistedMetrics',
    'Read-HostedMetrics',
    'Get-HostedLogExcerpt'
)
