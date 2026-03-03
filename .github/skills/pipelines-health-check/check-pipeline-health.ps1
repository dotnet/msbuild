<#
.SYNOPSIS
    Checks health of MSBuild Azure DevOps pipelines.

.DESCRIPTION
    Queries recent pipeline runs for the specified pipelines, finds the last
    successful run, and extracts failure reasons from failed runs using the
    Build Timeline API. Outputs structured JSON for agent consumption.

.PARAMETER PipelineIds
    Array of pipeline definition IDs to check. Defaults to MSBuild (9434) and
    MSBuild-OptProf (17389).

.PARAMETER Organization
    Azure DevOps organization URL.

.PARAMETER Project
    Azure DevOps project name.

.PARAMETER Branch
    Branch to filter runs by. Defaults to "main".

.PARAMETER Top
    Number of recent runs to retrieve per pipeline. Defaults to 5.

.EXAMPLE
    .\check-pipeline-health.ps1
    .\check-pipeline-health.ps1 -PipelineIds @(9434) -Branch main -Top 10
#>
[CmdletBinding()]
param(
    [int[]]$PipelineIds = @(9434, 17389),
    [string]$Organization = "https://dev.azure.com/devdiv",
    [string]$Project = "DevDiv",
    [string]$Branch = "main",
    [int]$Top = 5
)

$ErrorActionPreference = "Stop"

# Azure DevOps first-party Entra app ID (used by az rest --resource)
$script:AzDoResource = "499b84ac-1321-427f-aa17-267ca6975798"

function Get-PipelineName {
    param([int]$PipelineId)
    $rawJson = az pipelines show --id $PipelineId --organization $Organization --project $Project --query "{name:name}" -o json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($rawJson)) {
        Write-Warning "Failed to resolve name for pipeline $PipelineId (exit code: $LASTEXITCODE). Is 'az extension add --name azure-devops' installed and 'az login' done?"
        return "Pipeline-$PipelineId"
    }
    $info = $rawJson | ConvertFrom-Json
    return $info.name
}

function Get-RecentRuns {
    param([int]$PipelineId)
    $runsJson = az pipelines runs list `
        --pipeline-id $PipelineId `
        --organization $Organization `
        --project $Project `
        --branch $Branch `
        --top $Top `
        -o json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($runsJson)) {
        Write-Warning "Failed to list runs for pipeline $PipelineId (exit code: $LASTEXITCODE)."
        return @()
    }
    return $runsJson | ConvertFrom-Json
}

function Get-LastSuccessfulRun {
    param([int]$PipelineId)
    $runsJson = az pipelines runs list `
        --pipeline-id $PipelineId `
        --organization $Organization `
        --project $Project `
        --branch $Branch `
        --result succeeded `
        --top 1 `
        -o json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($runsJson)) {
        Write-Warning "Failed to query last successful run for pipeline $PipelineId (exit code: $LASTEXITCODE)."
        return $null
    }
    $runs = $runsJson | ConvertFrom-Json
    if ($runs.Count -eq 0) { return $null }
    return $runs[0]
}

function Get-FailedTasksFromTimeline {
    param([int]$BuildId)
    $url = "$Organization/$Project/_apis/build/builds/$BuildId/timeline?api-version=7.1"
    try {
        $timelineJson = az rest --method get --url $url --resource $script:AzDoResource 2>$null
        $timeline = $timelineJson | ConvertFrom-Json
    }
    catch {
        return @()
    }

    $failedTasks = $timeline.records | Where-Object { $_.type -eq "Task" -and $_.result -eq "failed" }
    $results = @()
    foreach ($task in $failedTasks) {
        $errors = @()
        if ($task.issues) {
            $errors = @($task.issues | Where-Object { $_.type -eq "error" } | ForEach-Object { $_.message })
        }
        $results += [PSCustomObject]@{
            name   = $task.name
            errors = $errors
        }
    }
    return $results
}

# --- Main ---

$now = [DateTimeOffset]::UtcNow
$allResults = @()

foreach ($pipelineId in $PipelineIds) {
    $pipelineName = Get-PipelineName -PipelineId $pipelineId

    # Get recent runs
    $recentRuns = Get-RecentRuns -PipelineId $pipelineId
    $runResults = @()
    foreach ($run in $recentRuns) {
        $runObj = [ordered]@{
            id        = $run.id
            result    = $run.result
            status    = $run.status
            branch    = $run.sourceBranch
            startTime = $run.startTime
            reason    = $run.reason
            url       = "$Organization/$Project/_build/results?buildId=$($run.id)"
        }

        # Get failure details for failed runs
        if ($run.result -eq "failed") {
            $failedTasks = Get-FailedTasksFromTimeline -BuildId $run.id
            $runObj.failedTasks = @($failedTasks | ForEach-Object {
                [ordered]@{
                    name   = $_.name
                    errors = @($_.errors)
                }
            })
        }

        $runResults += [PSCustomObject]$runObj
    }

    # Get last successful run
    $lastSuccess = Get-LastSuccessfulRun -PipelineId $pipelineId
    $lastSuccessObj = $null
    if ($lastSuccess) {
        $finishTime = [DateTimeOffset]::Parse($lastSuccess.finishTime)
        $ageHours = [math]::Round(($now - $finishTime).TotalHours, 1)
        $lastSuccessObj = [ordered]@{
            id         = $lastSuccess.id
            finishTime = $lastSuccess.finishTime
            ageHours   = $ageHours
            url        = "$Organization/$Project/_build/results?buildId=$($lastSuccess.id)"
        }
    }

    # Compute health summary
    $totalRuns = $recentRuns.Count
    $failedCount = @($recentRuns | Where-Object { $_.result -eq "failed" }).Count
    $succeededCount = @($recentRuns | Where-Object { $_.result -eq "succeeded" }).Count
    if ($totalRuns -eq 0) {
        $healthSummary = "UNKNOWN - no recent runs found"
    }
    elseif ($failedCount -eq 0) {
        $healthSummary = "HEALTHY - $succeededCount/$totalRuns recent runs succeeded"
    }
    elseif ($failedCount -eq $totalRuns) {
        $healthSummary = "UNHEALTHY - $failedCount/$totalRuns recent runs failed"
    }
    else {
        $healthSummary = "FLAKY - $failedCount/$totalRuns recent runs failed, $succeededCount succeeded"
    }

    $allResults += [PSCustomObject][ordered]@{
        pipelineName     = $pipelineName
        pipelineId       = $pipelineId
        branch           = "refs/heads/$Branch"
        lastSuccessfulRun = $lastSuccessObj
        recentRuns       = @($runResults)
        healthSummary    = $healthSummary
    }
}

$allResults | ConvertTo-Json -Depth 6
