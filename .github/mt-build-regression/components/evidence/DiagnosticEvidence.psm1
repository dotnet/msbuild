# Copyright (c) Microsoft. All rights reserved.

Import-Module (Join-Path $PSScriptRoot '..\clients\AzureDevOpsClient.psm1')
Import-Module (Join-Path $PSScriptRoot '..\clients\KustoClient.psm1')
Import-Module (Join-Path $PSScriptRoot 'RunSelection.psm1')

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

function Find-DiagnosticRuns
{
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]$AzureDevOpsClient,
        [Parameter(Mandatory)]$KustoClient,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.HashSet[string]]$RequiredSourceVersions,
        [Parameter(Mandatory)][int]$DiagnosticPipelineId,
        [Parameter(Mandatory)][ValidateRange(1, 100)][int]$MaximumRunsToInspect
    )

    $runsBySource = @{}
    foreach ($sourceVersion in $RequiredSourceVersions)
    {
        $runsBySource[$sourceVersion] = [System.Collections.Generic.List[object]]::new()
    }

    if ($RequiredSourceVersions.Count -eq 0)
    {
        return $runsBySource
    }

    # GOLD source_version is the PerfStar revision, not the measured MSBuild revision.
    # Collapse partition-level metadata before resolving the selected MSBuild build through AzDO.
    $query = @"
gold_runs()
| where ingested_at >= ago(21d)
| where source_branch == "refs/heads/main" and msbuild_branch == "refs/heads/main"
| where run_kind in ("GoldDiagnostic", "GoldPairedDiagnostic")
| summarize last_ingested_at = max(ingested_at) by run_key, build_id
| order by last_ingested_at desc, build_id desc, run_key asc
| take $MaximumRunsToInspect
"@
    $runCache = New-PerfStarRunCache
    $runs = @(Invoke-KustoQuery -Client $KustoClient -Query $query -MaximumAttempts 4 -RequirePrimaryTable $true)
    foreach ($runSummary in $runs)
    {
        if ([string]$runSummary.build_id -notmatch '^[1-9][0-9]*\z')
        {
            Write-Warning 'Skipping a GOLD diagnostic with an invalid build ID.'
            continue
        }

        $run = Get-PerfStarRunMetadata `
            -AzureDevOpsClient $AzureDevOpsClient `
            -RunCache $runCache `
            -DefinitionId $DiagnosticPipelineId `
            -BuildId ([string]$runSummary.build_id) `
            -Backend 'Gold'
        if (-not (Test-PerfStarRunUsable -Run $run))
        {
            continue
        }

        if (-not $RequiredSourceVersions.Contains($run.componentSourceVersion))
        {
            continue
        }

        $runsBySource[$run.componentSourceVersion].Add(
            [pscustomobject][ordered]@{
                diagnosticBuildId = $run.perfStarBuildId
                diagnosticBuildNumber = $run.perfStarBuildNumber
                diagnosticResult = $run.perfStarBuildResult
                diagnosticBuildUrl = $run.perfStarBuildUrl
                diagnosticRunKey = [string]$runSummary.run_key
                componentBuildId = $run.componentBuildId
                componentBuildNumber = $run.componentBuildNumber
                componentSourceVersion = $run.componentSourceVersion
                componentBuildUrl = $run.componentBuildUrl
            })
    }

    $runsBySource
}

function Test-SafeKustoDimension
{
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string]$Value,
        [Parameter(Mandatory)][string]$Name
    )

    $isSafe = $Value -match '^[A-Za-z0-9_.-]+\z'
    if (-not $isSafe)
    {
        Write-Warning "Skipping diagnostic evidence because $Name contains characters that are not safe for Kusto interpolation."
    }

    $isSafe
}

function Get-DiagnosticRows
{
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)]$KustoClient,
        [Parameter(Mandatory)][string]$DiagnosticRunKey,
        [Parameter(Mandatory)][string]$ScenarioPair,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)][hashtable]$Cache
    )

    if (-not (Test-SafeKustoDimension -Value $DiagnosticRunKey -Name 'diagnostic run key') -or
        -not (Test-SafeKustoDimension -Value $ScenarioPair -Name 'scenario pair') -or
        -not (Test-SafeKustoDimension -Value $Os -Name 'OS'))
    {
        return @()
    }

    $cacheKey = "$DiagnosticRunKey/$Os/$ScenarioPair"
    if ($Cache.ContainsKey($cacheKey))
    {
        return $Cache[$cacheKey]
    }

    # Values are interpolated only after the strict dimension validation above.
    $query = @"
let runKey = "$DiagnosticRunKey";
let scenarioName = "$ScenarioPair";
let osName = "$Os";
let partitions =
    gold_runs()
    | where run_key == runKey and scenario_pair == scenarioName and os == osName
    | where source_branch == "refs/heads/main" and msbuild_branch == "refs/heads/main"
    | where run_kind in ("GoldDiagnostic", "GoldPairedDiagnostic");
let paired =
    partitions
    | summarize modes = make_set(mt_flag), sdk_count = dcount(sdk_sha256),
        version_count = dcount(msbuild_version), iteration_count = dcount(expected_iterations)
      by run_key, scenario_pair, os
    | where array_length(modes) == 2 and sdk_count == 1 and version_count == 1 and iteration_count == 1;
let publications =
    partitions
    | join kind=leftsemi paired on run_key, scenario_pair, os
    | project publication_id;
let taskMigrationData =
    gold_task_inventory()
    | where publication_id in (publications)
    | extend short_task_name = tostring(split(task_full_name, ".")[-1])
    | summarize
        migrated = take_any(marker_attribute),
        marker_interface = take_any(marker_interface),
        task_full_name = take_any(task_full_name)
      by short_task_name;
let taskEvidence =
    gold_task_runs()
    | where publication_id in (publications)
    | where not(task_name startswith "Message:")
    | summarize
        non_mt_ms = sumif(duration_ms, not(mt_flag)),
        mt_ms = sumif(duration_ms, mt_flag)
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
    gold_target_runs()
    | where publication_id in (publications)
    | summarize
        non_mt_ms = sumif(duration_ms, not(mt_flag)),
        mt_ms = sumif(duration_ms, mt_flag)
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
    gold_project_evaluations()
    | where publication_id in (publications)
    | summarize
        non_mt_ms = sumif(duration_ms, not(mt_flag)),
        mt_ms = sumif(duration_ms, mt_flag)
      by pass_name = pass
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
        [Parameter(Mandatory)][AllowEmptyString()][string]$SourceVersion,
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
            -DiagnosticRunKey $run.diagnosticRunKey `
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
            diagnosticRunKey = $run.diagnosticRunKey
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

    $relationship = if ($Candidate.Backend -eq 'Gold') { 'same-backend-gold' } else { 'gold-corroboration-for-hosted' }
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
        -KustoClient $KustoClient `
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
