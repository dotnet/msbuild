<#
.SYNOPSIS
    Runs only the tests CTS considers impacted by the current working tree
    against the *.UnitTests.VSTest projects.

.DESCRIPTION
    Runs `cts apply vstest --local-development` per project. Requires a
    baseline from .\Collect-Local.ps1.

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

if (-not (Test-Path $script:BaselineDir) -or
    -not (Get-ChildItem -Path $script:BaselineDir -Recurse -ErrorAction SilentlyContinue)) {
    throw "No CTS baseline at '$script:BaselineDir'. Run .\Collect-Local.ps1 first."
}

$projects = Get-Projects -Filter $Project
$null = New-Item -ItemType Directory -Force -Path $script:LogsDir

$totalSw = [Diagnostics.Stopwatch]::StartNew()
$summary = @()

foreach ($p in $projects) {
    Write-Host "==== Apply $($p.Key) ====" -ForegroundColor Cyan

    if (-not $SkipBuild) { Build-Project $p }

    $dll = Get-ProjectDllPath $p
    if (-not (Test-Path $dll)) {
        throw "DLL not found at '$dll'. Did the build run? Drop -SkipBuild."
    }

    $logFile = Join-Path $script:LogsDir "apply-$($p.Key).log"
    $errFile = Join-Path $script:LogsDir "apply-$($p.Key).err.log"

    $ctsArgs = @(
        'apply','vstest',
        '--rootPath',                    $script:RepoRoot,
        '--config',                      $script:ConfigPath,
        '--storage-type',                'filesystem',
        '--storage-type-filesystem-dir', $script:BaselineDir,
        '--tag',                         (Get-ProjectTag $p),
        '--local-development',
        '--logs-directory',              $script:LogsDir,
        '--filter',                      (Get-ProjectDllFilter $p),
        '--dop',                         $Dop,
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

    Get-Content $logFile -Tail 30 |
        Where-Object { $_ -match 'impacted|executed|succeeded|failed|reason ' } |
        ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }

    if ($proc.ExitCode -ne 0) {
        Get-Content $logFile -Tail 10 | ForEach-Object { Write-Host "    $_" }
    }
    $summary += [pscustomobject]@{ Project = $p.Key; Status = $status; Duration = $sw.Elapsed }
}

$totalSw.Stop()
Write-Host ""
Write-Host "==== Summary ($([int]$totalSw.Elapsed.TotalSeconds)s total) ====" -ForegroundColor Cyan
$summary | Format-Table -AutoSize | Out-Host

if ($summary | Where-Object { $_.Status -ne 'OK' }) { exit 1 } else { exit 0 }
