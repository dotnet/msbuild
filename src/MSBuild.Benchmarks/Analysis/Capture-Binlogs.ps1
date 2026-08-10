#requires -Version 5.1
<#
Captures one binlog per scenario so the wall-clock matrix can be decomposed into
evaluation versus execution versus process overhead.

Run this AFTER the wall-clock matrix, never alongside it.
#>
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$ProjectOrSln,
    [Parameter(Mandatory)][string]$TouchFile,
    [string[]]$CleanRoots,
    [switch]$SkipClean,
    [string]$Dotnet = 'C:\dotnet-daily\dotnet.exe',
    [string]$OutDir = 'C:\bench\results'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

New-Item -ItemType Directory -Force -Path $OutDir *> $null

function Stop-Servers {
    & $Dotnet build-server shutdown *> $null
    Start-Sleep -Milliseconds 900
    $procs = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='MSBuild.exe'" |
               Where-Object { $_.CommandLine -match 'nodemode' })
    foreach ($p in $procs) { try { Stop-Process -Id $p.ProcessId -Force -EA Stop } catch {} }
    Start-Sleep -Milliseconds 400
}

function Build-WithLogs {
    param([string]$Tag)
    $bl   = Join-Path $OutDir "$Name-$Tag.binlog"
    $prof = Join-Path $OutDir "$Name-$Tag.profile.md"
    Remove-Item $bl, $prof -EA 0
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $Dotnet build $ProjectOrSln --no-restore -v:q --nologo -mt "-bl:$bl" "-profileevaluation:$prof" *> $null
    $sw.Stop()
    Write-Host ("  {0,-12} {1,7:F0} ms wall (instrumented)   exit={2}" -f $Tag, $sw.Elapsed.TotalMilliseconds, $LASTEXITCODE)
}

function Remove-Outputs {
    foreach ($root in $CleanRoots) {
        Get-ChildItem $root -Include bin, obj -Directory -Recurse -EA 0 |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force -EA 0 }
    }
}

function Restore-Untimed { & $Dotnet restore $ProjectOrSln *> $null }

Write-Host "=== $Name (instrumented) ==="

Restore-Untimed
Stop-Servers
& $Dotnet build $ProjectOrSln --no-restore -v:q --nologo -mt *> $null
& $Dotnet build $ProjectOrSln --no-restore -v:q --nologo -mt *> $null

Build-WithLogs 'warm-noop'

(Get-Item $TouchFile).LastWriteTime = Get-Date
Start-Sleep -Milliseconds 60
Build-WithLogs 'warm-inc'

Stop-Servers
Build-WithLogs 'cold-noop'

if (-not $SkipClean) {
    Remove-Outputs; Restore-Untimed
    & $Dotnet build $ProjectOrSln --no-restore -v:q --nologo -mt *> $null
    Remove-Outputs; Restore-Untimed
    Build-WithLogs 'warm-clean'

    Remove-Outputs; Restore-Untimed; Stop-Servers
    Build-WithLogs 'cold-clean'
}

Stop-Servers
Write-Host "  binlogs and evaluation profiles in $OutDir"
