<#
.SYNOPSIS
    Collects a CTS baseline for the given test project(s).

.DESCRIPTION
    Runs `cts collect vstest --coverage` against the *.UnitTests.VSTest
    wrapper for each project, populating the local filesystem cache under
    <repo>/.cts/baseline. The baseline is keyed by HEAD SHA, so this script
    requires a clean working tree.

.PARAMETER Project
    Short key from projects.json (e.g. StringTools). Default: all projects.

.PARAMETER SkipBuild
    Reuse already-built VSTest DLLs.

.PARAMETER TimeoutMinutes
    Per-project timeout. Default 15.

.PARAMETER Dop
    Degree of parallelism for CTS. Default 4.
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
Assert-CleanRepo

$projects = Get-Projects -Filter $Project
$null = New-Item -ItemType Directory -Force -Path $script:BaselineDir, $script:LogsDir

$totalSw = [Diagnostics.Stopwatch]::StartNew()
$summary = @()

foreach ($p in $projects) {
    Write-Host "==== Collect $($p.Key) ====" -ForegroundColor Cyan

    if (-not $SkipBuild) { Build-Project $p }

    $dll = Get-ProjectDllPath $p
    if (-not (Test-Path $dll)) {
        throw "DLL not found at '$dll'. Did the build run? Drop -SkipBuild."
    }

    $logFile = Join-Path $script:LogsDir "collect-$($p.Key).log"
    $errFile = Join-Path $script:LogsDir "collect-$($p.Key).err.log"

    $ctsArgs = @(
        'collect','vstest',
        '--rootPath',                    $script:RepoRoot,
        '--config',                      $script:ConfigPath,
        '--storage-type',                'filesystem',
        '--storage-type-filesystem-dir', $script:BaselineDir,
        '--tag',                         (Get-ProjectTag $p),
        '--logs-directory',              $script:LogsDir,
        '--filter',                      (Get-ProjectDllFilter $p),
        '--dop',                         $Dop,
        '--coverage',
        '--print-console-output'
    )

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process -FilePath cts -ArgumentList (ConvertTo-ProcessArgumentList $ctsArgs) `
        -RedirectStandardOutput $logFile -RedirectStandardError $errFile -PassThru -NoNewWindow
    if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        $sw.Stop()
        Write-Host "  TIMEOUT after $TimeoutMinutes min" -ForegroundColor Red
        Get-Content $logFile -Tail 30 | ForEach-Object { Write-Host "    $_" }
        $summary += [pscustomobject]@{ Project = $p.Key; Status = 'TIMEOUT'; Duration = $sw.Elapsed }
        continue
    }
    $sw.Stop()

    $status = if ($proc.ExitCode -eq 0) { 'OK' } else { "EXIT $($proc.ExitCode)" }
    $color  = if ($proc.ExitCode -eq 0) { 'Green' } else { 'Red' }
    Write-Host "  $status ($([int]$sw.Elapsed.TotalSeconds)s) -> $logFile" -ForegroundColor $color
    if ($proc.ExitCode -ne 0) {
        Get-Content $logFile -Tail 15 | ForEach-Object { Write-Host "    $_" }
    }
    $summary += [pscustomobject]@{ Project = $p.Key; Status = $status; Duration = $sw.Elapsed }
}

$totalSw.Stop()
Write-Host ""
Write-Host "==== Summary ($([int]$totalSw.Elapsed.TotalSeconds)s total) ====" -ForegroundColor Cyan
$summary | Format-Table -AutoSize | Out-Host
Write-Host "Baseline cache: $script:BaselineDir" -ForegroundColor DarkGray

if ($summary | Where-Object { $_.Status -ne 'OK' }) { exit 1 } else { exit 0 }
