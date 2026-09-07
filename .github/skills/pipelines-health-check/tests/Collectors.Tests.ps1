#requires -Version 5.1
<#
.SYNOPSIS
Exercises the health collectors offline with intercepted service commands.
.DESCRIPTION
Run in a fresh PowerShell process. Uses no external testing dependencies, SDK,
credentials, or services. The az, gh, and Invoke-RestMethod fixtures reject
unexpected operations instead of forwarding them to real services.
#>
$ErrorActionPreference = 'Stop'
if ($MyInvocation.InvocationName -eq '.')
{
    throw 'Run this fixture suite in a fresh PowerShell process; do not dot-source it.'
}
$root = Split-Path $PSScriptRoot -Parent
$global:FixtureCase = ''
$global:FixtureAssertions = 0
$global:FixtureNow = [DateTimeOffset]::UtcNow
$global:FixtureHead = 'a' * 40
$global:FixtureMerge = 'b' * 40
$global:FixtureRepositoryId = 'a290117c-5a8a-40f7-bc2c-f14dbe3acf6d'
$global:FixtureReviewerId = '66cc9d27-aef7-4399-ba2c-3dccb4489098'

function Assert-Fixture($Condition, [string]$Message)
{
    if (!$Condition) { throw "Assertion failed: $Message" }
    $global:FixtureAssertions++
}

function Assert-Throws([scriptblock]$Action, [string]$Message, [string]$ExpectedError)
{
    $actualError = $null
    try { & $Action | Out-Null } catch { $actualError = $_.Exception.Message }
    Assert-Fixture ($null -ne $actualError -and $actualError -match $ExpectedError) "$Message (received: $actualError)"
}

function Get-FixtureResponse([string]$Url)
{
    $now = $global:FixtureNow
    if ($Url -match '/pagination')
    {
        $skip = if ($Url -match '\$skip=(\d+)') { [int]$Matches[1] } else { 0 }
        $ids = if ($skip -eq 0 -or $global:FixtureCase -eq 'repeated-page') { 1..100 } elseif ($skip -eq 100) { @(101) } else { @() }
        return @{ value = @($ids | ForEach-Object { @{ id = $_ } }) }
    }
    if ($Url -match '\$skip=(\d+)' -and [int]$Matches[1] -gt 0) { return @{ value = @() } }
    if ($Url -match '/identities\?')
    {
        return @{ value = @(@{
            id = $global:FixtureReviewerId; isActive = $true
            providerDisplayName = if ($global:FixtureCase -eq 'wrong-reviewer') { 'Other team' } else { 'MSBuild' }
        }) }
    }
    if ($Url -match '/git/repositories/VS\?')
    {
        return @{ id = $global:FixtureRepositoryId; name = 'VS'; project = @{ id = 'test-project' } }
    }
    if ($Url -match '/pullRequests\?')
    {
        $pr = @{
            pullRequestId = 50; repository = @{ id = $global:FixtureRepositoryId }
            targetRefName = 'refs/heads/main'; title = 'Insert MSBuild'
            creationDate = $now.AddDays(-3).ToString('o'); mergeStatus = 'succeeded'
            closedDate = $now.AddDays(-2).ToString('o')
        }
        if ($Url -match 'status=completed')
        {
            $newer = $pr.Clone()
            $newer.pullRequestId = 51
            $newer.closedDate = $now.AddDays(-1).ToString('o')
            return @{ value = @($pr, $newer) }
        }
        return @{ value = @($pr) }
    }
    if ($Url -match '/iterations\?') { return @{ value = @(@{ id = 1 }, @{ id = 2 }) } }
    if ($Url -match '/statuses\?')
    {
        if ($global:FixtureCase -eq 'no-checks') { return @{ value = @() } }
        if ($global:FixtureCase -like 'pr-wide-*')
        {
            $wideState = if ($global:FixtureCase -eq 'pr-wide-newer-success') { 'succeeded' } else { 'failed' }
            $iterationState = if ($wideState -eq 'succeeded') { 'failed' } else { 'succeeded' }
            $iterationTime = if ($global:FixtureCase -eq 'pr-wide-same-time-failure') { $now } else { $now.AddHours(-1) }
            return @{ value = @(
                @{ id = 20; iterationId = 2; context = @{ genre = 'custom'; name = 'new check' }; state = $iterationState; updatedDate = $iterationTime.ToString('o') },
                @{ id = 21; iterationId = 0; context = @{ genre = 'custom'; name = 'new check' }; state = $wideState; updatedDate = $now.ToString('o') },
                @{ id = 30; iterationId = 1; context = @{ genre = 'custom'; name = 'new check' }; state = 'failed'; updatedDate = $now.AddHours(1).ToString('o') }
            ) }
        }
        return @{ value = @(
            @{ id = 20; iterationId = 2; context = @{ genre = 'custom'; name = 'new check' }; state = 'succeeded'; updatedDate = $now.ToString('o') },
            @{ id = 19; iterationId = 2; context = @{ genre = 'custom'; name = 'new check' }; state = 'failed'; updatedDate = $now.AddHours(-1).ToString('o') },
            @{ id = 30; iterationId = 1; context = @{ genre = 'custom'; name = 'new check' }; state = 'failed'; updatedDate = $now.AddHours(1).ToString('o') }
        ) }
    }
    if ($Url -match '/policy/evaluations\?')
    {
        if ($global:FixtureCase -eq 'no-checks') { return @{ value = @() } }
        return @{ value = @(@{
            configuration = @{
                id = 77; isEnabled = $true; isBlocking = $true
                type = @{ displayName = 'Previously unknown required policy' }
            }
            status = if ($global:FixtureCase -eq 'blocked-policy') { 'rejected' } else { 'approved' }
        }) }
    }
    if ($Url -match '/build/definitions\?')
    {
        return @{ value = @(@{ id = 77; name = 'dotnet-unified-build' }) }
    }
    if ($Url -match '/build/definitions/9434\?')
    {
        $name = switch ($global:FixtureCase) {
            'wrong-pipeline' { 'Other' }
            'custom-pipeline' { 'Acme Build' }
            default { 'MSBuild' }
        }
        return @{ id = 9434; name = $name }
    }
    if ($Url -match '/build/definitions/77\?')
    {
        return @{
            id = 77; name = 'dotnet-unified-build'
            repository = @{ id = 'dotnet/dotnet'; name = 'dotnet/dotnet'; type = 'GitHub'; url = 'https://github.com/dotnet/dotnet' }
        }
    }
    if ($Url -match '/timeline\?')
    {
        if ($global:FixtureCase -eq 'bad-timeline') { return @{ other = 'invalid' } }
        return @{ records = @(
            @{ id = 'stage'; type = 'Stage'; name = 'Build'; result = 'failed'; issues = @() },
            @{ id = 'job'; parentId = 'stage'; type = 'Job'; name = 'Agent'; result = 'failed'; issues = @(@{ type = 'error'; message = 'Agent lost without child task' }) }
        ) }
    }
    if ($Url -match '/build/builds\?')
    {
        $definitionId = if ($Url -match 'definitions=77') { 77 } else { 9434 }
        $prNumber = if ([Uri]::UnescapeDataString($Url) -match 'refs/pull/(\d+)/merge') { [int]$Matches[1] } else { 0 }
        if ($definitionId -eq 77)
        {
            Assert-Fixture ($Url.Contains('repositoryId=dotnet%2Fdotnet') -and $Url.Contains('repositoryType=GitHub')) 'VMR query filters repository'
        }
        $result = switch ($global:FixtureCase) {
            'canceled' { 'canceled' }
            'partial' { 'partiallySucceeded' }
            'failed' { 'failed' }
            default { 'succeeded' }
        }
        if ($Url -match 'resultFilter=succeeded')
        {
            Assert-Fixture ($Url.Contains('queryOrder=finishTimeDescending')) 'last success is ordered by finish time'
            $result = 'succeeded'
        }
        return @{ value = @(@{
            id = 100 + $prNumber; buildNumber = 'sample'
            definition = @{ id = $definitionId; name = if ($definitionId -eq 77) { 'dotnet-unified-build' } else { 'MSBuild' } }
            repository = @{ id = 'dotnet/dotnet'; name = if ($global:FixtureCase -eq 'wrong-build-repo') { 'other/repo' } else { 'dotnet/dotnet' }; url = if ($global:FixtureCase -eq 'wrong-build-repo') { 'https://github.com/other/repo' } else { 'https://github.com/dotnet/dotnet' } }
            sourceBranch = if ($prNumber) { "refs/pull/$prNumber/merge" } else { 'refs/heads/main' }
            sourceVersion = if ($global:FixtureCase -eq 'stale-vmr') { 'c' * 40 } else { $global:FixtureMerge }
            status = 'completed'; result = $result; reason = 'manual'
            startTime = $now.AddHours(-2).ToString('o'); finishTime = $now.AddHours(-1).ToString('o')
        }) }
    }
    throw "Unexpected fixture URL: $Url"
}

function global:az
{
    $global:LASTEXITCODE = 0
    if ($global:FixtureCase -eq 'native-failure')
    {
        $global:LASTEXITCODE = 17
        '{"value":[]}'
        return
    }
    Assert-Fixture ($args[0] -eq 'rest' -and $args[[Array]::IndexOf($args, '--method') + 1] -eq 'get') 'az calls are GET only'
    $url = $args[[Array]::IndexOf($args, '--url') + 1]
    Get-FixtureResponse $url | ConvertTo-Json -Depth 30
}

function global:Invoke-RestMethod
{
    [CmdletBinding()]
    param([string]$Uri, [string]$Method)
    Assert-Fixture ($Method -eq 'Get') 'public HTTP calls are GET only'
    Get-FixtureResponse $Uri | ConvertTo-Json -Depth 30 | ConvertFrom-Json
}

function global:gh
{
    $global:LASTEXITCODE = 0
    Assert-Fixture ($args[0] -eq 'api' -and $args[1] -eq '--method' -and $args[2] -eq 'GET') 'gh calls are GET only'
    $endpoint = [string]$args[3]
    if ($endpoint.StartsWith('search/issues?'))
    {
        $count = if ($global:FixtureCase -eq 'empty-vmr') { 0 } elseif ($global:FixtureCase -eq 'paged-vmr') { 2 } else { 1 }
        $first = @{
            total_count = $count
            incomplete_results = ($global:FixtureCase -eq 'incomplete-vmr')
            items = @()
        }
        if ($count -gt 0) { $first.items = @(@{ number = 12 }) }
        $pages = @($first)
        if ($count -eq 2) { $pages += @{ total_count = 2; incomplete_results = $false; items = @(@{ number = 13 }) } }
        ConvertTo-Json -InputObject $pages -Depth 20
        return
    }
    if ($endpoint -match 'repos/dotnet/dotnet/pulls/(\d+)$')
    {
        @{
            number = [int]$Matches[1]; state = 'open'; title = 'Source code updates from dotnet/msbuild'
            base = @{ repo = @{ full_name = 'dotnet/dotnet' } }
            head = @{ sha = $global:FixtureHead; ref = 'codeflow' }; merge_commit_sha = $global:FixtureMerge
            mergeable = if ($global:FixtureCase -eq 'merge-unresolved') { $null } else { $true }
            html_url = 'https://github.com/dotnet/dotnet/pull/12'
            body = 'dotnet/msbuild#34'
            created_at = $global:FixtureNow.AddDays(-1).ToString('o'); updated_at = $global:FixtureNow.ToString('o')
        } | ConvertTo-Json -Depth 20
        return
    }
    if ($endpoint -match '/comments\?')
    {
        ConvertTo-Json -InputObject @(,@(@{ body = 'https://github.com/dotnet/msbuild/pull/35' })) -Depth 20
        return
    }
    if ($endpoint -match 'repos/dotnet/msbuild/pulls/(\d+)$')
    {
        @{ number = [int]$Matches[1]; title = 'Referenced change'; html_url = "https://github.com/dotnet/msbuild/pull/$($Matches[1])"; merged = $true } | ConvertTo-Json
        return
    }
    throw "Unexpected fixture GitHub endpoint: $endpoint"
}

. (Join-Path $root 'HealthCheck.Common.ps1')
foreach ($case in @(
    @('completed', 'succeeded', 'HEALTHY'), @('completed', 'failed', 'UNHEALTHY'),
    @('completed', 'partiallySucceeded', 'UNHEALTHY'), @('completed', 'canceled', 'CANCELED'),
    @('notStarted', $null, 'PENDING'), @('inProgress', $null, 'IN_PROGRESS'),
    @('completed', $null, 'UNKNOWN'), @('unexpected', 'succeeded', 'UNKNOWN')
))
{
    $health = Get-HealthRunState -Runs @([pscustomobject]@{ status = $case[0]; result = $case[1] })
    Assert-Fixture ($health.state -eq $case[2]) "run classification $($case[0])/$($case[1])"
}
Assert-Fixture ((Get-HealthRunState -Runs @()).state -eq 'UNKNOWN') 'empty run sample is unknown'
Assert-Throws { Get-HealthOrganizationName 'https://example.com/other' } 'organization restriction' 'organization must use'
$global:FixtureCase = 'pagination'
Assert-Fixture (@(Get-HealthPagedValues -Url 'https://dev.azure.com/test/pagination?api-version=7.1').Count -eq 101) 'pagination traverses next page'
Assert-Throws { Get-HealthPagedValues -Url 'https://dev.azure.com/test/pagination?api-version=7.1' -MaxPages 1 } 'pagination limit is failure' 'Collection exceeded'
$global:FixtureCase = 'repeated-page'
Assert-Throws { Get-HealthPagedValues -Url 'https://dev.azure.com/test/pagination?api-version=7.1' } 'repeated page is failure' 'pagination did not advance'
$global:FixtureCase = 'bad-timeline'
Assert-Throws { Get-HealthTimelineDetails -Url 'https://dev.azure.com/test/timeline?api-version=7.1' } 'malformed timeline is failure' 'timeline is unavailable or malformed'

foreach ($case in @(@('success', 'HEALTHY'), @('canceled', 'CANCELED'), @('partial', 'UNHEALTHY'), @('failed', 'UNHEALTHY')))
{
    $global:FixtureCase = $case[0]
    $json = & (Join-Path $root 'check-pipeline-health.ps1') -PipelineIds @(9434) -Branch 'refs/heads/main' -IncludeFailureDetails
    Assert-Fixture (($json -join "`n").TrimStart().StartsWith('[')) 'single pipeline emits JSON array'
    $result = $json | ConvertFrom-Json
    Assert-Fixture ($result.healthState -eq $case[1]) "pipeline status $($case[0])"
    Assert-Fixture ($result.branch -eq 'refs/heads/main') 'branch is not double-prefixed'
    if ($case[0] -eq 'failed') { Assert-Fixture ($result.recentRuns[0].failureDetails.Count -eq 2) 'job and stage failures retained' }
}
$global:FixtureCase = 'native-failure'
Assert-Throws { & (Join-Path $root 'check-pipeline-health.ps1') -PipelineIds @(9434) } 'native failure is not empty healthy result' 'failed \(exit 17\)'
$global:FixtureCase = 'wrong-pipeline'
Assert-Throws { & (Join-Path $root 'check-pipeline-health.ps1') -PipelineIds @(9434) } 'default definition identity is checked' 'Default pipeline ID'

foreach ($case in @(
    @('vs-success', 'HEALTHY'), @('blocked-policy', 'UNHEALTHY'), @('no-checks', 'UNKNOWN'),
    @('pr-wide-newer-failure', 'UNHEALTHY'), @('pr-wide-newer-success', 'HEALTHY'),
    @('pr-wide-same-time-failure', 'UNHEALTHY')
))
{
    $global:FixtureCase = $case[0]
    $result = (& (Join-Path $root 'check-vs-pr-status.ps1')) | ConvertFrom-Json
    Assert-Fixture ($result.prs[0].healthState -eq $case[1]) "VS status $($case[0])"
    Assert-Fixture ($result.lastMergedPr.id -eq 51) 'last merged PR selected by close time'
    if ($case[0] -eq 'vs-success')
    {
        Assert-Fixture ($result.prs[0].checks.failed -eq 0 -and $result.prs[0].checks.succeeded -eq 1) 'latest timestamp/current iteration deduplication'
    }
    if ($case[0] -like 'pr-wide-*')
    {
        Assert-Fixture ($result.prs[0].checks.total -eq 1 -and $result.prs[0].checks.details[0].iterationId -eq 0) 'newest eligible PR-wide status wins after filtering obsolete iterations'
    }
}
$global:FixtureCase = 'wrong-reviewer'
Assert-Throws { & (Join-Path $root 'check-vs-pr-status.ps1') } 'reviewer identity checked' 'reviewer identity is unavailable'

foreach ($case in @(@('current-vmr', 'HEALTHY'), @('stale-vmr', 'UNKNOWN'), @('merge-unresolved', 'UNKNOWN')))
{
    $global:FixtureCase = $case[0]
    $result = (& (Join-Path $root 'check-vmr-codeflow.ps1')) | ConvertFrom-Json
    Assert-Fixture ($result.codeflowPRs[0].healthState -eq $case[1]) "VMR status $($case[0])"
}
$global:FixtureCase = 'empty-vmr'
$result = (& (Join-Path $root 'check-vmr-codeflow.ps1')) | ConvertFrom-Json
Assert-Fixture ($result.codeflowPRs.Count -eq 0 -and $result.collectionStatus -eq 'complete') 'genuine empty search distinguished'
$global:FixtureCase = 'paged-vmr'
$result = (& (Join-Path $root 'check-vmr-codeflow.ps1')) | ConvertFrom-Json
Assert-Fixture ($result.codeflowPRs.Count -eq 2) 'multiple GitHub pages consumed'
$global:FixtureCase = 'incomplete-vmr'
Assert-Throws { & (Join-Path $root 'check-vmr-codeflow.ps1') } 'incomplete GitHub search fails' 'complete PR coverage'
$global:FixtureCase = 'wrong-build-repo'
Assert-Throws { & (Join-Path $root 'check-vmr-codeflow.ps1') } 'nonmatching build repository fails' 'returned build does not match'
$global:FixtureCase = 'current-vmr'
$result = (& (Join-Path $root 'check-vmr-codeflow.ps1') -IncludeUpstreamReferences) | ConvertFrom-Json
Assert-Fixture ($result.codeflowPRs[0].upstreamReferences.count -eq 2) 'body and paginated comment references preserved as references'

$global:FixtureCase = 'custom-pipeline'
foreach ($scope in @(
    @('https://dev.azure.com/another', 'DevDiv'),
    @('https://dev.azure.com/devdiv', 'OtherProject')
))
{
    $result = (& (Join-Path $root 'check-pipeline-health.ps1') -PipelineIds @(9434) -Organization $scope[0] -Project $scope[1]) | ConvertFrom-Json
    Assert-Fixture ($result.pipelineName -eq 'Acme Build' -and $result.healthState -eq 'HEALTHY') 'numeric pipeline IDs do not inherit another organization/project identity'
}

"Offline collector assertions passed: $global:FixtureAssertions. All service commands were intercepted by fixtures."
