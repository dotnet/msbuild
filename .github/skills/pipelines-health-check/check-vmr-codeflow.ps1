#requires -Version 5.1
<#
.SYNOPSIS
Reads MSBuild-to-VMR codeflow PRs and revision-matched pipeline outcomes.
.DESCRIPTION
Read-only. Enumerates matching GitHub PRs with completeness checks, resolves the
pipeline definition/repository, and evaluates only sampled builds matching the
current head or test-merge commit. Historical runs are not current-head evidence.
Optional comment references are explicitly not proof of flow membership/causality.
Required query failures terminate without success-shaped JSON.
#>
[CmdletBinding()]
param(
    [ValidatePattern('^[a-zA-Z0-9_.-]+$')]
    [string]$GitHubOwner = 'dotnet',
    [ValidatePattern('^[a-zA-Z0-9_.-]+$')]
    [string]$GitHubRepo = 'dotnet',
    [ValidatePattern('^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$')]
    [string]$SourceRepo = 'dotnet/msbuild',
    [string]$Organization = 'https://dev.azure.com/dnceng-public',
    [string]$Project = 'public',
    [string]$PipelineName = 'dotnet-unified-build',
    [ValidateRange(0, 2147483647)]
    [int]$DefinitionId = 0,
    [ValidateRange(1, 100)]
    [int]$Top = 3,
    [switch]$IncludeFailureDetails,
    [switch]$IncludeUpstreamReferences
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'HealthCheck.Common.ps1')

function Invoke-CodeflowGitHubGet
{
    param(
        [string]$Endpoint,
        [switch]$Paginate
    )
    $arguments = @('api', '--method', 'GET', $Endpoint)
    if ($Paginate) { $arguments += @('--paginate', '--slurp') }
    Invoke-HealthJsonCommand -Command gh -Arguments $arguments -Operation 'GitHub codeflow GET'
}

function Test-CodeflowRepository
{
    param($Repository)
    if ($null -eq $Repository) { return $false }
    $expectedUrl = "https://github.com/$GitHubOwner/$GitHubRepo"
    if (![string]::IsNullOrWhiteSpace($Repository.url))
    {
        return ([string]$Repository.url).TrimEnd('/') -in @(
            $expectedUrl, "$expectedUrl.git", "https://api.github.com/repos/$GitHubOwner/$GitHubRepo"
        )
    }
    ([string]$Repository.id -eq "$GitHubOwner/$GitHubRepo") -or
        ([string]$Repository.name -eq "$GitHubOwner/$GitHubRepo")
}

function Get-UpstreamReferences
{
    param($PullRequest)

    $pages = @(Invoke-CodeflowGitHubGet -Endpoint "repos/$GitHubOwner/$GitHubRepo/issues/$($PullRequest.number)/comments?per_page=100" -Paginate)
    $bodies = [System.Collections.Generic.List[string]]::new()
    $bodies.Add([string]$PullRequest.body)
    foreach ($page in $pages)
    {
        foreach ($comment in $page) { $bodies.Add([string]$comment.body) }
    }
    $numbers = [System.Collections.Generic.HashSet[int]]::new()
    $source = [regex]::Escape($SourceRepo)
    foreach ($body in $bodies)
    {
        foreach ($match in [regex]::Matches($body, "(?:https://github\.com/$source/pull/|$source#)(\d+)"))
        {
            [void]$numbers.Add([int]$match.Groups[1].Value)
        }
    }

    $ordered = @($numbers | Sort-Object)
    $details = @(
        foreach ($number in ($ordered | Select-Object -First 30))
        {
            $pr = Invoke-CodeflowGitHubGet -Endpoint "repos/$SourceRepo/pulls/$number"
            [pscustomobject]@{ number = $number; title = $pr.title; url = $pr.html_url; merged = $pr.merged }
        }
    )
    [pscustomobject]@{
        evidence = 'References in the PR body/comments only; inclusion and causality are not established.'
        count = $ordered.Count
        prs = $details
        additionalNumbers = @($ordered | Select-Object -Skip 30)
    }
}

$organizationName = Get-HealthOrganizationName -Organization $Organization
$Organization = "https://dev.azure.com/$organizationName"
$baseUrl = "$Organization/$([Uri]::EscapeDataString($Project))"
$now = [DateTimeOffset]::UtcNow
$query = "repo:$GitHubOwner/$GitHubRepo is:pr is:open `"Source code updates from $SourceRepo`" in:title"
$pages = @(Invoke-CodeflowGitHubGet -Endpoint "search/issues?q=$([Uri]::EscapeDataString($query))&per_page=100&sort=updated&order=desc" -Paginate)
$issues = [System.Collections.Generic.List[object]]::new()
$issueNumbers = [System.Collections.Generic.HashSet[int]]::new()
$expectedCount = $null
foreach ($page in $pages)
{
    if ($null -eq $page.total_count -or $page.incomplete_results -ne $false -or
        $page.items -isnot [System.Array])
    {
        throw 'GitHub search did not establish complete PR coverage.'
    }
    if ($null -eq $expectedCount) { $expectedCount = [int]$page.total_count }
    if ($page.total_count -ne $expectedCount -or $expectedCount -gt 1000)
    {
        throw 'GitHub search changed during pagination or exceeds its result ceiling; partition the query.'
    }
    foreach ($issue in $page.items)
    {
        if ($null -eq $issue -or $issue.number -le 0 -or !$issueNumbers.Add([int]$issue.number))
        {
            throw 'GitHub search returned an invalid or duplicate PR identity.'
        }
        $issues.Add($issue)
    }
}
if ($null -eq $expectedCount -or $issues.Count -ne $expectedCount)
{
    throw "GitHub PR enumeration is incomplete: retrieved $($issues.Count), expected $expectedCount."
}

$definition = $null
if ($issues.Count -gt 0)
{
    if ($DefinitionId -eq 0)
    {
        $response = Invoke-HealthAzDoGet -Url "$baseUrl/_apis/build/definitions?name=$([Uri]::EscapeDataString($PipelineName))&api-version=7.1" -Public
        $matches = @(Get-HealthResponseValues -Response $response -Operation 'Pipeline discovery' | Where-Object { $_.name -eq $PipelineName })
        if ($matches.Count -ne 1) { throw 'Pipeline discovery is ambiguous or unavailable; supply a verified definition ID.' }
        $DefinitionId = [int]$matches[0].id
    }
    $definition = Invoke-HealthAzDoGet -Url "$baseUrl/_apis/build/definitions/$DefinitionId`?api-version=7.1" -Public
    if ($definition.id -ne $DefinitionId -or $definition.name -ne $PipelineName -or
        !(Test-CodeflowRepository -Repository $definition.repository) -or
        !$definition.repository.id -or $definition.repository.type -ne 'GitHub')
    {
        throw 'The pipeline definition does not match the requested name and GitHub repository.'
    }
}

$results = @(
    foreach ($issue in $issues)
    {
        $pr = Invoke-CodeflowGitHubGet -Endpoint "repos/$GitHubOwner/$GitHubRepo/pulls/$($issue.number)"
        if ($pr.state -ne 'open' -or $pr.base.repo.full_name -ne "$GitHubOwner/$GitHubRepo" -or !$pr.head.sha)
        {
            throw 'A PR changed state or does not match the selected repository; refresh the inventory.'
        }
        $branch = [Uri]::EscapeDataString("refs/pull/$($pr.number)/merge")
        $repositoryId = [Uri]::EscapeDataString([string]$definition.repository.id)
        $repositoryType = [Uri]::EscapeDataString([string]$definition.repository.type)
        $url = "$baseUrl/_apis/build/builds?definitions=$DefinitionId&repositoryId=$repositoryId&repositoryType=$repositoryType&branchName=$branch&`$top=$Top&queryOrder=queueTimeDescending&api-version=7.1"
        $response = Invoke-HealthAzDoGet -Url $url -Public
        $runs = @(Get-HealthResponseValues -Response $response -Operation "Builds for codeflow PR $($pr.number)")
        $runResults = @(
            foreach ($run in $runs)
            {
                if ($run.definition.id -ne $DefinitionId -or !(Test-CodeflowRepository -Repository $run.repository) -or
                    $run.repository.id -ne $definition.repository.id -or
                    $run.sourceBranch -ne "refs/pull/$($pr.number)/merge")
                {
                    throw 'A returned build does not match the requested definition, repository, and PR branch.'
                }
                $current = $run.sourceVersion -eq $pr.head.sha -or
                    ($pr.mergeable -eq $true -and ![string]::IsNullOrWhiteSpace($pr.merge_commit_sha) -and
                     $run.sourceVersion -eq $pr.merge_commit_sha)
                $item = [ordered]@{
                    id = $run.id
                    buildNumber = $run.buildNumber
                    status = $run.status
                    result = $run.result
                    sourceVersion = $run.sourceVersion
                    matchesCurrentRevision = $current
                    startTime = $run.startTime
                    finishTime = $run.finishTime
                    definition = $run.definition.name
                    url = "$baseUrl/_build/results?buildId=$($run.id)"
                }
                if ($IncludeFailureDetails -and $current -and $run.result -in @('failed', 'partiallySucceeded'))
                {
                    $item.failureDetails = @(Get-HealthTimelineDetails -Url "$baseUrl/_apis/build/builds/$($run.id)/timeline?api-version=7.1" -Public)
                }
                [pscustomobject]$item
            }
        )
        $currentRuns = @($runResults | Where-Object { $_.matchesCurrentRevision })
        $health = Get-HealthRunState -Runs $currentRuns
        $item = [ordered]@{
            prNumber = $pr.number
            prTitle = $pr.title
            prUrl = $pr.html_url
            prBranch = $pr.head.ref
            headSha = $pr.head.sha
            mergeSha = $pr.merge_commit_sha
            mergeable = $pr.mergeable
            prAge = [Math]::Round(($now - [DateTimeOffset]::Parse($pr.created_at)).TotalHours, 1)
            createdAt = $pr.created_at
            updatedAt = $pr.updated_at
            pipelineRuns = $runResults
            currentRevisionRunCount = $currentRuns.Count
            healthState = $health.state
            healthSummary = $health.summary
        }
        if ($IncludeUpstreamReferences) { $item.upstreamReferences = Get-UpstreamReferences -PullRequest $pr }
        [pscustomobject]$item
    }
)

[pscustomobject][ordered]@{
    schemaVersion = 2
    collectionStatus = 'complete'
    observedAt = $now.ToString('o')
    sourceRepo = $SourceRepo
    vmrRepo = "$GitHubOwner/$GitHubRepo"
    pipeline = $PipelineName
    definitionId = if ($definition) { $definition.id } else { $null }
    organization = $Organization
    project = $Project
    runSampleLimit = $Top
    codeflowPRs = $results
    summary = "$($results.Count) matching open codeflow PRs. Health uses current-revision runs only; no matching sampled run means UNKNOWN."
} | ConvertTo-Json -Depth 12
