# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot '..\clients\AzureDevOpsClient.psm1')

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

function New-PerfStarRunCache
{
    <#
    .SYNOPSIS
    Creates the per-invocation cache used to resolve each PerfStar run at most once.
    #>
    [OutputType([hashtable])]
    param()

    @{}
}

function Get-OptionalValue
{
    <#
    .SYNOPSIS
    Reads a nested property path from a deserialized REST payload, or $null when any step is absent.

    .DESCRIPTION
    Azure DevOps omits properties rather than nulling them: an in-progress run has no 'result', and a
    run without a component resource has no 'resources.pipelines'. This module runs under StrictMode,
    where direct dotted access to a missing property throws, so optional fields are read through the
    property collection instead.
    #>
    param(
        $InputObject,
        [Parameter(Mandatory)][string[]]$Path
    )

    $current = $InputObject
    foreach ($name in $Path)
    {
        if ($null -eq $current)
        {
            return $null
        }

        $property = $current.PSObject.Properties[$name]
        if ($null -eq $property)
        {
            return $null
        }

        $current = $property.Value
    }

    $current
}

function Get-PerfStarRunMetadata
{
    <#
    .SYNOPSIS
    Resolves a PerfStar run and the MSBuild component build it consumed.

    .DESCRIPTION
    Candidates in a single scan usually share one PerfStar build, so results are cached by
    backend and build id to avoid repeating identical Azure DevOps requests.
    #>
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]$AzureDevOpsClient,
        [Parameter(Mandatory)][hashtable]$RunCache,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$Backend
    )

    $cacheKey = "$Backend/$BuildId"
    if ($RunCache.ContainsKey($cacheKey))
    {
        return $RunCache[$cacheKey]
    }

    $definitionId = Get-PerfStarPipelineDefinitionId -Backend $Backend
    $run = Get-AzureDevOpsPipelineRun -Client $AzureDevOpsClient -DefinitionId $definitionId -BuildId $BuildId
    $component = Get-OptionalValue -InputObject $run -Path 'resources', 'pipelines', 'ComponentBuildUnderTest'
    $componentPipelineId = Get-OptionalValue -InputObject $component -Path 'pipeline', 'id'

    $componentBuild = $null
    if ($null -ne $componentPipelineId)
    {
        $componentBuild = Get-AzureDevOpsBuild -Client $AzureDevOpsClient -BuildId ([string]$componentPipelineId)
    }

    $metadata = [pscustomobject][ordered]@{
        perfStarBuildId = [string](Get-OptionalValue -InputObject $run -Path 'id')
        perfStarBuildNumber = [string](Get-OptionalValue -InputObject $run -Path 'name')
        perfStarBuildState = [string](Get-OptionalValue -InputObject $run -Path 'state')
        perfStarBuildResult = [string](Get-OptionalValue -InputObject $run -Path 'result')
        perfStarBuildUrl = [string](Get-OptionalValue -InputObject $run -Path '_links', 'web', 'href')
        componentBuildId = [string](Get-OptionalValue -InputObject $componentBuild -Path 'id')
        componentBuildNumber = if ($null -ne $componentBuild)
        {
            [string](Get-OptionalValue -InputObject $componentBuild -Path 'buildNumber')
        }
        else
        {
            [string](Get-OptionalValue -InputObject $component -Path 'version')
        }
        componentSourceBranch = [string](Get-OptionalValue -InputObject $componentBuild -Path 'sourceBranch')
        componentSourceVersion = [string](Get-OptionalValue -InputObject $componentBuild -Path 'sourceVersion')
        componentBuildResult = [string](Get-OptionalValue -InputObject $componentBuild -Path 'result')
        componentBuildUrl = [string](Get-OptionalValue -InputObject $componentBuild -Path '_links', 'web', 'href')
    }

    $RunCache[$cacheKey] = $metadata
    $metadata
}

function Test-PerfStarRunUsable
{
    <#
    .SYNOPSIS
    Indicates whether a PerfStar run's measurements describe a whole, finished run.

    .DESCRIPTION
    PerfStarDataRaw is ingested while a run executes, so the detector's arg_max(RunTimestamp) can
    select a build that has not finished. Such a build reports only part of its scenarios, and those
    scenarios are measured while the remaining ones still compete for the same machine, which biases
    them against baselines that ran to completion.

    Canceled runs are rejected for the same reason: they reach state 'completed' but stop partway,
    so their scenario coverage is truncated or duplicated by retries.

    Run-level failure is not rejected, because the scenarios that did report may still be complete
    and 'failed' is the most common outcome for these pipelines. Unknown and missing values are
    denied by default.
    #>
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]$Run
    )

    $state = $Run.PSObject.Properties['perfStarBuildState']
    $result = $Run.PSObject.Properties['perfStarBuildResult']
    if ($null -eq $state -or $null -eq $result)
    {
        return $false
    }

    [string]$state.Value -eq 'completed' -and [string]$result.Value -in @('succeeded', 'failed')
}

Export-ModuleMember -Function @(
    'Get-PerfStarPipelineDefinitionId',
    'New-PerfStarRunCache',
    'Get-PerfStarRunMetadata',
    'Test-PerfStarRunUsable'
)
