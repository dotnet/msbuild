<#
.SYNOPSIS
    Collects a CTS baseline against the *.UnitTests.VSTest projects.

.DESCRIPTION
    For each VSTest variant test project (see scripts/cts/_Common.ps1), this
    script runs `cts collect vstest`, populating the local filesystem cache
    under <repo>/.cts/baseline. CTS keys the baseline by HEAD SHA, so the
    working tree must be clean.

.PARAMETER Project
    Optional short key (e.g. StringTools, Framework, Engine) to collect just
    one project. Default is to collect all.

.PARAMETER SkipBuild
    Reuse already-built VSTest DLLs.

.PARAMETER TimeoutMinutes
    Per-project timeout for the CTS collect step. Default 15.

.PARAMETER Dop
    Degree of parallelism for CTS. Default 4 (CTS default is 32; lower
    when running locally to avoid CPU saturation).

.EXAMPLE
    .\Collect-Local.ps1
    .\Collect-Local.ps1 -Project StringTools
    .\Collect-Local.ps1 -SkipBuild
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
Assert-CleanRepo

$projects = Get-Projects -Filter $Project
$null = New-Item -ItemType Directory -Force -Path $script:BaselineDir, $script:LogsDir

$totalSw = [Diagnostics.Stopwatch]::StartNew()
$summary = @()

foreach ($p in $projects) {
    Write-Host "==== Collect $($p.Key) ====" -ForegroundColor Cyan

    if (-not $SkipBuild) {
        Build-Project $p
    }

    $dll = Get-ProjectDllPath $p
    if (-not (Test-Path $dll)) {
        throw "DLL not found at '$dll'. Did the build run? Drop -SkipBuild."
    }

    $tag      = "local-$($p.Key.ToLower())"
    $logFile  = Join-Path $script:LogsDir "collect-$($p.Key).log"
    $errFile  = Join-Path $script:LogsDir "collect-$($p.Key).err.log"

    $ctsArgs = @(
        'collect','vstest',
        '--rootPath',           $script:RepoRoot,
        '--config',             $script:ConfigPath,
        '--storage-type',       'filesystem',
        '--storage-type-filesystem-dir', $script:BaselineDir,
        '--tag',                $tag,
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
