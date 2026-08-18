<#
.SYNOPSIS
    Prints the latest known-good OptProf optimization-data drop produced for a branch.

.DESCRIPTION
    The MSBuild official build applies VS optimization (OptProf/IBC) data identified by a drop path
    (passed as `/p:VisualStudioIbcDrop`). A freshly-cut `vs*` release branch has no collected OptProf
    data yet, so its first official build fails unless seeded with a known-good drop.

    This resolves that seed deterministically: it reads the latest successful run of the
    `MSBuild-OptProf` pipeline (definition 17389, devdiv) on the given branch and extracts the value
    emitted by its "Set PreviousOptimizationInputsDropName" step, e.g.
    `OptimizationData/DotNet-msbuild-Trusted/main/20260623.5/14471019/1`.

    The drop reported by that step was produced by a *different* OptProf run, whose id is embedded in
    the drop path. That producing run is not necessarily green: the OptProf finalization phase
    publishes an OptimizationInputs drop even when "Run Tests" failed, so a half-failed run still
    yields a drop -- one containing only a partial set of profiles. Pinning such a drop seeds the next
    release branch with degraded optimization data, silently. This script therefore validates the
    producing run and walks back to older successful runs until it finds a drop with clean provenance.

    Use the output as the hardcoded `OptProfBaselineDrop` in `.vsts-dotnet.yml`; refresh it each
    release (see documentation/release-checklist.md, Phase 3).

    Requires `az login` with access to the devdiv Azure DevOps organization.

.PARAMETER SourceBranch
    Branch whose latest OptProf run to read. Default: main.

.PARAMETER OptProfPipelineId
    The MSBuild-OptProf pipeline definition id. Default: 17389.

.PARAMETER MaximumRunsToInspect
    How many recent successful runs to walk back through while looking for a drop with clean
    provenance. Must be between 1 and 100. Default: 10.

.PARAMETER AllowDegradedProvenance
    Accept the newest drop even if the run that produced it did not collect a full set of profiles.
    Use only when you knowingly accept seeding the next release branch with partial data.

.EXAMPLE
    ./Get-LatestOptProfDrop.ps1
    -> OptimizationData/DotNet-msbuild-Trusted/main/20260623.5/14471019/1
#>

[CmdletBinding()]
param(
    [string]$SourceBranch = 'main',
    [int]$OptProfPipelineId = 17389,
    [ValidateRange(1, 100)]
    [int]$MaximumRunsToInspect = 10,
    [switch]$AllowDegradedProvenance
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

$DevDivOrg = 'https://devdiv.visualstudio.com/DevDiv'
$AzureDevOpsResource = '499b84ac-1321-427f-aa17-267ca6975798'

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }

# A drop path ends with the run that produced it, e.g.
#   OptimizationData/DotNet-msbuild-Trusted/main/<componentBuildRunName>/<optProfBuildId>/<stageAttempt>
# so the producing run id is the second-to-last segment.
function Get-ProducingBuildId([string]$dropPath) {
    $segments = $dropPath.Trim().Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($segments.Length -lt 6) { return $null }

    $parsed = 0
    if ([int]::TryParse($segments[$segments.Length - 2], [ref]$parsed)) { return $parsed }
    return $null
}

# Returns a description of why the producing run is unfit to seed a release branch, or $null if it
# is clean. The finalization phase publishes a drop even when the profiling tests failed, so the
# run's own result is not enough -- the test steps have to be green too.
function Get-ProvenanceIssue([int]$buildId, [hashtable]$requestHeaders) {
    try {
        $timeline = Invoke-RestMethod -Uri "$DevDivOrg/_apis/build/builds/$buildId/timeline?api-version=7.0" -Headers $requestHeaders
    }
    catch {
        return "could not inspect producing run ${buildId}: $($_.Exception.Message)"
    }

    # These exact names are a contract with the MSBuild-OptProf pipeline. A missing or renamed step
    # intentionally rejects the drop rather than silently weakening provenance validation.
    foreach ($stepName in @('Run Tests', 'Validate Test Results')) {
        $record = $timeline.records | Where-Object { $_.name -eq $stepName } | Select-Object -First 1
        if (-not $record) { return "producing run $buildId has no '$stepName' step" }
        if ($record.result -ne 'succeeded') { return "'$stepName' in producing run $buildId was '$($record.result)'" }
    }

    return $null
}

$token = (& az account get-access-token --resource $AzureDevOpsResource --query accessToken -o tsv 2>$null)
if (-not $token) { throw "Could not get an Azure DevOps token. Run 'az login' with devdiv access." }
$headers = @{ Authorization = "Bearer $token" }

# Newest successful runs first. The newest is not necessarily usable: it reports the drop the last
# official build consumed, which may have been produced by a run whose profiling tests failed.
$u = "$DevDivOrg/_apis/build/builds?definitions=$OptProfPipelineId&branchName=refs/heads/$SourceBranch&resultFilter=succeeded&statusFilter=completed&queryOrder=finishTimeDescending&api-version=7.0&`$top=$MaximumRunsToInspect"
$runs = (Invoke-RestMethod -Uri $u -Headers $headers).value
if (-not $runs) { throw "No successful MSBuild-OptProf ($OptProfPipelineId) run found on '$SourceBranch'." }

$rejected = @()
foreach ($run in $runs) {
    Write-Info "Inspecting MSBuild-OptProf run $($run.id) ($($run.buildNumber)), finished $($run.finishTime)"

    # Find the 'Set PreviousOptimizationInputsDropName' step and read its log.
    try {
        $tl = Invoke-RestMethod -Uri "$DevDivOrg/_apis/build/builds/$($run.id)/timeline?api-version=7.0" -Headers $headers
    }
    catch {
        $rejected += "  run $($run.id) ($($run.buildNumber)): could not read timeline: $($_.Exception.Message)"
        continue
    }

    $step = $tl.records | Where-Object { $_.name -like '*PreviousOptimizationInputsDropName*' } | Select-Object -First 1
    if (-not $step -or -not $step.log -or -not $step.log.id) {
        $rejected += "  run $($run.id) ($($run.buildNumber)): no 'Set PreviousOptimizationInputsDropName' step with a log"
        continue
    }

    try {
        $log = Invoke-RestMethod -Uri "$DevDivOrg/_apis/build/builds/$($run.id)/logs/$($step.log.id)?api-version=7.0" -Headers $headers
    }
    catch {
        $rejected += "  run $($run.id) ($($run.buildNumber)): could not read drop-selection log: $($_.Exception.Message)"
        continue
    }

    $match = [regex]::Match(($log -join "`n"), 'PreviousOptimizationInputsDropName:\s*(OptimizationData/\S+)')
    if (-not $match.Success) {
        $rejected += "  run $($run.id) ($($run.buildNumber)): no OptimizationData drop path in the step log"
        continue
    }

    $drop = $match.Groups[1].Value.Trim()
    $producingBuildId = Get-ProducingBuildId $drop
    if (-not $producingBuildId) {
        $rejected += "  run $($run.id) ($($run.buildNumber)): could not parse a producing run id out of '$drop'"
        continue
    }

    $issue = Get-ProvenanceIssue $producingBuildId $headers
    if ($issue) {
        if (-not $AllowDegradedProvenance) {
            Write-Warning "Skipping '$drop': $issue."
            $rejected += "  run $($run.id) ($($run.buildNumber)): $issue"
            continue
        }

        Write-Warning "Accepting '$drop' despite degraded provenance ($issue) because -AllowDegradedProvenance was specified."
    }
    else {
        Write-Info "Drop was produced by OptProf run ${producingBuildId}, which collected a full set of profiles."
    }

    Write-Host ""
    Write-Host "OptProfBaselineDrop = $drop" -ForegroundColor Green
    # Emit the bare value for scripting.
    return $drop
}

throw @"
No OptProf drop with clean provenance found in the last $MaximumRunsToInspect successful run(s) on '$SourceBranch'.
Rejected:
$($rejected -join [Environment]::NewLine)

Fix the MSBuild-OptProf pipeline before cutting a release branch, raise -MaximumRunsToInspect to look
further back, or re-run with -AllowDegradedProvenance if you knowingly accept partial profile data.
"@
