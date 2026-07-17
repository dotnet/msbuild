# Copyright (c) Microsoft. All rights reserved.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InputReport,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [string]$OrganizationUri = 'https://dev.azure.com/devdiv',

    [string]$Project = 'DevDiv'
)

$ErrorActionPreference = 'Stop'

$accessToken = $env:AZDO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($accessToken))
{
    throw 'AZDO_ACCESS_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $InputReport))
{
    throw "Regression report not found: $InputReport"
}

$script:projectBaseUri = "$($OrganizationUri.TrimEnd('/'))/$([Uri]::EscapeDataString($Project))"
$script:headers = @{ Authorization = "Bearer $accessToken" }
$script:runCache = @{}
$script:artifactCache = @{}
$script:rawDirectory = Join-Path $env:RUNNER_TEMP "mt-regression-raw-$([Guid]::NewGuid().ToString('N'))"

function Invoke-AzdoJson
{
    param([Parameter(Mandatory)][string]$Uri)

    for ($attempt = 1; $attempt -le 4; $attempt++)
    {
        try
        {
            return Invoke-RestMethod -Method Get -Uri $Uri -Headers $script:headers
        }
        catch
        {
            $statusCode = if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
            $retryable = $statusCode -in @(0, 408, 429, 500, 502, 503, 504)
            if (-not $retryable -or $attempt -eq 4)
            {
                throw
            }

            Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
        }
    }
}

function Get-PipelineDefinitionId
{
    param([Parameter(Mandatory)][string]$Backend)

    switch ($Backend)
    {
        'Gold' { return 25429 }
        'Hosted' { return 28338 }
        default { throw "Unsupported PerfStar backend '$Backend'." }
    }
}

function Get-RunMetadata
{
    param(
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Backend
    )

    $cacheKey = "$Backend/$BuildId"
    if ($script:runCache.ContainsKey($cacheKey))
    {
        return $script:runCache[$cacheKey]
    }

    $definitionId = Get-PipelineDefinitionId -Backend $Backend
    $runUri = "$script:projectBaseUri/_apis/pipelines/$definitionId/runs/$([Uri]::EscapeDataString($BuildId))?api-version=7.1"
    $run = Invoke-AzdoJson -Uri $runUri
    $component = $run.resources.pipelines.ComponentBuildUnderTest

    $componentBuild = $null
    if ($null -ne $component -and $null -ne $component.pipeline.id)
    {
        $componentBuildId = [string]$component.pipeline.id
        $componentBuildUri = "$script:projectBaseUri/_apis/build/builds/$([Uri]::EscapeDataString($componentBuildId))?api-version=7.1"
        $componentBuild = Invoke-AzdoJson -Uri $componentBuildUri
    }

    $metadata = [pscustomobject][ordered]@{
        perfStarBuildId = [string]$run.id
        perfStarBuildNumber = [string]$run.name
        perfStarBuildResult = [string]$run.result
        perfStarBuildUrl = [string]$run._links.web.href
        componentBuildId = if ($null -ne $componentBuild) { [string]$componentBuild.id } else { '' }
        componentBuildNumber = if ($null -ne $componentBuild) { [string]$componentBuild.buildNumber } else { [string]$component.version }
        componentSourceBranch = if ($null -ne $componentBuild) { [string]$componentBuild.sourceBranch } else { '' }
        componentSourceVersion = if ($null -ne $componentBuild) { [string]$componentBuild.sourceVersion } else { '' }
        componentBuildResult = if ($null -ne $componentBuild) { [string]$componentBuild.result } else { '' }
        componentBuildUrl = if ($null -ne $componentBuild) { [string]$componentBuild._links.web.href } else { '' }
    }

    $script:runCache[$cacheKey] = $metadata
    return $metadata
}

function Get-SafeFileName
{
    param([Parameter(Mandatory)][string]$Value)

    $safeName = $Value -replace '[^A-Za-z0-9_.-]', '_'
    if ($safeName.Length -gt 120)
    {
        $safeName = $safeName.Substring(0, 120)
    }

    $safeName
}

function Get-ArtifactDirectory
{
    param(
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$ArtifactName
    )

    $cacheKey = "$BuildId/$ArtifactName"
    if ($script:artifactCache.ContainsKey($cacheKey))
    {
        return $script:artifactCache[$cacheKey]
    }

    $escapedArtifactName = [Uri]::EscapeDataString($ArtifactName)
    $artifactUri = "$script:projectBaseUri/_apis/build/builds/$([Uri]::EscapeDataString($BuildId))/artifacts?artifactName=$escapedArtifactName&api-version=7.1"

    try
    {
        $artifact = Invoke-AzdoJson -Uri $artifactUri
    }
    catch
    {
        if ($null -ne $_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404)
        {
            $script:artifactCache[$cacheKey] = $null
            return $null
        }

        throw
    }

    $downloadUrl = [string]$artifact.resource.downloadUrl
    if ([string]::IsNullOrWhiteSpace($downloadUrl))
    {
        $script:artifactCache[$cacheKey] = $null
        return $null
    }

    $safeName = Get-SafeFileName -Value "$BuildId-$ArtifactName"
    $zipPath = Join-Path $script:rawDirectory "$safeName.zip"
    $extractPath = Join-Path $script:rawDirectory $safeName
    New-Item -ItemType Directory -Force -Path $extractPath | Out-Null

    for ($attempt = 1; $attempt -le 4; $attempt++)
    {
        try
        {
            Invoke-WebRequest -Uri $downloadUrl -Headers $script:headers -OutFile $zipPath
            break
        }
        catch
        {
            $statusCode = if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
            $retryable = $statusCode -in @(0, 408, 429, 500, 502, 503, 504)
            if (-not $retryable -or $attempt -eq 4)
            {
                throw
            }

            Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
        }
    }
    $zipLength = (Get-Item -LiteralPath $zipPath).Length
    if ($zipLength -gt 100MB)
    {
        throw "Artifact '$ArtifactName' from build $BuildId is $([Math]::Round($zipLength / 1MB, 1)) MB; refusing to extract more than 100 MB."
    }

    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force
    Remove-Item -LiteralPath $zipPath -Force
    $script:artifactCache[$cacheKey] = $extractPath
    return $extractPath
}

function Convert-ScalarMetrics
{
    param([Parameter(Mandatory)]$Properties)

    $metrics = [ordered]@{}
    foreach ($property in $Properties)
    {
        if (-not (Test-SafeMetricName -Name $property.Name))
        {
            continue
        }

        if ($property.Value -is [string] -or $property.Value -is [ValueType])
        {
            $metrics[$property.Name] = $property.Value
        }
    }

    $metrics
}

function Read-HostedMetrics
{
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

function Test-SafeMetricName
{
    param([Parameter(Mandatory)][string]$Name)

    $Name -match '^(build-time|evaluation-time(?:-.+)?|exit-code|recollected-attempts|msbuild-(?:display-)?version|dotnet-version|crank-netSdkVersion|info/(?:test-asset|test-scenario|msbuild-app|test-version|iterations-number))$'
}

function Get-HostedLogExcerpt
{
    param([Parameter(Mandatory)][string]$Path)

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

function Get-HostedScenarioEvidence
{
    param(
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)][string]$Scenario,
        [bool]$IncludeLog
    )

    $artifactName = "HostedPerfResults-$Os-$Scenario"
    $artifactDirectory = Get-ArtifactDirectory -BuildId $BuildId -ArtifactName $artifactName
    if ($null -eq $artifactDirectory)
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = 'Artifact was not available.'
        }
    }

    $metricsFile = Get-ChildItem -Path $artifactDirectory -Recurse -File -Filter "$Scenario.metrics.txt" | Select-Object -First 1
    $logFile = Get-ChildItem -Path $artifactDirectory -Recurse -File -Filter "$Scenario.log" | Select-Object -First 1
    $metrics = if ($null -ne $metricsFile) { Read-HostedMetrics -Path $metricsFile.FullName } else { [ordered]@{} }
    $logExcerpt = if ($IncludeLog -and $null -ne $logFile) { Get-HostedLogExcerpt -Path $logFile.FullName } else { '' }

    [pscustomobject][ordered]@{
        available = $true
        artifactName = $artifactName
        metrics = $metrics
        logExcerpt = $logExcerpt
    }
}

function Get-GoldScenarioEvidence
{
    param(
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)][string]$Scenario
    )

    $agentName = if ($Os -eq 'Windows') { 'GOLDWIN' } else { 'GOLDLIN' }
    $artifactName = "CrankAssetsThinned$agentName"
    $artifactDirectory = Get-ArtifactDirectory -BuildId $BuildId -ArtifactName $artifactName
    if ($null -eq $artifactDirectory)
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = 'Artifact was not available.'
        }
    }

    $resultFile = Get-ChildItem -Path $artifactDirectory -Recurse -File -Filter "$Scenario.json" | Select-Object -First 1
    if ($null -eq $resultFile)
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = "Scenario result '$Scenario.json' was not present."
        }
    }

    $result = Get-Content -LiteralPath $resultFile.FullName -Raw | ConvertFrom-Json
    [pscustomobject][ordered]@{
        available = $true
        artifactName = $artifactName
        timestamp = [string]$result.timestamp
        metrics = Convert-ScalarMetrics -Properties $result.results.PSObject.Properties
        logExcerpt = ''
    }
}

function Get-ScenarioEvidence
{
    param(
        [Parameter(Mandatory)][string]$Backend,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)][string]$Scenario,
        [bool]$IncludeLog
    )

    if ($Backend -eq 'Hosted')
    {
        return Get-HostedScenarioEvidence -BuildId $BuildId -Os $Os -Scenario $Scenario -IncludeLog $IncludeLog
    }

    Get-GoldScenarioEvidence -BuildId $BuildId -Os $Os -Scenario $Scenario
}

function Add-CandidateProperties
{
    param(
        [Parameter(Mandatory)]$Candidate,
        [Parameter(Mandatory)]$CurrentRun,
        $HealthyRun,
        [Parameter(Mandatory)]$CurrentMtEvidence,
        [Parameter(Mandatory)]$CurrentNonMtEvidence,
        $HealthyMtEvidence,
        $HealthyNonMtEvidence
    )

    $properties = [ordered]@{}
    foreach ($property in $Candidate.PSObject.Properties)
    {
        $properties[$property.Name] = $property.Value
    }

    $properties.currentRun = $CurrentRun
    $properties.healthyRun = $HealthyRun
    $properties.currentMtEvidence = $CurrentMtEvidence
    $properties.currentNonMtEvidence = $CurrentNonMtEvidence
    $properties.healthyMtEvidence = $HealthyMtEvidence
    $properties.healthyNonMtEvidence = $HealthyNonMtEvidence
    [pscustomobject]$properties
}

function Get-CompareUrl
{
    param($HealthyRun, $CurrentRun)

    if ($null -eq $HealthyRun -or
        [string]::IsNullOrWhiteSpace($HealthyRun.componentSourceVersion) -or
        [string]::IsNullOrWhiteSpace($CurrentRun.componentSourceVersion))
    {
        return ''
    }

    "https://github.com/dotnet/msbuild/compare/$($HealthyRun.componentSourceVersion)...$($CurrentRun.componentSourceVersion)"
}

function Add-MetricTable
{
    param(
        [Parameter(Mandatory)][System.Text.StringBuilder]$Builder,
        $CurrentMt,
        $CurrentNonMt,
        $HealthyMt,
        $HealthyNonMt
    )

    $metricNames = @(
        'build-time',
        'evaluation-time',
        'evaluation-time-globbing',
        'evaluation-time-pass0',
        'evaluation-time-pass1',
        'evaluation-time-pass2',
        'evaluation-time-pass3',
        'evaluation-time-pass3dot1',
        'evaluation-time-pass4',
        'evaluation-time-pass5',
        'exit-code',
        'msbuild-version',
        'dotnet-version'
    )

    [void]$Builder.AppendLine('| Metric | Current MT | Current non-MT | Healthy MT | Healthy non-MT |')
    [void]$Builder.AppendLine('| --- | ---: | ---: | ---: | ---: |')
    foreach ($metricName in $metricNames)
    {
        $currentMtValue = if ($null -ne $CurrentMt -and $CurrentMt.available) { $CurrentMt.metrics.$metricName } else { '' }
        $currentNonMtValue = if ($null -ne $CurrentNonMt -and $CurrentNonMt.available) { $CurrentNonMt.metrics.$metricName } else { '' }
        $healthyMtValue = if ($null -ne $HealthyMt -and $HealthyMt.available) { $HealthyMt.metrics.$metricName } else { '' }
        $healthyNonMtValue = if ($null -ne $HealthyNonMt -and $HealthyNonMt.available) { $HealthyNonMt.metrics.$metricName } else { '' }
        if (@($currentMtValue, $currentNonMtValue, $healthyMtValue, $healthyNonMtValue) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
        {
            [void]$Builder.AppendLine("| ``$metricName`` | $currentMtValue | $currentNonMtValue | $healthyMtValue | $healthyNonMtValue |")
        }
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $script:rawDirectory | Out-Null

try
{
    $report = Get-Content -LiteralPath $InputReport -Raw | ConvertFrom-Json
    $enrichedCandidates = @(
        foreach ($candidate in @($report.candidates))
        {
            $currentRun = Get-RunMetadata -BuildId ([string]$candidate.CurrentBuildId) -Backend ([string]$candidate.Backend)
            $healthyRun = if (-not [string]::IsNullOrWhiteSpace([string]$candidate.HealthyBuildId))
            {
                Get-RunMetadata -BuildId ([string]$candidate.HealthyBuildId) -Backend ([string]$candidate.Backend)
            }
            else
            {
                $null
            }

            $currentMtEvidence = Get-ScenarioEvidence -Backend $candidate.Backend -BuildId $candidate.CurrentBuildId -Os $candidate.Os -Scenario $candidate.MtScenario -IncludeLog $true
            $currentNonMtEvidence = Get-ScenarioEvidence -Backend $candidate.Backend -BuildId $candidate.CurrentBuildId -Os $candidate.Os -Scenario $candidate.NonMtScenario -IncludeLog $true
            $healthyMtEvidence = if ($null -ne $healthyRun)
            {
                Get-ScenarioEvidence -Backend $candidate.Backend -BuildId $candidate.HealthyBuildId -Os $candidate.Os -Scenario $candidate.MtScenario -IncludeLog $false
            }
            else
            {
                $null
            }
            $healthyNonMtEvidence = if ($null -ne $healthyRun)
            {
                Get-ScenarioEvidence -Backend $candidate.Backend -BuildId $candidate.HealthyBuildId -Os $candidate.Os -Scenario $candidate.NonMtScenario -IncludeLog $false
            }
            else
            {
                $null
            }

            Add-CandidateProperties `
                -Candidate $candidate `
                -CurrentRun $currentRun `
                -HealthyRun $healthyRun `
                -CurrentMtEvidence $currentMtEvidence `
                -CurrentNonMtEvidence $currentNonMtEvidence `
                -HealthyMtEvidence $healthyMtEvidence `
                -HealthyNonMtEvidence $healthyNonMtEvidence
        }
    )

    $evidenceReport = [ordered]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        candidateCount = $enrichedCandidates.Count
        candidates = $enrichedCandidates
    }
    $jsonPath = Join-Path $OutputDirectory 'mt-regression-evidence.json'
    $markdownPath = Join-Path $OutputDirectory 'mt-regression-evidence.md'
    $evidenceReport | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

    $markdown = [System.Text.StringBuilder]::new()
    [void]$markdown.AppendLine('# PerfStar actual-run evidence')
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('Raw Azure DevOps artifacts were downloaded into an ephemeral directory, transformed, sanitized, bounded, and deleted. Only this derived report is uploaded to GitHub.')

    foreach ($candidate in $enrichedCandidates)
    {
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine("## $($candidate.Backend)/$($candidate.Os): ``$($candidate.ScenarioPair)``")
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine("- Current PerfStar run: [$($candidate.currentRun.perfStarBuildNumber)]($($candidate.currentRun.perfStarBuildUrl))")
        [void]$markdown.AppendLine("- Current MSBuild build: [$($candidate.currentRun.componentBuildNumber)]($($candidate.currentRun.componentBuildUrl))")
        [void]$markdown.AppendLine("- Current MSBuild source: ``$($candidate.currentRun.componentSourceVersion)``")

        if ($null -ne $candidate.healthyRun)
        {
            [void]$markdown.AppendLine("- Last healthy PerfStar run: [$($candidate.healthyRun.perfStarBuildNumber)]($($candidate.healthyRun.perfStarBuildUrl))")
            [void]$markdown.AppendLine("- Last healthy MSBuild build: [$($candidate.healthyRun.componentBuildNumber)]($($candidate.healthyRun.componentBuildUrl))")
            [void]$markdown.AppendLine("- Last healthy MSBuild source: ``$($candidate.healthyRun.componentSourceVersion)``")
            $compareUrl = Get-CompareUrl -HealthyRun $candidate.healthyRun -CurrentRun $candidate.currentRun
            if (-not [string]::IsNullOrWhiteSpace($compareUrl))
            {
                [void]$markdown.AppendLine("- Source comparison: [healthy...current]($compareUrl)")
            }
        }

        [void]$markdown.AppendLine()
        Add-MetricTable `
            -Builder $markdown `
            -CurrentMt $candidate.currentMtEvidence `
            -CurrentNonMt $candidate.currentNonMtEvidence `
            -HealthyMt $candidate.healthyMtEvidence `
            -HealthyNonMt $candidate.healthyNonMtEvidence

        foreach ($entry in @(
            @{ Name = 'Current MT log excerpt'; Evidence = $candidate.currentMtEvidence },
            @{ Name = 'Current non-MT log excerpt'; Evidence = $candidate.currentNonMtEvidence }))
        {
            if ($null -ne $entry.Evidence -and -not [string]::IsNullOrWhiteSpace([string]$entry.Evidence.logExcerpt))
            {
                [void]$markdown.AppendLine()
                [void]$markdown.AppendLine("<details><summary>$($entry.Name)</summary>")
                [void]$markdown.AppendLine()
                [void]$markdown.AppendLine('```text')
                [void]$markdown.AppendLine($entry.Evidence.logExcerpt)
                [void]$markdown.AppendLine('```')
                [void]$markdown.AppendLine('</details>')
            }
        }
    }

    $markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM
    Write-Host "Wrote actual-run evidence for $($enrichedCandidates.Count) candidate(s) to $OutputDirectory."
}
finally
{
    Remove-Item -LiteralPath $script:rawDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
