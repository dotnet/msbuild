<#
.SYNOPSIS
    Run CTS collect against a single test project (or subset) with a timeout to detect hangs.

.PARAMETER Filter
    The --filter glob to pass to CTS. e.g. "**/Debug/**/StringTools.UnitTests.dll"

.PARAMETER Tag
    Label for this bisect run (e.g. "stringtools-only").

.PARAMETER TimeoutMinutes
    How long to wait before killing CTS. Default 8.

.PARAMETER Dop
    --dop value. Default 1.

.EXAMPLE
    .\eng\cts\bisect.ps1 -Filter "**/Debug/net10.0/Microsoft.NET.StringTools.UnitTests.dll" -Tag stringtools-net10
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Filter,
    [Parameter(Mandatory=$true)][string]$Tag,
    [int]$TimeoutMinutes = 8,
    [int]$Dop = 1
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$config = Join-Path $repoRoot 'cts.json'
$bisectRoot = Join-Path $repoRoot 'artifacts\cts\bisect'
$baselineDir = Join-Path $bisectRoot "baseline-$Tag"
$logsDir = Join-Path $bisectRoot "logs-$Tag"
$runLog = Join-Path $bisectRoot "$Tag.log"

if (Test-Path $baselineDir) { Remove-Item -Recurse -Force $baselineDir }
if (Test-Path $logsDir) { Remove-Item -Recurse -Force $logsDir }
New-Item -ItemType Directory -Force -Path $baselineDir, $logsDir | Out-Null

Write-Host "=== Bisect run: $Tag ===" -ForegroundColor Cyan
Write-Host "Filter:  $Filter"
Write-Host "Timeout: $TimeoutMinutes min"
Write-Host "DOP:     $Dop"

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath "cts" -ArgumentList @(
    "collect", "testingplatform",
    "--rootPath", $repoRoot,
    "--config", $config,
    "--storage-type", "filesystem",
    "--storage-type-filesystem-dir", $baselineDir,
    "--tag", $Tag,
    "--logs-directory", $logsDir,
    "--filter", $Filter,
    "--dop", $Dop,
    "--diagnostic",
    "--platform-diagnostic",
    "--platform-diagnostic-verbosity", "trace"
) -RedirectStandardOutput $runLog -RedirectStandardError "$runLog.err" -PassThru -NoNewWindow

$timeoutMs = $TimeoutMinutes * 60 * 1000
if (-not $proc.WaitForExit($timeoutMs)) {
    Write-Host "TIMEOUT after $TimeoutMinutes min - killing cts pid $($proc.Id)" -ForegroundColor Red
    # Capture dump before killing
    $dumpDir = Join-Path $bisectRoot "dumps-$Tag"
    New-Item -ItemType Directory -Force -Path $dumpDir | Out-Null
    Write-Host "Capturing dump to $dumpDir\cts.dmp..."
    & dotnet-dump collect -p $proc.Id -o "$dumpDir\cts.dmp" --type Full 2>&1 | Out-Host
    # Also dump all testhost children
    Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $proc.Id -or $_.Name -like '*testhost*' -or $_.Name -like '*Microsoft.Build*UnitTests*' } | ForEach-Object {
        Write-Host "  Dumping child pid $($_.ProcessId) name $($_.Name)..."
        & dotnet-dump collect -p $_.ProcessId -o "$dumpDir\$($_.Name)-$($_.ProcessId).dmp" --type Full 2>&1 | Out-Host
    }
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    $sw.Stop()
    Write-Host "Result: HANG ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor Red
    exit 124
}
$sw.Stop()
$exit = $proc.ExitCode
if ($exit -eq 0) {
    Write-Host "Result: PASS ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor Green
} else {
    Write-Host "Result: FAIL exit=$exit ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor Yellow
    Write-Host "--- Last 30 lines of $runLog ---"
    Get-Content $runLog -Tail 30
}
exit $exit
