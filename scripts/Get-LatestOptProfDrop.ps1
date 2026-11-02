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

    Use the output as the hardcoded `OptProfBaselineDrop` in `.vsts-dotnet.yml`; refresh it each
    release (see documentation/release-checklist.md, Phase 3).

    Requires `az login` with access to the devdiv Azure DevOps organization.

.PARAMETER SourceBranch
    Branch whose latest OptProf run to read. Default: main.

.PARAMETER OptProfPipelineId
    The MSBuild-OptProf pipeline definition id. Default: 17389.

.EXAMPLE
    ./Get-LatestOptProfDrop.ps1
    -> OptimizationData/DotNet-msbuild-Trusted/main/20260623.5/14471019/1
#>

[CmdletBinding()]
param(
    [string]$SourceBranch = 'main',
    [int]$OptProfPipelineId = 17389
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

$DevDivOrg = 'https://devdiv.visualstudio.com/DevDiv'
$AzureDevOpsResource = '499b84ac-1321-427f-aa17-267ca6975798'

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }

$token = (& az account get-access-token --resource $AzureDevOpsResource --query accessToken -o tsv 2>$null)
if (-not $token) { throw "Could not get an Azure DevOps token. Run 'az login' with devdiv access." }
$headers = @{ Authorization = "Bearer $token" }

# Latest successful run on the branch.
$u = "$DevDivOrg/_apis/build/builds?definitions=$OptProfPipelineId&branchName=refs/heads/$SourceBranch&resultFilter=succeeded&statusFilter=completed&queryOrder=finishTimeDescending&api-version=7.0&`$top=1"
$run = (Invoke-RestMethod -Uri $u -Headers $headers).value | Select-Object -First 1
if (-not $run) { throw "No successful MSBuild-OptProf ($OptProfPipelineId) run found on '$SourceBranch'." }
Write-Info "Latest successful MSBuild-OptProf run on '$SourceBranch': $($run.id) ($($run.buildNumber)), finished $($run.finishTime)"

# Find the 'Set PreviousOptimizationInputsDropName' step and read its log.
$tl = Invoke-RestMethod -Uri "$DevDivOrg/_apis/build/builds/$($run.id)/timeline?api-version=7.0" -Headers $headers
$step = $tl.records | Where-Object { $_.name -like '*PreviousOptimizationInputsDropName*' } | Select-Object -First 1
if (-not $step -or -not $step.log -or -not $step.log.id) {
    throw "Could not find the 'Set PreviousOptimizationInputsDropName' step (with a log) in run $($run.id)."
}

$log = Invoke-RestMethod -Uri "$DevDivOrg/_apis/build/builds/$($run.id)/logs/$($step.log.id)?api-version=7.0" -Headers $headers
$match = [regex]::Match(($log -join "`n"), 'PreviousOptimizationInputsDropName:\s*(OptimizationData/\S+)')
if (-not $match.Success) {
    throw "Could not extract an OptimizationData drop path from the step log of run $($run.id)."
}

$drop = $match.Groups[1].Value.Trim()
Write-Host ""
Write-Host "OptProfBaselineDrop = $drop" -ForegroundColor Green
# Emit the bare value for scripting.
$drop
