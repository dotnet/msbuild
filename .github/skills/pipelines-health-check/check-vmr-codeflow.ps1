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

# Verify gh CLI is available and authenticated
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh CLI is required but not found. Install from https://cli.github.com/"
}
$ghStatus = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "gh CLI is not authenticated. Run 'gh auth login' first."
}

function Invoke-GitHubApi {
    <#
    .SYNOPSIS
        Calls the GitHub REST API via gh cli, returning parsed JSON.
    #>
    param([string]$Endpoint)

    $json = gh api $Endpoint 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "gh api call failed for '$Endpoint': $json"
    }
    return $json | ConvertFrom-Json
}

function Get-OpenCodeflowPRs {
    <#
    .SYNOPSIS
        Finds open codeflow PRs from the source repo in the VMR.
    #>
    $searchQuery = "repo:$GitHubOwner/$GitHubRepo is:pr is:open `"Source code updates from $SourceRepo`" in:title"
    $endpoint = "search/issues?q=$([Uri]::EscapeDataString($searchQuery))&per_page=10&sort=updated&order=desc"

    try {
        $resp = Invoke-GitHubApi -Endpoint $endpoint
    }
    catch {
        Write-Warning "Failed to search GitHub PRs: $($_.Exception.Message)"
        return @()
    }

    $prs = @()
    foreach ($issue in $resp.items) {
        try {
            $prDetail = Invoke-GitHubApi -Endpoint "repos/$GitHubOwner/$GitHubRepo/pulls/$($issue.number)"
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

    $endpoint = "repos/$GitHubOwner/$GitHubRepo/issues/$PullNumber/comments?per_page=50"
    try {
        $comments = Invoke-GitHubApi -Endpoint $endpoint
    }
    catch {
        Write-Warning "Failed to get comments for PR #${PullNumber}: $($_.Exception.Message)"
        return @()
    }

    $upstreamPRs = @()
    foreach ($comment in $comments) {
        # Match full GitHub URL patterns like "https://github.com/dotnet/msbuild/pull/13175"
        $regexMatches = [regex]::Matches($comment.body, "https://github\.com/$([regex]::Escape($SourceRepo))/pull/(\d+)")
        foreach ($m in $regexMatches) {
            $prNum = [int]$m.Groups[1].Value
            if ($upstreamPRs -notcontains $prNum) {
                $upstreamPRs += $prNum
            }
        }

        # Match short reference patterns like "msbuild#13175"
        $repoName = ($SourceRepo -split '/')[-1]
        $shortMatches = [regex]::Matches($comment.body, "$([regex]::Escape($repoName))#(\d+)")
        foreach ($m in $shortMatches) {
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
        $pr = Invoke-GitHubApi -Endpoint "repos/$SourceRepo/pulls/$PullNumber"
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
    param(
        [int]$PullNumber,
        [int]$DefinitionId
    )

    $branchName = "refs/pull/$PullNumber/merge"
    $url = "$Organization/$Project/_apis/build/builds?branchName=$([Uri]::EscapeDataString($branchName))&api-version=7.1&`$top=$Top&queryOrder=startTimeDescending"

    if ($DefinitionId) {
        $url += "&definitions=$DefinitionId"
    }

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
                $errors = @($task.issues | Where-Object { $_.type -eq "error" } | ForEach-Object {
                    Sanitize-ErrorString -Text $_.message
                })
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

    # Compact job summary: count by result instead of listing every job
    $jobSummary = [ordered]@{
        total     = @($timeline.records | Where-Object { $_.type -eq "Job" }).Count
        succeeded = @($timeline.records | Where-Object { $_.type -eq "Job" -and $_.result -eq "succeeded" }).Count
        failed    = @($timeline.records | Where-Object { $_.type -eq "Job" -and $_.result -eq "failed" }).Count
        skipped   = @($timeline.records | Where-Object { $_.type -eq "Job" -and ($_.result -eq "skipped" -or $_.result -eq "canceled") }).Count
    }

    return [ordered]@{
        failedJobs = $failedJobs
        jobSummary = $jobSummary
    }
}

function Sanitize-ErrorString {
    <#
    .SYNOPSIS
        Cleans an error string for safe JSON serialization: strips control
        characters and truncates to a reasonable length.
    #>
    param(
        [string]$Text,
        [int]$MaxLength = 500
    )

    if ([string]::IsNullOrEmpty($Text)) { return "" }

    # Strip control characters (0x00-0x1F) except common whitespace (\n \r \t)
    $cleaned = $Text -replace '[\x00-\x08\x0B\x0C\x0E-\x1F]', ''
    # Collapse runs of whitespace (newlines, tabs, spaces) into a single space
    $cleaned = $cleaned -replace '\s+', ' '
    $cleaned = $cleaned.Trim()

    if ($cleaned.Length -gt $MaxLength) {
        $cleaned = $cleaned.Substring(0, $MaxLength) + "..."
    }
    return $cleaned
}

function Get-FailureCategory {
    <#
    .SYNOPSIS
        Categorizes a failure based on error messages.
    #>
    param([string[]]$ErrorMessages)

    foreach ($msg in $ErrorMessages) {
        if ($msg -match "error MSB4216.*BinaryToolTask") { return "SOURCE_BUILD_TASK_HOST" }
        if ($msg -match "MSB4216.*task host") { return "TASK_HOST" }
        if ($msg -match "MSB4027.*MetadataLoadContext.*disposed") { return "TASK_HOST" }
        if ($msg -match "error CS\d+") { return "COMPILATION" }
        if ($msg -match "error MSB3073.*exited with code") { return "BUILD_COMMAND" }
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
    $output | ConvertTo-Json -Depth 10
    return
}

Write-Host "Found $($codeflowPRs.Count) open codeflow PR(s)." -ForegroundColor Cyan

foreach ($pr in $codeflowPRs) {
    Write-Host "Processing PR #$($pr.number): $($pr.title)..." -ForegroundColor Cyan

    # Get upstream PRs included in this codeflow
    $upstreamPRNumbers = Get-IncludedUpstreamPRs -PullNumber $pr.number
    $upstreamPRDetails = @()

    # Fetch details for up to 30 upstream PRs to keep output manageable.
    # For larger batches, only the first 30 get full details; the rest are
    # listed as numbers so the agent knows they exist.
    $maxDetailedPRs = 30
    $detailedNumbers = $upstreamPRNumbers | Select-Object -First $maxDetailedPRs
    $remainingNumbers = @()
    if ($upstreamPRNumbers.Count -gt $maxDetailedPRs) {
        $remainingNumbers = @($upstreamPRNumbers | Select-Object -Skip $maxDetailedPRs)
        Write-Host "  Found $($upstreamPRNumbers.Count) upstream PRs; fetching details for $maxDetailedPRs, listing rest as numbers." -ForegroundColor Yellow
    }
    foreach ($num in $detailedNumbers) {
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
            $runObj.jobSummary = $timelineInfo.jobSummary
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
            $runObj.jobSummary = $timelineInfo.jobSummary
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
        upstreamPRCount = $upstreamPRNumbers.Count
        additionalUpstreamPRNumbers = @($remainingNumbers)
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

$output | ConvertTo-Json -Depth 10
