# Copyright (c) Microsoft. All rights reserved.

Import-Module (Join-Path $PSScriptRoot '..\clients\AzureDevOpsClient.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\clients\KustoClient.psm1') -Force

function Get-RequiredSourceVersions
{
    [OutputType([System.Collections.Generic.HashSet[string]])]
    param([Parameter(Mandatory)]$Evidence)

    $versions = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in @($Evidence.candidates))
    {
        foreach ($run in @($candidate.currentRun, $candidate.healthyRun))
        {
            $sourceVersion = [string]$run.componentSourceVersion
            if ($sourceVersion -match '^[0-9a-fA-F]{40}$')
            {
                [void]$versions.Add($sourceVersion)
            }
        }
    }

    return ,$versions
}

function Get-CachedComponentBuild
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]$AzureDevOpsClient,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][hashtable]$Cache
    )

    if ($Cache.ContainsKey($BuildId))
    {
        return $Cache[$BuildId]
    }

    $build = Get-AzureDevOpsBuild -Client $AzureDevOpsClient -BuildId $BuildId
    $metadata = [pscustomobject][ordered]@{
        buildId = [string]$build.id
        buildNumber = [string]$build.buildNumber
        sourceVersion = [string]$build.sourceVersion
        sourceBranch = [string]$build.sourceBranch
        buildUrl = [string]$build._links.web.href
    }
    $Cache[$BuildId] = $metadata
    $metadata
}

function Find-DiagnosticRuns
{
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]$AzureDevOpsClient,
        [Parameter(Mandatory)][System.Collections.Generic.HashSet[string]]$RequiredSourceVersions,
        [Parameter(Mandatory)][int]$DiagnosticPipelineId,
        [Parameter(Mandatory)][int]$MaximumRunsToInspect
    )

    $matches = @{}
    foreach ($sourceVersion in $RequiredSourceVersions)
    {
        $matches[$sourceVersion] = [System.Collections.Generic.List[object]]::new()
    }

    $componentBuildCache = @{}
    $runs = Get-AzureDevOpsPipelineRuns -Client $AzureDevOpsClient -DefinitionId $DiagnosticPipelineId
    foreach ($runSummary in @(
        $runs.value |
            Sort-Object id -Descending |
            Select-Object -First $MaximumRunsToInspect))
    {
        if ([string]$runSummary.state -ne 'completed')
        {
            continue
        }

        $run = Get-AzureDevOpsPipelineRun `
            -Client $AzureDevOpsClient `
            -DefinitionId $DiagnosticPipelineId `
            -BuildId ([string]$runSummary.id)
        $component = $run.resources.pipelines.ComponentBuildUnderTest
        if ($null -eq $component -or $null -eq $component.pipeline.id)
        {
            continue
        }

        $componentBuild = Get-CachedComponentBuild `
            -AzureDevOpsClient $AzureDevOpsClient `
            -BuildId ([string]$component.pipeline.id) `
            -Cache $componentBuildCache
        if (-not $RequiredSourceVersions.Contains($componentBuild.sourceVersion))
        {
            continue
        }

        $matches[$componentBuild.sourceVersion].Add(
            [pscustomobject][ordered]@{
                diagnosticBuildId = [string]$run.id
                diagnosticBuildNumber = [string]$run.name
                diagnosticResult = [string]$run.result
                diagnosticBuildUrl = [string]$run._links.web.href
                componentBuildId = $componentBuild.buildId
                componentBuildNumber = $componentBuild.buildNumber
                componentSourceVersion = $componentBuild.sourceVersion
                componentBuildUrl = $componentBuild.buildUrl
            })
    }

    $matches
}

function Test-SafeKustoDimension
{
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Name
    )

    $isSafe = $Value -match '^[A-Za-z0-9_.-]+$'
    if (-not $isSafe)
    {
        Write-Warning "Skipping diagnostic evidence because $Name '$Value' is not safe for Kusto interpolation."
    }

    $isSafe
}

function Get-DiagnosticRows
{
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)]$KustoClient,
        [Parameter(Mandatory)][string]$DiagnosticBuildId,
        [Parameter(Mandatory)][string]$ScenarioPair,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)][hashtable]$Cache
    )

    if (-not (Test-SafeKustoDimension -Value $DiagnosticBuildId -Name 'diagnostic build ID') -or
        -not (Test-SafeKustoDimension -Value $ScenarioPair -Name 'scenario pair') -or
        -not (Test-SafeKustoDimension -Value $Os -Name 'OS'))
    {
        return @()
    }

    $cacheKey = "$DiagnosticBuildId/$Os/$ScenarioPair"
    if ($Cache.ContainsKey($cacheKey))
    {
        return $Cache[$cacheKey]
    }

    # Values are interpolated only after the strict dimension validation above.
    $query = @"
let runSuffix = "-$DiagnosticBuildId";
let scenarioName = "$ScenarioPair";
let osName = "$Os";
let taskMigrationData =
    task_inventory
    | where run_key endswith runSuffix and scenario_pair == scenarioName and os == osName
    | extend short_task_name = tostring(split(task_full_name, ".")[-1])
    | summarize
        migrated = take_any(marker_attribute),
        marker_interface = take_any(marker_interface),
        task_full_name = take_any(task_full_name)
      by short_task_name;
let taskEvidence =
    task_wallclock()
    | where run_key endswith runSuffix and scenario_pair == scenarioName and os == osName
    | where not(task_name startswith "Message:")
    | summarize
        non_mt_ms = sumif(total_ms, not(mt_flag)),
        mt_ms = sumif(total_ms, mt_flag),
        non_mt_instances = sumif(instances, not(mt_flag)),
        mt_instances = sumif(instances, mt_flag)
      by task_name
    | extend
        delta_ms = mt_ms - non_mt_ms,
        absolute_delta_ms = abs(mt_ms - non_mt_ms)
    | join kind=leftouter taskMigrationData on `$left.task_name == `$right.short_task_name
    | extend migration_state = case(
        isempty(task_full_name), "unknown",
        migrated == true, "migrated",
        marker_interface == true, "interface-only",
        "not-migrated")
    | project
        evidence_kind = "task",
        item_name = task_name,
        item_full_name = task_full_name,
        non_mt_ms,
        mt_ms,
        delta_ms,
        absolute_delta_ms,
        migration_state;
let targetEvidence =
    target_wallclock()
    | where run_key endswith runSuffix and scenario_pair == scenarioName and os == osName
    | summarize
        non_mt_ms = sumif(total_ms, not(mt_flag)),
        mt_ms = sumif(total_ms, mt_flag)
      by target_name
    | extend
        delta_ms = mt_ms - non_mt_ms,
        absolute_delta_ms = abs(mt_ms - non_mt_ms)
    | project
        evidence_kind = "target",
        item_name = target_name,
        item_full_name = "",
        non_mt_ms,
        mt_ms,
        delta_ms,
        absolute_delta_ms,
        migration_state = "";
let evaluationEvidence =
    eval_pass()
    | where run_key endswith runSuffix and scenario_pair == scenarioName and os == osName
    | summarize
        non_mt_ms = sumif(total_ms, not(mt_flag)),
        mt_ms = sumif(total_ms, mt_flag)
      by pass_name
    | extend
        delta_ms = mt_ms - non_mt_ms,
        absolute_delta_ms = abs(mt_ms - non_mt_ms)
    | project
        evidence_kind = "evaluation",
        item_name = pass_name,
        item_full_name = "",
        non_mt_ms,
        mt_ms,
        delta_ms,
        absolute_delta_ms,
        migration_state = "";
union taskEvidence, targetEvidence, evaluationEvidence
| sort by evidence_kind asc, absolute_delta_ms desc
"@

    $rows = @(Invoke-KustoQuery -Client $KustoClient -Query $query -MaximumAttempts 4)
    $Cache[$cacheKey] = $rows
    $rows
}

function Select-DiagnosticEvidence
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string]$SourceVersion,
        [Parameter(Mandatory)][string]$ScenarioPair,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)]$DiagnosticRuns,
        [Parameter(Mandatory)]$KustoClient,
        [Parameter(Mandatory)][hashtable]$QueryCache
    )

    if ([string]::IsNullOrWhiteSpace($SourceVersion) -or -not $DiagnosticRuns.ContainsKey($SourceVersion))
    {
        return $null
    }

    foreach ($run in $DiagnosticRuns[$SourceVersion])
    {
        $rows = @(Get-DiagnosticRows `
            -KustoClient $KustoClient `
            -DiagnosticBuildId $run.diagnosticBuildId `
            -ScenarioPair $ScenarioPair `
            -Os $Os `
            -Cache $QueryCache)
        if ($rows.Count -eq 0)
        {
            continue
        }

        $taskRows = @($rows | Where-Object evidence_kind -eq 'task')
        $hasPairedTaskData = @(
            $taskRows |
                Where-Object { [double]$_.non_mt_ms -gt 0 -and [double]$_.mt_ms -gt 0 }).Count -gt 0
        if (-not $hasPairedTaskData)
        {
            continue
        }

        $targetRows = @($rows | Where-Object evidence_kind -eq 'target')
        $evaluationRows = @($rows | Where-Object evidence_kind -eq 'evaluation')
        return [pscustomobject][ordered]@{
            available = $true
            diagnosticBuildId = $run.diagnosticBuildId
            diagnosticBuildNumber = $run.diagnosticBuildNumber
            diagnosticResult = $run.diagnosticResult
            diagnosticBuildUrl = $run.diagnosticBuildUrl
            componentBuildId = $run.componentBuildId
            componentBuildNumber = $run.componentBuildNumber
            componentSourceVersion = $run.componentSourceVersion
            relationship = ''
            topTaskDeltas = @($taskRows | Sort-Object absolute_delta_ms -Descending | Select-Object -First 15)
            migratedTaskControls = @(
                $taskRows |
                    Where-Object migration_state -eq 'migrated' |
                    Sort-Object absolute_delta_ms -Descending |
                    Select-Object -First 5)
            topTargetDeltas = @($targetRows | Sort-Object absolute_delta_ms -Descending | Select-Object -First 12)
            evaluationPassDeltas = @($evaluationRows | Sort-Object absolute_delta_ms -Descending)
        }
    }

    $null
}

function Add-DiagnosticProperties
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)]$Candidate,
        $CurrentDiagnostic,
        $HealthyDiagnostic
    )

    $properties = [ordered]@{}
    foreach ($property in $Candidate.PSObject.Properties)
    {
        $properties[$property.Name] = $property.Value
    }

    $relationship = if ($Candidate.Backend -eq 'Hosted') { 'direct-hosted' } else { 'hosted-corroboration-for-gold' }
    if ($null -ne $CurrentDiagnostic)
    {
        $CurrentDiagnostic.relationship = $relationship
    }
    if ($null -ne $HealthyDiagnostic)
    {
        $HealthyDiagnostic.relationship = $relationship
    }

    $properties.currentDiagnostic = $CurrentDiagnostic
    $properties.healthyDiagnostic = $HealthyDiagnostic
    [pscustomobject]$properties
}

function Get-DiagnosticEvidenceCandidates
{
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)]$Evidence,
        [Parameter(Mandatory)]$AzureDevOpsClient,
        [Parameter(Mandatory)]$KustoClient,
        [Parameter(Mandatory)][int]$DiagnosticPipelineId,
        [Parameter(Mandatory)][int]$MaximumRunsToInspect
    )

    $requiredSourceVersions = Get-RequiredSourceVersions -Evidence $Evidence
    $diagnosticRuns = Find-DiagnosticRuns `
        -AzureDevOpsClient $AzureDevOpsClient `
        -RequiredSourceVersions $requiredSourceVersions `
        -DiagnosticPipelineId $DiagnosticPipelineId `
        -MaximumRunsToInspect $MaximumRunsToInspect
    $queryCache = @{}

    @(
        foreach ($candidate in @($Evidence.candidates))
        {
            $currentDiagnostic = Select-DiagnosticEvidence `
                -SourceVersion ([string]$candidate.currentRun.componentSourceVersion) `
                -ScenarioPair ([string]$candidate.ScenarioPair) `
                -Os ([string]$candidate.Os) `
                -DiagnosticRuns $diagnosticRuns `
                -KustoClient $KustoClient `
                -QueryCache $queryCache
            $healthyDiagnostic = if ($null -ne $candidate.healthyRun)
            {
                Select-DiagnosticEvidence `
                    -SourceVersion ([string]$candidate.healthyRun.componentSourceVersion) `
                    -ScenarioPair ([string]$candidate.ScenarioPair) `
                    -Os ([string]$candidate.Os) `
                    -DiagnosticRuns $diagnosticRuns `
                    -KustoClient $KustoClient `
                    -QueryCache $queryCache
            }
            else
            {
                $null
            }

            Add-DiagnosticProperties `
                -Candidate $candidate `
                -CurrentDiagnostic $currentDiagnostic `
                -HealthyDiagnostic $healthyDiagnostic
        }
    )
}

Export-ModuleMember -Function @(
    'Test-SafeKustoDimension',
    'Get-RequiredSourceVersions',
    'Get-DiagnosticEvidenceCandidates'
)
