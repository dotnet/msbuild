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

    $Name -match '^(build-time|evaluation-time(?:-.+)?|exit-code|recollected-attempts|msbuild-(?:display-)?version|dotnet-version|crank-netSdkVersion|info/(?:test-asset|test-scenario|msbuild-app|test-version|iterations-number))\z'
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

function Expand-BoundedArchive
{
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string]$ArchivePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [long]$MaximumCompressedBytes = 100MB,
        [long]$MaximumUncompressedBytes = 500MB,
        [int]$MaximumEntryCount = 10000
    )

    if ((Get-Item -LiteralPath $ArchivePath).Length -gt $MaximumCompressedBytes)
    {
        Write-Warning 'Artifact archive exceeds the compressed-size limit.'
        return $false
    }

    $archive = $null
    try
    {
        $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
        if ($archive.Entries.Count -gt $MaximumEntryCount)
        {
            Write-Warning 'Artifact archive exceeds the entry-count limit.'
            return $false
        }

        $entryNames = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::OrdinalIgnoreCase)
        $totalBytes = 0L
        foreach ($entry in $archive.Entries)
        {
            $entryName = $entry.FullName.Replace('\', '/')
            if ([string]::IsNullOrWhiteSpace($entryName) -or -not $entryNames.Add($entryName))
            {
                Write-Warning 'Artifact archive contains an empty or duplicate entry name.'
                return $false
            }

            if ($entry.Length -gt $MaximumUncompressedBytes - $totalBytes)
            {
                Write-Warning 'Artifact archive exceeds the uncompressed-size limit.'
                return $false
            }

            $totalBytes += $entry.Length
        }
    }
    catch
    {
        Write-Warning "Artifact archive could not be inspected safely ($($_.Exception.GetType().Name))."
        return $false
    }
    finally
    {
        if ($null -ne $archive)
        {
            $archive.Dispose()
        }
    }

    try
    {
        # The .NET extractor rejects entries that resolve outside DestinationPath.
        [IO.Compression.ZipFile]::ExtractToDirectory($ArchivePath, $DestinationPath, $true)
        $true
    }
    catch
    {
        # Report only the exception type; entry names are untrusted data.
        Write-Warning "Artifact archive could not be extracted safely ($($_.Exception.GetType().Name))."
        $false
    }
}

Export-ModuleMember -Function @(
    'Get-SafeFileName',
    'Test-SafeMetricName',
    'ConvertTo-AllowlistedMetrics',
    'Read-HostedMetrics',
    'Get-HostedLogExcerpt',
    'Expand-BoundedArchive'
)
