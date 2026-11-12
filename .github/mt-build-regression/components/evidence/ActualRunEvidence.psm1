# Copyright (c) Microsoft. All rights reserved.

Import-Module (Join-Path $PSScriptRoot '..\clients\AzureDevOpsClient.psm1')
Import-Module (Join-Path $PSScriptRoot 'RunSelection.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceSanitizer.psm1')

function New-ActualRunEvidenceContext
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]$AzureDevOpsClient,
        [Parameter(Mandatory)][string]$RawDirectory
    )

    [pscustomobject][ordered]@{
        AzureDevOpsClient = $AzureDevOpsClient
        RawDirectory = $RawDirectory
        RunCache = New-PerfStarRunCache
        ArtifactCache = @{}
    }
}

function Get-ArtifactDirectory
{
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$ArtifactName
    )

    $cacheKey = "$BuildId/$ArtifactName"
    if ($Context.ArtifactCache.ContainsKey($cacheKey))
    {
        return $Context.ArtifactCache[$cacheKey]
    }

    $artifact = Get-AzureDevOpsArtifact -Client $Context.AzureDevOpsClient -BuildId $BuildId -ArtifactName $ArtifactName
    $downloadUrl = if ($null -ne $artifact) { [string]$artifact.resource.downloadUrl } else { '' }
    if ([string]::IsNullOrWhiteSpace($downloadUrl))
    {
        $Context.ArtifactCache[$cacheKey] = $null
        return $null
    }

    $safeName = Get-SafeFileName -Value "$BuildId-$ArtifactName"
    if (-not (Test-AzureDevOpsArtifactUrl -Url $downloadUrl))
    {
        Write-Warning "Artifact '$safeName' has an unapproved download URL."
        $Context.ArtifactCache[$cacheKey] = $null
        return $null
    }

    $zipPath = Join-Path $Context.RawDirectory "$safeName.zip"
    $extractPath = Join-Path $Context.RawDirectory $safeName
    New-Item -ItemType Directory -Force -Path $extractPath | Out-Null

    try
    {
        $downloaded = Save-AzureDevOpsArtifact `
            -Client $Context.AzureDevOpsClient `
            -DownloadUrl $downloadUrl `
            -DestinationPath $zipPath

        if (-not $downloaded -or -not (Expand-BoundedArchive -ArchivePath $zipPath -DestinationPath $extractPath))
        {
            Remove-Item -LiteralPath $extractPath -Recurse -Force -ErrorAction SilentlyContinue
            $Context.ArtifactCache[$cacheKey] = $null
            return $null
        }
    }
    finally
    {
        Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    }

    $Context.ArtifactCache[$cacheKey] = $extractPath
    $extractPath
}

function Get-HostedScenarioEvidence
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)][string]$Scenario,
        [bool]$IncludeLog
    )

    $artifactName = "HostedPerfResults-$Os-$Scenario"
    $artifactDirectory = Get-ArtifactDirectory -Context $Context -BuildId $BuildId -ArtifactName $artifactName
    if ($null -eq $artifactDirectory)
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = 'Artifact was not available.'
        }
    }

    $files = @(Get-ChildItem -LiteralPath $artifactDirectory -Recurse -File)
    $metricsFile = $files | Where-Object Name -eq "$Scenario.metrics.txt" | Select-Object -First 1
    $logFile = $files | Where-Object Name -eq "$Scenario.log" | Select-Object -First 1
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
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)][string]$Scenario
    )

    $agentName = if ($Os -eq 'Windows') { 'GOLDWIN' } else { 'GOLDLIN' }
    $artifactName = "CrankAssets$agentName"
    $artifactDirectory = Get-ArtifactDirectory -Context $Context -BuildId $BuildId -ArtifactName $artifactName
    if ($null -eq $artifactDirectory)
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = 'Artifact was not available.'
        }
    }

    $resultFiles = @(
        Get-ChildItem -LiteralPath $artifactDirectory -Recurse -File |
            Where-Object Name -eq "$Scenario.json")
    # A diagnostic worker can publish multiple arms with identical scenario filenames.
    # Only an unambiguous clean result can describe the detector's clean measurement.
    $cleanFiles = @($resultFiles | Where-Object {
        $relativePath = [IO.Path]::GetRelativePath($artifactDirectory, $_.FullName)
        $relativePath -notmatch '(^|[\\/])(baseline|diagnostic)([\\/]|$)'
    })
    if ($cleanFiles.Count -ne 1)
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = 'A unique clean scenario result was not available.'
        }
    }

    if ($cleanFiles[0].Length -gt 16MB)
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = 'Scenario result exceeds the 16 MiB JSON-size limit.'
        }
    }

    $result = Get-Content -LiteralPath $cleanFiles[0].FullName -Raw | ConvertFrom-Json
    $results = Get-OptionalValue -InputObject $result -Path 'jobResults', 'jobs', 'application', 'results'
    if ($null -eq $results)
    {
        $results = Get-OptionalValue -InputObject $result -Path 'jobs', 'application', 'results'
    }
    $exitCode = Get-OptionalValue -InputObject $results -Path 'exit-code'
    $failure = [string](Get-OptionalValue -InputObject $results -Path 'failure-description')
    if ($null -eq $results -or [string]$exitCode -cne '0' -or -not [string]::IsNullOrWhiteSpace($failure))
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = 'The clean scenario result did not contain successful application metrics.'
        }
    }

    [pscustomobject][ordered]@{
        available = $true
        artifactName = $artifactName
        timestamp = [string](Get-OptionalValue -InputObject $result -Path 'timestamp')
        metrics = ConvertTo-AllowlistedMetrics -Properties $results.PSObject.Properties
        logExcerpt = ''
    }
}

function Get-ScenarioEvidence
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Backend,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)][string]$Scenario,
        [bool]$IncludeLog
    )

    if ($Backend -eq 'Hosted')
    {
        return Get-HostedScenarioEvidence `
            -Context $Context `
            -BuildId $BuildId `
            -Os $Os `
            -Scenario $Scenario `
            -IncludeLog $IncludeLog
    }

    Get-GoldScenarioEvidence -Context $Context -BuildId $BuildId -Os $Os -Scenario $Scenario
}

function Add-ActualRunProperties
{
    [OutputType([pscustomobject])]
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

function Get-ActualRunEvidenceCandidates
{
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)]$Report,
        [Parameter(Mandatory)]$AzureDevOpsClient,
        [Parameter(Mandatory)][string]$RawDirectory
    )

    New-Item -ItemType Directory -Force -Path $RawDirectory | Out-Null
    $context = New-ActualRunEvidenceContext -AzureDevOpsClient $AzureDevOpsClient -RawDirectory $RawDirectory

    try
    {
        @(
            foreach ($candidate in @($Report.candidates))
            {
                $currentRun = Get-PerfStarRunMetadata `
                    -AzureDevOpsClient $context.AzureDevOpsClient `
                    -RunCache $context.RunCache `
                    -BuildId ([string]$candidate.CurrentBuildId) `
                    -Backend ([string]$candidate.Backend)

                # Step 1 already excluded unusable runs; a violation here means the evidence input
                # and the detector report disagree, which must fail rather than silently diverge.
                if (-not (Test-PerfStarRunUsable -Run $currentRun))
                {
                    throw ("Current PerfStar run {0} for {1}/{2} '{3}' is not usable (state '{4}', result '{5}'). The detector report and this step disagree." -f `
                        $currentRun.perfStarBuildNumber,
                        $candidate.Backend,
                        $candidate.Os,
                        $candidate.ScenarioPair,
                        $currentRun.perfStarBuildState,
                        $currentRun.perfStarBuildResult)
                }

                $healthyRun = if (-not [string]::IsNullOrWhiteSpace([string]$candidate.HealthyBuildId))
                {
                    Get-PerfStarRunMetadata `
                        -AzureDevOpsClient $context.AzureDevOpsClient `
                        -RunCache $context.RunCache `
                        -BuildId ([string]$candidate.HealthyBuildId) `
                        -Backend ([string]$candidate.Backend)
                }
                else
                {
                    $null
                }

                $currentMtEvidence = Get-ScenarioEvidence `
                    -Context $context `
                    -Backend $candidate.Backend `
                    -BuildId $candidate.CurrentBuildId `
                    -Os $candidate.Os `
                    -Scenario $candidate.MtScenario `
                    -IncludeLog $true
                $currentNonMtEvidence = Get-ScenarioEvidence `
                    -Context $context `
                    -Backend $candidate.Backend `
                    -BuildId $candidate.CurrentBuildId `
                    -Os $candidate.Os `
                    -Scenario $candidate.NonMtScenario `
                    -IncludeLog $true
                $healthyMtEvidence = if ($null -ne $healthyRun)
                {
                    Get-ScenarioEvidence `
                        -Context $context `
                        -Backend $candidate.Backend `
                        -BuildId $candidate.HealthyBuildId `
                        -Os $candidate.Os `
                        -Scenario $candidate.MtScenario `
                        -IncludeLog $false
                }
                else
                {
                    $null
                }
                $healthyNonMtEvidence = if ($null -ne $healthyRun)
                {
                    Get-ScenarioEvidence `
                        -Context $context `
                        -Backend $candidate.Backend `
                        -BuildId $candidate.HealthyBuildId `
                        -Os $candidate.Os `
                        -Scenario $candidate.NonMtScenario `
                        -IncludeLog $false
                }
                else
                {
                    $null
                }

                Add-ActualRunProperties `
                    -Candidate $candidate `
                    -CurrentRun $currentRun `
                    -HealthyRun $healthyRun `
                    -CurrentMtEvidence $currentMtEvidence `
                    -CurrentNonMtEvidence $currentNonMtEvidence `
                    -HealthyMtEvidence $healthyMtEvidence `
                    -HealthyNonMtEvidence $healthyNonMtEvidence
            }
        )
    }
    finally
    {
        # Raw artifacts never cross into the agent job, even when evidence collection fails.
        Remove-Item -LiteralPath $RawDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Export-ModuleMember -Function 'Get-ActualRunEvidenceCandidates'
