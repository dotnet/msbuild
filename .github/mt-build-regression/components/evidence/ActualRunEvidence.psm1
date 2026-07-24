# Copyright (c) Microsoft. All rights reserved.

Import-Module (Join-Path $PSScriptRoot '..\clients\AzureDevOpsClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'EvidenceSanitizer.psm1') -Force

function Get-PerfStarPipelineDefinitionId
{
    [OutputType([int])]
    param([Parameter(Mandatory)][string]$Backend)

    switch ($Backend)
    {
        'Gold' { return 25429 }
        'Hosted' { return 28338 }
        default { throw "Unsupported PerfStar backend '$Backend'." }
    }
}

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
        RunCache = @{}
        ArtifactCache = @{}
    }
}

function Get-PerfStarRunMetadata
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Backend
    )

    $cacheKey = "$Backend/$BuildId"
    if ($Context.RunCache.ContainsKey($cacheKey))
    {
        return $Context.RunCache[$cacheKey]
    }

    $definitionId = Get-PerfStarPipelineDefinitionId -Backend $Backend
    $run = Get-AzureDevOpsPipelineRun -Client $Context.AzureDevOpsClient -DefinitionId $definitionId -BuildId $BuildId
    $component = $run.resources.pipelines.ComponentBuildUnderTest

    $componentBuild = $null
    if ($null -ne $component -and $null -ne $component.pipeline.id)
    {
        $componentBuild = Get-AzureDevOpsBuild -Client $Context.AzureDevOpsClient -BuildId ([string]$component.pipeline.id)
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

    $Context.RunCache[$cacheKey] = $metadata
    $metadata
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
    $zipPath = Join-Path $Context.RawDirectory "$safeName.zip"
    $extractPath = Join-Path $Context.RawDirectory $safeName
    New-Item -ItemType Directory -Force -Path $extractPath | Out-Null

    Save-AzureDevOpsArtifact `
        -Client $Context.AzureDevOpsClient `
        -DownloadUrl $downloadUrl `
        -DestinationPath $zipPath

    $zipLength = (Get-Item -LiteralPath $zipPath).Length
    if ($zipLength -gt 100MB)
    {
        throw "Artifact '$ArtifactName' from build $BuildId is $([Math]::Round($zipLength / 1MB, 1)) MB; refusing to extract more than 100 MB."
    }

    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force
    Remove-Item -LiteralPath $zipPath -Force
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
    $artifactName = "CrankAssetsThinned$agentName"
    $artifactDirectory = Get-ArtifactDirectory -Context $Context -BuildId $BuildId -ArtifactName $artifactName
    if ($null -eq $artifactDirectory)
    {
        return [pscustomobject][ordered]@{
            available = $false
            artifactName = $artifactName
            message = 'Artifact was not available.'
        }
    }

    $resultFile = Get-ChildItem -LiteralPath $artifactDirectory -Recurse -File |
        Where-Object Name -eq "$Scenario.json" |
        Select-Object -First 1
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
        metrics = ConvertTo-AllowlistedMetrics -Properties $result.results.PSObject.Properties
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
                    -Context $context `
                    -BuildId ([string]$candidate.CurrentBuildId) `
                    -Backend ([string]$candidate.Backend)
                $healthyRun = if (-not [string]::IsNullOrWhiteSpace([string]$candidate.HealthyBuildId))
                {
                    Get-PerfStarRunMetadata `
                        -Context $context `
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
