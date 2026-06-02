param([string]$Tag = "vstest-stringtools", [int]$TimeoutMinutes = 8)
$ErrorActionPreference = 'Stop'
$sampleRoot = $PSScriptRoot
$repoRoot = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$baselineDir = "$sampleRoot\.cts-baseline"
$logsDir = "$sampleRoot\.cts-logs"
if (Test-Path $baselineDir) { Remove-Item -Recurse -Force $baselineDir }
if (Test-Path $logsDir) { Remove-Item -Recurse -Force $logsDir }
New-Item -ItemType Directory -Force -Path $baselineDir, $logsDir | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath cts -ArgumentList @(
    "collect","vstest",
    "--rootPath",$repoRoot,
    "--config","$sampleRoot\ctsconfig.json",
    "--storage-type","filesystem",
    "--storage-type-filesystem-dir",$baselineDir,
    "--tag",$Tag,
    "--logs-directory",$logsDir,
    "--filter","eng/cts/vstest-stringtools/bin/Debug/net10.0/Microsoft.NET.StringTools.UnitTests.dll",
    "--dop","1",
    "--print-console-output"
) -RedirectStandardOutput "$sampleRoot\cts.log" -RedirectStandardError "$sampleRoot\cts.err.log" -PassThru -NoNewWindow
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    $pid_ = $proc.Id
    Write-Host "TIMEOUT after $TimeoutMinutes min - killing pid $pid_" -ForegroundColor Red
    Stop-Process -Id $pid_ -Force -ErrorAction SilentlyContinue
    $sw.Stop()
    Write-Host "Result: HANG ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor Red
    Get-Content "$sampleRoot\cts.log" -Tail 50
    exit 124
}
$sw.Stop()
Write-Host "exit=$($proc.ExitCode) duration=$([int]$sw.Elapsed.TotalSeconds)s"
Write-Host "--- stdout tail ---"
Get-Content "$sampleRoot\cts.log" -Tail 60
exit $proc.ExitCode
