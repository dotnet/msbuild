<#
.SYNOPSIS
    Verifies that MSBuild.VSTest.slnx and MSBuild.slnx reference the same
    production projects. Fails (exit 1) on drift.

.DESCRIPTION
    MSBuild.VSTest.slnx mirrors MSBuild.slnx's production graph and adds
    sibling *.UnitTests.VSTest.csproj wrappers in /Tests/. When someone
    adds a new production project to MSBuild.slnx they need to add it to
    MSBuild.VSTest.slnx too, otherwise CTS will silently build a stale
    project graph.

    Heuristic: consider every src/* project under /Production/ in
    MSBuild.VSTest.slnx and every src/* project in MSBuild.slnx that is
    not a *.UnitTests* / *.Tests project; the two sets must match.

    Wired into azure-pipelines/cts-apply.yml as a non-blocking PR-time step
    (see the "Check MSBuild.VSTest.slnx parity" step). Runs standalone with no
    dependency on the `cts` tool, so it can also be used as a local pre-commit
    check: `pwsh ./scripts/cts/Check-SlnxParity.ps1`.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Get-ProductionProjects([string]$slnx) {
    $xml = [xml](Get-Content (Join-Path $repoRoot $slnx) -Raw)
    $all = @()
    $all += @($xml.Solution.Project)
    foreach ($f in @($xml.Solution.Folder)) { $all += @($f.Project) }

    # Production set = src/* projects that are not tests, not samples,
    # not packaging metadata, and not the Roslyn analyzer (which lives
    # outside the CTS test surface).
    $excludeLike = @(
        '*UnitTests*', '*.Tests/*', '*Benchmark*',
        'src/Samples/*', 'src/Package/*',
        'src/ThreadSafeTaskAnalyzer/*',
        # TestSupport projects mirrored only into MSBuild.VSTest.slnx
        # because the wrappers reference them transitively. They are not
        # "production" in MSBuild.slnx terms.
        'src/UnitTests.Shared/*', 'src/Xunit.NetCore.Extensions/*'
    )

    $all |
        Where-Object { $_ -and $_.Path -and $_.Path -like 'src/*' } |
        ForEach-Object { ($_.Path -replace '\\','/') } |
        Where-Object {
            $p = $_
            -not ($excludeLike | Where-Object { $p -like $_ })
        } |
        Sort-Object -Unique
}

$mainProd   = Get-ProductionProjects 'MSBuild.slnx'
$vstestProd = Get-ProductionProjects 'MSBuild.VSTest.slnx'

$missing = $mainProd | Where-Object { $_ -notin $vstestProd }
$extra   = $vstestProd | Where-Object { $_ -notin $mainProd }

if ($missing -or $extra) {
    Write-Host "MSBuild.VSTest.slnx is out of sync with MSBuild.slnx." -ForegroundColor Red
    if ($missing) {
        Write-Host "  Missing in MSBuild.VSTest.slnx:" -ForegroundColor Red
        $missing | ForEach-Object { Write-Host "    $_" }
    }
    if ($extra) {
        Write-Host "  Present in MSBuild.VSTest.slnx but not MSBuild.slnx:" -ForegroundColor Red
        $extra | ForEach-Object { Write-Host "    $_" }
    }
    exit 1
}
Write-Host ("MSBuild.VSTest.slnx production set matches MSBuild.slnx ({0} projects)." -f $mainProd.Count) -ForegroundColor Green
