# Copyright (c) Microsoft. All rights reserved.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InputEvidence,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [string]$OrganizationUri = 'https://dev.azure.com/devdiv',

    [string]$Project = 'DevDiv',

    [string]$ClusterUri = 'https://perfstar-experimental.swedencentral.kusto.windows.net',

    [string]$Database = 'perfstar-dev',

    [int]$DiagnosticPipelineId = 28394,

    [int]$MaximumRunsToInspect = 24
)

$ErrorActionPreference = 'Stop'

$azdoAccessToken = $env:AZDO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($azdoAccessToken))
{
    throw 'AZDO_ACCESS_TOKEN is required.'
}

$kustoAccessToken = $env:KUSTO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($kustoAccessToken))
{
    throw 'KUSTO_ACCESS_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $InputEvidence))
{
    throw "Actual-run evidence not found: $InputEvidence"
}

$projectBaseUri = "$($OrganizationUri.TrimEnd('/'))/$([Uri]::EscapeDataString($Project))"
$azdoHeaders = @{ Authorization = "Bearer $azdoAccessToken" }
$kustoHeaders = @{ Authorization = "Bearer $kustoAccessToken" }
$componentBuildCache = @{}
$diagnosticQueryCache = @{}

function Invoke-AzdoJson
{
    param([Parameter(Mandatory)][string]$Uri)

    for ($attempt = 1; $attempt -le 4; $attempt++)
    {
        try
        {
            return Invoke-RestMethod -Method Get -Uri $Uri -Headers $azdoHeaders
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

function Invoke-KustoQuery
{
    param([Parameter(Mandatory)][string]$Query)

    $payload = @{
        db = $Database
        csl = $Query
    } | ConvertTo-Json -Compress

    $response = $null
    for ($attempt = 1; $attempt -le 4; $attempt++)
    {
        try
        {
            $response = Invoke-RestMethod `
                -Method Post `
                -Uri "$($ClusterUri.TrimEnd('/'))/v1/rest/query" `
                -Headers $kustoHeaders `
                -ContentType 'application/json' `
                -Body $payload
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

    $table = $response.Tables | Select-Object -First 1
    if ($null -eq $table)
    {
        return @()
    }

    $columnNames = @($table.Columns | ForEach-Object { $_.ColumnName })
    @(
        foreach ($row in $table.Rows)
        {
            $record = [ordered]@{}
            for ($index = 0; $index -lt $columnNames.Count; $index++)
            {
                $record[$columnNames[$index]] = $row[$index]
            }

            [pscustomobject]$record
        }
    )
}

function Get-ComponentBuild
{
    param([Parameter(Mandatory)][string]$BuildId)

    if ($componentBuildCache.ContainsKey($BuildId))
    {
        return $componentBuildCache[$BuildId]
    }

    $buildUri = "$projectBaseUri/_apis/build/builds/$([Uri]::EscapeDataString($BuildId))?api-version=7.1"
    $build = Invoke-AzdoJson -Uri $buildUri
    $metadata = [pscustomobject][ordered]@{
        buildId = [string]$build.id
        buildNumber = [string]$build.buildNumber
        sourceVersion = [string]$build.sourceVersion
        sourceBranch = [string]$build.sourceBranch
        buildUrl = [string]$build._links.web.href
    }
    $componentBuildCache[$BuildId] = $metadata
    $metadata
}

function Get-RequiredSourceVersions
{
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

    $versions
}

function Find-DiagnosticRuns
{
    param([Parameter(Mandatory)][System.Collections.Generic.HashSet[string]]$RequiredSourceVersions)

    $matches = @{}
    foreach ($sourceVersion in $RequiredSourceVersions)
    {
        $matches[$sourceVersion] = [System.Collections.Generic.List[object]]::new()
    }

    $runsUri = "$projectBaseUri/_apis/pipelines/$DiagnosticPipelineId/runs?api-version=7.1"
    $runs = Invoke-AzdoJson -Uri $runsUri
    foreach ($runSummary in @(
        $runs.value |
            Sort-Object id -Descending |
            Select-Object -First $MaximumRunsToInspect))
    {
        if ([string]$runSummary.state -ne 'completed')
        {
            continue
        }

        $runUri = "$projectBaseUri/_apis/pipelines/$DiagnosticPipelineId/runs/$($runSummary.id)?api-version=7.1"
        $run = Invoke-AzdoJson -Uri $runUri
        $component = $run.resources.pipelines.ComponentBuildUnderTest
        if ($null -eq $component -or $null -eq $component.pipeline.id)
        {
            continue
        }

        $componentBuild = Get-ComponentBuild -BuildId ([string]$component.pipeline.id)
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

function Test-SafeDimension
{
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
    param(
        [Parameter(Mandatory)][string]$DiagnosticBuildId,
        [Parameter(Mandatory)][string]$ScenarioPair,
        [Parameter(Mandatory)][string]$Os
    )

    if (-not (Test-SafeDimension -Value $DiagnosticBuildId -Name 'diagnostic build ID') -or
        -not (Test-SafeDimension -Value $ScenarioPair -Name 'scenario pair') -or
        -not (Test-SafeDimension -Value $Os -Name 'OS'))
    {
        return @()
    }

    $cacheKey = "$DiagnosticBuildId/$Os/$ScenarioPair"
    if ($diagnosticQueryCache.ContainsKey($cacheKey))
    {
        return $diagnosticQueryCache[$cacheKey]
    }

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

    $rows = @(Invoke-KustoQuery -Query $query)
    $diagnosticQueryCache[$cacheKey] = $rows
    $rows
}

function Select-DiagnosticEvidence
{
    param(
        [Parameter(Mandatory)][string]$SourceVersion,
        [Parameter(Mandatory)][string]$ScenarioPair,
        [Parameter(Mandatory)][string]$Os,
        [Parameter(Mandatory)]$DiagnosticRuns
    )

    if ([string]::IsNullOrWhiteSpace($SourceVersion) -or -not $DiagnosticRuns.ContainsKey($SourceVersion))
    {
        return $null
    }

    foreach ($run in $DiagnosticRuns[$SourceVersion])
    {
        $rows = @(Get-DiagnosticRows -DiagnosticBuildId $run.diagnosticBuildId -ScenarioPair $ScenarioPair -Os $Os)
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
        $topTasks = @($taskRows | Sort-Object absolute_delta_ms -Descending | Select-Object -First 15)
        $migratedControls = @(
            $taskRows |
                Where-Object migration_state -eq 'migrated' |
                Sort-Object absolute_delta_ms -Descending |
                Select-Object -First 5)

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
            topTaskDeltas = $topTasks
            migratedTaskControls = $migratedControls
            topTargetDeltas = @($targetRows | Sort-Object absolute_delta_ms -Descending | Select-Object -First 12)
            evaluationPassDeltas = @($evaluationRows | Sort-Object absolute_delta_ms -Descending)
        }
    }

    $null
}

function Add-DiagnosticProperties
{
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

    if ($null -ne $CurrentDiagnostic)
    {
        $CurrentDiagnostic.relationship = if ($Candidate.Backend -eq 'Hosted') { 'direct-hosted' } else { 'hosted-corroboration-for-gold' }
    }
    if ($null -ne $HealthyDiagnostic)
    {
        $HealthyDiagnostic.relationship = if ($Candidate.Backend -eq 'Hosted') { 'direct-hosted' } else { 'hosted-corroboration-for-gold' }
    }

    $properties.currentDiagnostic = $CurrentDiagnostic
    $properties.healthyDiagnostic = $HealthyDiagnostic
    [pscustomobject]$properties
}

function Add-DeltaTable
{
    param(
        [Parameter(Mandatory)][System.Text.StringBuilder]$Builder,
        [Parameter(Mandatory)][string]$Heading,
        [Parameter(Mandatory)]$Rows,
        [bool]$IncludeMigrationState
    )

    if (@($Rows).Count -eq 0)
    {
        return
    }

    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine("### $Heading")
    [void]$Builder.AppendLine()
    if ($IncludeMigrationState)
    {
        [void]$Builder.AppendLine('| Item | non-MT ms | MT ms | Delta | Migration state |')
        [void]$Builder.AppendLine('| --- | ---: | ---: | ---: | --- |')
    }
    else
    {
        [void]$Builder.AppendLine('| Item | non-MT ms | MT ms | Delta |')
        [void]$Builder.AppendLine('| --- | ---: | ---: | ---: |')
    }

    foreach ($row in @($Rows))
    {
        $itemName = ([string]$row.item_name).Replace('|', '\|')
        $nonMt = [Math]::Round([double]$row.non_mt_ms, 1)
        $mt = [Math]::Round([double]$row.mt_ms, 1)
        $delta = [Math]::Round([double]$row.delta_ms, 1)
        if ($IncludeMigrationState)
        {
            [void]$Builder.AppendLine("| ``$itemName`` | $nonMt | $mt | $delta | $($row.migration_state) |")
        }
        else
        {
            [void]$Builder.AppendLine("| ``$itemName`` | $nonMt | $mt | $delta |")
        }
    }
}

function Add-DiagnosticSection
{
    param(
        [Parameter(Mandatory)][System.Text.StringBuilder]$Builder,
        [Parameter(Mandatory)][string]$Heading,
        $Diagnostic
    )

    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine("### $Heading")
    [void]$Builder.AppendLine()
    if ($null -eq $Diagnostic)
    {
        [void]$Builder.AppendLine('No exact-source diagnostic run with task-level Kusto data was found.')
        return
    }

    [void]$Builder.AppendLine("- Diagnostic run: [$($Diagnostic.diagnosticBuildNumber)]($($Diagnostic.diagnosticBuildUrl))")
    [void]$Builder.AppendLine("- Result: ``$($Diagnostic.diagnosticResult)`` (partial scenario data is accepted)")
    [void]$Builder.AppendLine("- Relationship: ``$($Diagnostic.relationship)``")
    [void]$Builder.AppendLine("- MSBuild source: ``$($Diagnostic.componentSourceVersion)``")
    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine('Task and target durations are aggregated across instances and may include nested/inclusive work. Positive deltas must be compared with migrated controls before attribution.')

    Add-DeltaTable -Builder $Builder -Heading 'Largest task deltas' -Rows $Diagnostic.topTaskDeltas -IncludeMigrationState $true
    Add-DeltaTable -Builder $Builder -Heading 'Migrated task controls (contention/noise floor)' -Rows $Diagnostic.migratedTaskControls -IncludeMigrationState $true
    Add-DeltaTable -Builder $Builder -Heading 'Largest target deltas' -Rows $Diagnostic.topTargetDeltas -IncludeMigrationState $false
    Add-DeltaTable -Builder $Builder -Heading 'Evaluation-pass deltas' -Rows $Diagnostic.evaluationPassDeltas -IncludeMigrationState $false
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$evidence = Get-Content -LiteralPath $InputEvidence -Raw | ConvertFrom-Json
$requiredSourceVersions = Get-RequiredSourceVersions -Evidence $evidence
$diagnosticRuns = Find-DiagnosticRuns -RequiredSourceVersions $requiredSourceVersions

$enrichedCandidates = @(
    foreach ($candidate in @($evidence.candidates))
    {
        $currentDiagnostic = Select-DiagnosticEvidence `
            -SourceVersion ([string]$candidate.currentRun.componentSourceVersion) `
            -ScenarioPair ([string]$candidate.ScenarioPair) `
            -Os ([string]$candidate.Os) `
            -DiagnosticRuns $diagnosticRuns
        $healthyDiagnostic = if ($null -ne $candidate.healthyRun)
        {
            Select-DiagnosticEvidence `
                -SourceVersion ([string]$candidate.healthyRun.componentSourceVersion) `
                -ScenarioPair ([string]$candidate.ScenarioPair) `
                -Os ([string]$candidate.Os) `
                -DiagnosticRuns $diagnosticRuns
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

$diagnosticReport = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    diagnosticPipelineId = $DiagnosticPipelineId
    inspectedRunLimit = $MaximumRunsToInspect
    candidateCount = $enrichedCandidates.Count
    candidates = $enrichedCandidates
}

$jsonPath = Join-Path $OutputDirectory 'mt-regression-diagnostics.json'
$markdownPath = Join-Path $OutputDirectory 'mt-regression-diagnostics.md'
$diagnosticReport | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

$markdown = [System.Text.StringBuilder]::new()
[void]$markdown.AppendLine('# PerfStar scheduled-binlog supporting evidence')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Diagnostic pipeline: [definition $DiagnosticPipelineId](https://dev.azure.com/devdiv/DevDiv/_build?definitionId=$DiagnosticPipelineId)")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('Diagnostic runs are matched by exact MSBuild source SHA. A failed overall diagnostic run is usable when the affected scenario/OS has task-level Kusto data. Hosted evidence is direct; for Gold candidates it is corroboration from the Hosted backend.')

foreach ($candidate in $enrichedCandidates)
{
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("## $($candidate.Backend)/$($candidate.Os): ``$($candidate.ScenarioPair)``")
    Add-DiagnosticSection -Builder $markdown -Heading 'Current-source diagnostic evidence' -Diagnostic $candidate.currentDiagnostic
    Add-DiagnosticSection -Builder $markdown -Heading 'Last-healthy-source diagnostic evidence' -Diagnostic $candidate.healthyDiagnostic
}

$markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM
Write-Host "Wrote scheduled-binlog evidence for $($enrichedCandidates.Count) candidate(s) to $OutputDirectory."
