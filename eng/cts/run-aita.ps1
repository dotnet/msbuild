param([string]$Tag = "aitestagent-coverage", [int]$TimeoutMinutes = 6)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$bisectRoot = "$repoRoot\artifacts\cts\bisect"
$baselineDir = "$bisectRoot\baseline-$Tag"
$logsDir = "$bisectRoot\logs-$Tag"
$runLog = "$bisectRoot\$Tag.log"
if (Test-Path $baselineDir) { Remove-Item -Recurse -Force $baselineDir }
if (Test-Path $logsDir) { Remove-Item -Recurse -Force $logsDir }
New-Item -ItemType Directory -Force -Path $baselineDir, $logsDir | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath cts -ArgumentList @(
    "collect","testingplatform",
    "--rootPath",$repoRoot,
    "--config","$repoRoot\cts.json",
    "--storage-type","filesystem",
    "--storage-type-filesystem-dir",$baselineDir,
    "--tag",$Tag,
    "--logs-directory",$logsDir,
    "--results-directory",$logsDir,
    "--filter","artifacts/bin/StringTools.UnitTests/Debug/net10.0/Microsoft.NET.StringTools.UnitTests.exe",
    "--coverage",
    "--coverage-settings","$repoRoot\eng\cts\coverage.config",
    "--report-trx"
) -RedirectStandardOutput $runLog -RedirectStandardError "$runLog.err" -PassThru -NoNewWindow
$timeoutMs = $TimeoutMinutes * 60 * 1000
if (-not $proc.WaitForExit($timeoutMs)) {
    $pid_ = $proc.Id
    Write-Host "TIMEOUT after $TimeoutMinutes min - killing pid $pid_" -ForegroundColor Red
    Stop-Process -Id $pid_ -Force -ErrorAction SilentlyContinue
    $sw.Stop()
    Write-Host "Result: HANG ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor Red
    exit 124
}
$sw.Stop()
Write-Host "Result: exit=$($proc.ExitCode) ($([int]$sw.Elapsed.TotalSeconds)s)"
Get-Content $runLog -Tail 25
exit $proc.ExitCode
