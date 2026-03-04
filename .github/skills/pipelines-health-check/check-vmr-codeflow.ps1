<#
.SYNOPSIS
    Checks health of VMR codeflow PRs that flow MSBuild source into dotnet/dotnet.

.DESCRIPTION
    Finds open codeflow PRs from dotnet/msbuild to dotnet/dotnet, retrieves
    the dotnet-unified-build pipeline runs from dnceng-public/public for each,
    extracts failure details, and correlates them with the upstream MSBuild PRs
    included in the codeflow. Outputs structured JSON for agent consumption.

.PARAMETER GitHubOwner
    GitHub owner of the VMR repository. Defaults to "dotnet".

.PARAMETER GitHubRepo
    GitHub VMR repository name. Defaults to "dotnet".

.PARAMETER SourceRepo
    Source repository that codeflows into the VMR. Defaults to "dotnet/msbuild".

.PARAMETER Organization
    Azure DevOps organization URL for the VMR pipeline. Defaults to
    "https://dev.azure.com/dnceng-public".

.PARAMETER Project
    Azure DevOps project name. Defaults to "public".

.PARAMETER Top
    Number of recent pipeline runs to retrieve per PR. Defaults to 3.

.EXAMPLE
    .\check-vmr-codeflow.ps1
    .\check-vmr-codeflow.ps1 -Top 5
#>
[CmdletBinding()]
param(
    [string]$GitHubOwner = "dotnet",
    [string]$GitHubRepo = "dotnet",
    [string]$SourceRepo = "dotnet/msbuild",
    [string]$Organization = "https://dev.azure.com/dnceng-public",
    [string]$Project = "public",
    [int]$Top = 3
)

$ErrorActionPreference = "Stop"

function Get-OpenCodeflowPRs {
    <#
    .SYNOPSIS
        Finds open codeflow PRs from the source repo in the VMR.
    #>
    $searchQuery = "repo:$GitHubOwner/$GitHubRepo is:pr is:open `"Source code updates from $SourceRepo`" in:title"
    $url = "https://api.github.com/search/issues?q=$([Uri]::EscapeDataString($searchQuery))&per_page=10&sort=updated&order=desc"

    try {
        $resp = Invoke-RestMethod -Uri $url -Headers @{
            "Accept" = "application/vnd.github+json"
            "User-Agent" = "msbuild-health-check"
        } -ErrorAction Stop
    }
    catch {
        Write-Warning "Failed to search GitHub PRs: $($_.Exception.Message)"
        return @()
    }

    $prs = @()
    foreach ($issue in $resp.items) {
        # Get full PR details (issue search doesn't include head branch)
        try {
            $prDetail = Invoke-RestMethod -Uri "https://api.github.com/repos/$GitHubOwner/$GitHubRepo/pulls/$($issue.number)" -Headers @{
                "Accept" = "application/vnd.github+json"
                "User-Agent" = "msbuild-health-check"
            } -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to get PR #$($issue.number) details: $($_.Exception.Message)"
            continue
        }

        $prs += $prDetail
    }

    return $prs
}

function Get-IncludedUpstreamPRs {
    <#
    .SYNOPSIS
        Parses PR comments to extract upstream msbuild PR numbers included in the codeflow.
    #>
    param([int]$PullNumber)

    $url = "https://api.github.com/repos/$GitHubOwner/$GitHubRepo/issues/$PullNumber/comments?per_page=50"
    try {
        $comments = Invoke-RestMethod -Uri $url -Headers @{
            "Accept" = "application/vnd.github+json"
            "User-Agent" = "msbuild-health-check"
        } -ErrorAction Stop
    }
    catch {
        Write-Warning "Failed to get comments for PR #$PullNumber"
        return @()
    }

    $upstreamPRs = @()
    foreach ($comment in $comments) {
        # Match patterns like "https://github.com/dotnet/msbuild/pull/13175" or "msbuild#13175"
        $matches = [regex]::Matches($comment.body, "https://github\.com/$([regex]::Escape($SourceRepo))/pull/(\d+)")
        foreach ($m in $matches) {
            $prNum = [int]$m.Groups[1].Value
            if ($upstreamPRs -notcontains $prNum) {
                $upstreamPRs += $prNum
            }
        }
    }

    # Also check the PR body itself
    # (The PR description may contain commit diff links but not always PR links)

    return $upstreamPRs
}

function Get-UpstreamPRDetails {
    <#
    .SYNOPSIS
        Gets title and URL for an upstream msbuild PR.
    #>
    param([int]$PullNumber)

    try {
        $pr = Invoke-RestMethod -Uri "https://api.github.com/repos/$SourceRepo/pulls/$PullNumber" -Headers @{
            "Accept" = "application/vnd.github+json"
            "User-Agent" = "msbuild-health-check"
        } -ErrorAction Stop
        return [ordered]@{
            number = $PullNumber
            title  = $pr.title
            url    = $pr.html_url
            state  = $pr.state
            merged = $pr.merged
        }
    }
    catch {
        return [ordered]@{
            number = $PullNumber
            title  = "(failed to fetch)"
            url    = "https://github.com/$SourceRepo/pull/$PullNumber"
            state  = "unknown"
            merged = $false
        }
    }
}

function Get-PipelineRunsForPR {
    <#
    .SYNOPSIS
        Gets dotnet-unified-build pipeline runs for a specific PR branch.
    #>
    param([int]$PullNumber)

    $branchName = "refs/pull/$PullNumber/merge"
    $url = "$Organization/$Project/_apis/build/builds?branchName=$([Uri]::EscapeDataString($branchName))&api-version=7.1&`$top=$Top&queryOrder=startTimeDescending"

    try {
        $resp = Invoke-RestMethod -Uri $url -ErrorAction Stop
    }
    catch {
        Write-Warning "Failed to get pipeline runs for PR #$PullNumber from $Organization/$Project"
        return @()
    }

    return $resp.value
}

function Get-FailedJobsFromTimeline {
    <#
    .SYNOPSIS
        Gets failed jobs and their error messages from a build timeline.
    #>
    param([int]$BuildId)

    $url = "$Organization/$Project/_apis/build/builds/$BuildId/timeline?api-version=7.1"
    try {
        $timeline = Invoke-RestMethod -Uri $url -ErrorAction Stop
    }
    catch {
        Write-Warning "Failed to get timeline for build $BuildId"
        return @()
    }

    $failedJobs = @()
    $jobs = $timeline.records | Where-Object { $_.type -eq "Job" -and $_.result -eq "failed" }

    foreach ($job in $jobs) {
        # Find failed tasks within this job
        $failedTasks = $timeline.records | Where-Object {
            $_.type -eq "Task" -and $_.result -eq "failed" -and $_.parentId -eq $job.id
        }

        $taskDetails = @()
        foreach ($task in $failedTasks) {
            $errors = @()
            if ($task.issues) {
                $errors = @($task.issues | Where-Object { $_.type -eq "error" } | ForEach-Object { $_.message })
            }
            $taskDetails += [ordered]@{
                name   = $task.name
                logId  = if ($task.log) { $task.log.id } else { $null }
                errors = $errors
            }
        }

        $failedJobs += [ordered]@{
            name        = $job.name
            state       = $job.state
            result      = $job.result
            startTime   = $job.startTime
            finishTime  = $job.finishTime
            failedTasks = @($taskDetails)
        }
    }

    # Also collect all stages and their results for the overview
    $stages = @($timeline.records | Where-Object { $_.type -eq "Stage" } | ForEach-Object {
        [ordered]@{
            name   = $_.name
            state  = $_.state
            result = $_.result
        }
    })

    $allJobs = @($timeline.records | Where-Object { $_.type -eq "Job" } | ForEach-Object {
        [ordered]@{
            name   = $_.name
            state  = $_.state
            result = $_.result
        }
    })

    return [ordered]@{
        failedJobs = $failedJobs
        stages     = $stages
        allJobs    = $allJobs
    }
}

function Get-FailureCategory {
    <#
    .SYNOPSIS
        Categorizes a failure based on error messages.
    #>
    param([string[]]$ErrorMessages)

    foreach ($msg in $ErrorMessages) {
        if ($msg -match "MSB4216.*task host") { return "TASK_HOST" }
        if ($msg -match "MSB4027.*MetadataLoadContext.*disposed") { return "TASK_HOST" }
        if ($msg -match "error CS\d+") { return "COMPILATION" }
        if ($msg -match "error MSB3073.*exited with code") { return "BUILD_COMMAND" }
        if ($msg -match "error MSB4216.*BinaryToolTask") { return "SOURCE_BUILD_TASK_HOST" }
        if ($msg -match "NuGet|401|feed|authentication") { return "NUGET_AUTH" }
        if ($msg -match "signing|certificate|CodeSign") { return "SIGNING" }
        if ($msg -match "timeout|Timeout|TIMEOUT") { return "TIMEOUT" }
        if ($msg -match "out of memory|OutOfMemory|OOM") { return "RESOURCE" }
    }
    return "UNKNOWN"
}

# --- Main ---

$now = [DateTimeOffset]::UtcNow
$allResults = @()

Write-Host "Searching for open codeflow PRs from $SourceRepo..." -ForegroundColor Cyan
$codeflowPRs = Get-OpenCodeflowPRs

if ($codeflowPRs.Count -eq 0) {
    # Return empty result
    $output = [PSCustomObject][ordered]@{
        sourceRepo    = $SourceRepo
        vmrRepo       = "$GitHubOwner/$GitHubRepo"
        codeflowPRs   = @()
        summary       = "No open codeflow PRs found from $SourceRepo."
    }
    $output | ConvertTo-Json -Depth 8
    return
}

Write-Host "Found $($codeflowPRs.Count) open codeflow PR(s)." -ForegroundColor Cyan

foreach ($pr in $codeflowPRs) {
    Write-Host "Processing PR #$($pr.number): $($pr.title)..." -ForegroundColor Cyan

    # Get upstream PRs included in this codeflow
    $upstreamPRNumbers = Get-IncludedUpstreamPRs -PullNumber $pr.number
    $upstreamPRDetails = @()
    foreach ($num in $upstreamPRNumbers) {
        $upstreamPRDetails += Get-UpstreamPRDetails -PullNumber $num
    }

    # Get pipeline runs
    $pipelineRuns = Get-PipelineRunsForPR -PullNumber $pr.number
    $runResults = @()

    foreach ($run in $pipelineRuns) {
        $runObj = [ordered]@{
            id          = $run.id
            buildNumber = $run.buildNumber
            status      = $run.status
            result      = $run.result
            startTime   = $run.startTime
            finishTime  = $run.finishTime
            definition  = $run.definition.name
            url         = "$Organization/$Project/_build/results?buildId=$($run.id)"
        }

        # Get timeline details for completed builds
        if ($run.status -eq "completed") {
            $timelineInfo = Get-FailedJobsFromTimeline -BuildId $run.id
            $runObj.stages = $timelineInfo.stages
            $runObj.allJobs = $timelineInfo.allJobs
            $runObj.failedJobs = $timelineInfo.failedJobs

            # Categorize failures
            $allErrors = @()
            foreach ($job in $timelineInfo.failedJobs) {
                foreach ($task in $job.failedTasks) {
                    $allErrors += $task.errors
                }
            }
            $runObj.failureCategories = @($allErrors | ForEach-Object { Get-FailureCategory -ErrorMessages @($_) } | Sort-Object -Unique)
        }
        elseif ($run.status -eq "inProgress") {
            # Get current state of in-progress build
            $timelineInfo = Get-FailedJobsFromTimeline -BuildId $run.id
            $runObj.stages = $timelineInfo.stages
            $runObj.allJobs = $timelineInfo.allJobs
            $runObj.failedJobs = $timelineInfo.failedJobs
        }

        $runResults += [PSCustomObject]$runObj
    }

    # Compute health for this PR
    $completedRuns = @($runResults | Where-Object { $_.status -eq "completed" })
    $failedRuns = @($completedRuns | Where-Object { $_.result -eq "failed" })
    $succeededRuns = @($completedRuns | Where-Object { $_.result -eq "succeeded" })
    $inProgressRuns = @($runResults | Where-Object { $_.status -eq "inProgress" })

    if ($completedRuns.Count -eq 0 -and $inProgressRuns.Count -gt 0) {
        $healthSummary = "IN_PROGRESS - $($inProgressRuns.Count) run(s) still building"
    }
    elseif ($completedRuns.Count -eq 0) {
        $healthSummary = "UNKNOWN - no pipeline runs found"
    }
    elseif ($failedRuns.Count -eq 0) {
        $healthSummary = "HEALTHY - $($succeededRuns.Count)/$($completedRuns.Count) runs succeeded"
    }
    elseif ($succeededRuns.Count -eq 0 -and $inProgressRuns.Count -eq 0) {
        $healthSummary = "UNHEALTHY - all $($failedRuns.Count) run(s) failed"
    }
    elseif ($inProgressRuns.Count -gt 0) {
        $healthSummary = "RETRYING - $($failedRuns.Count) failed, $($inProgressRuns.Count) in progress"
    }
    else {
        $healthSummary = "MIXED - $($failedRuns.Count) failed, $($succeededRuns.Count) succeeded"
    }

    # Correlate failures with upstream PRs
    $failureCorrelation = @()
    foreach ($failedRun in $failedRuns) {
        $categories = @()
        if ($failedRun.failureCategories) {
            $categories = @($failedRun.failureCategories)
        }

        # For each failure category, identify potentially related upstream PRs
        $relatedPRs = @()
        foreach ($category in $categories) {
            switch ($category) {
                "TASK_HOST" {
                    $relatedPRs += @($upstreamPRDetails | Where-Object {
                        $_.title -match "host|node|launch|task.*host|apphost"
                    })
                }
                "SOURCE_BUILD_TASK_HOST" {
                    $relatedPRs += @($upstreamPRDetails | Where-Object {
                        $_.title -match "host|node|launch|source.?build|apphost"
                    })
                }
                "COMPILATION" {
                    # Any PR could cause compilation errors
                    $relatedPRs += @($upstreamPRDetails)
                }
                "SIGNING" {
                    $relatedPRs += @($upstreamPRDetails | Where-Object {
                        $_.title -match "sign|cert|CodeSign"
                    })
                }
                "BUILD_COMMAND" {
                    # Build command failures could be caused by any code change
                    $relatedPRs += @($upstreamPRDetails)
                }
                "NUGET_AUTH" {
                    $relatedPRs += @($upstreamPRDetails | Where-Object {
                        $_.title -match "NuGet|dependency|dependencies|feed"
                    })
                }
            }
        }

        $failureCorrelation += [ordered]@{
            buildId    = $failedRun.id
            categories = $categories
            relatedUpstreamPRs = @($relatedPRs | Group-Object -Property { $_.number } | ForEach-Object { $_.Group[0] })
        }
    }

    $prAge = [math]::Round(($now - [DateTimeOffset]::Parse($pr.created_at)).TotalHours, 1)

    $allResults += [PSCustomObject][ordered]@{
        prNumber        = $pr.number
        prTitle         = $pr.title
        prUrl           = $pr.html_url
        prBranch        = $pr.head.ref
        prAge           = $prAge
        createdAt       = $pr.created_at
        updatedAt       = $pr.updated_at
        upstreamPRs     = @($upstreamPRDetails)
        pipelineRuns    = @($runResults)
        healthSummary   = $healthSummary
        failureCorrelation = @($failureCorrelation)
    }
}

# Build overall summary
$totalPRs = $allResults.Count
$healthyCount = @($allResults | Where-Object { $_.healthSummary -match "^HEALTHY" }).Count
$failingCount = @($allResults | Where-Object { $_.healthSummary -match "^UNHEALTHY" }).Count
$retryingCount = @($allResults | Where-Object { $_.healthSummary -match "^RETRYING|^IN_PROGRESS" }).Count

$summaryParts = @("$totalPRs open codeflow PR(s) from $SourceRepo")
if ($healthyCount -gt 0) { $summaryParts += "$healthyCount healthy" }
if ($failingCount -gt 0) { $summaryParts += "$failingCount failing" }
if ($retryingCount -gt 0) { $summaryParts += "$retryingCount in progress/retrying" }

$output = [PSCustomObject][ordered]@{
    sourceRepo    = $SourceRepo
    vmrRepo       = "$GitHubOwner/$GitHubRepo"
    pipeline      = "dotnet-unified-build"
    organization  = $Organization
    project       = $Project
    codeflowPRs   = @($allResults)
    summary       = ($summaryParts -join ". ") + "."
}

$output | ConvertTo-Json -Depth 8
