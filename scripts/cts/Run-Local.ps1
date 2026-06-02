<#
.SYNOPSIS
    Runs CTS-selected tests against the *.UnitTests.VSTest projects,
    incrementally based on the local baseline.

.DESCRIPTION
    For each VSTest variant test project, this script rebuilds (incremental)
    the DLL and runs `cts apply vstest`, which consults the baseline at
    <repo>/.cts/baseline and asks CTS which tests are impacted by the
    current working-tree changes. Only impacted tests are executed.

    Establish a baseline first with .\Collect-Local.ps1.

.PARAMETER Project
    Optional short key (e.g. StringTools, Framework) to run just one project.
    Default is to run all.

.PARAMETER SkipBuild
    Reuse already-built VSTest DLLs.

.PARAMETER TimeoutMinutes
    Per-project timeout for the CTS apply step. Default 15.

.PARAMETER Dop
    Degree of parallelism. Default 4.

.EXAMPLE
    .\Run-Local.ps1
    .\Run-Local.ps1 -Project Framework
    .\Run-Local.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [string]$Project,
    [switch]$SkipBuild,
    [int]$TimeoutMinutes = 15,
    [int]$Dop = 4
)

. (Join-Path $PSScriptRoot '_Common.ps1')

Ensure-Cli
Ensure-CtsConfig

if (-not (Test-Path $script:BaselineDir) -or -not (Get-ChildItem -Path $script:BaselineDir -Recurse -ErrorAction SilentlyContinue)) {
    throw "No CTS baseline at '$script:BaselineDir'. Run .\Collect-Local.ps1 first (from a clean working tree)."
}

$projects = Get-Projects -Filter $Project
$null = New-Item -ItemType Directory -Force -Path $script:LogsDir

$totalSw = [Diagnostics.Stopwatch]::StartNew()
$summary = @()

foreach ($p in $projects) {
    Write-Host "==== Apply $($p.Key) ====" -ForegroundColor Cyan

    if (-not $SkipBuild) {
        Build-Project $p
    }

    $dll = Get-ProjectDllPath $p
    if (-not (Test-Path $dll)) {
        throw "DLL not found at '$dll'. Did the build run? Drop -SkipBuild."
    }

    $logFile  = Join-Path $script:LogsDir "apply-$($p.Key).log"
    $errFile  = Join-Path $script:LogsDir "apply-$($p.Key).err.log"
    $tag      = "local-$($p.Key.ToLower())"

    $ctsArgs = @(
        'apply','vstest',
        '--rootPath',           $script:RepoRoot,
        '--config',             $script:ConfigPath,
        '--storage-type',       'filesystem',
        '--storage-type-filesystem-dir', $script:BaselineDir,
        '--tag',                $tag,
        '--local-development',
        '--logs-directory',     $script:LogsDir,
        '--filter',             (Get-ProjectDllFilter $p),
        '--dop',                $Dop,
        '--print-console-output'
    )

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process -FilePath cts -ArgumentList $ctsArgs -RedirectStandardOutput $logFile -RedirectStandardError $errFile -PassThru -NoNewWindow
    if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        $procId = $proc.Id
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        $sw.Stop()
        Write-Host "  TIMEOUT after $TimeoutMinutes min (pid $procId)" -ForegroundColor Red
        Get-Content $logFile -Tail 30 | ForEach-Object { Write-Host "    $_" }
        $summary += [pscustomobject]@{ Project = $p.Key; Status = 'TIMEOUT'; Duration = $sw.Elapsed }
        continue
    }
    $sw.Stop()

    $status = if ($proc.ExitCode -eq 0) { 'OK' } else { "EXIT $($proc.ExitCode)" }
    $color  = if ($proc.ExitCode -eq 0) { 'Green' } else { 'Red' }
    Write-Host "  $status ($([int]$sw.Elapsed.TotalSeconds)s) -> $logFile" -ForegroundColor $color

    # Surface the test summary line(s).
    $tail = Get-Content $logFile -Tail 30
    $tail | Where-Object { $_ -match 'executed|impacted|Test run summary|discovered:|succeeded:|failed:' } |
        ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }

    if ($proc.ExitCode -ne 0) {
        $tail | Select-Object -Last 10 | ForEach-Object { Write-Host "    $_" }
    }

    $summary += [pscustomobject]@{ Project = $p.Key; Status = $status; Duration = $sw.Elapsed }
}

$totalSw.Stop()
Write-Host ""
Write-Host "==== Summary ($([int]$totalSw.Elapsed.TotalSeconds)s total) ====" -ForegroundColor Cyan
$summary | Format-Table -AutoSize | Out-Host

if ($summary | Where-Object { $_.Status -ne 'OK' }) { exit 1 } else { exit 0 }
