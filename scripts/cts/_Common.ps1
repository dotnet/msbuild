# scripts/cts/_Common.ps1
#
# Shared helpers for Collect-Local.ps1, Run-Local.ps1, and the demo scripts.
# Configuration lives in two JSON files alongside this script:
#
#   projects.json    - the registry of *.UnitTests.VSTest projects
#   cts.config.json  - the CTS configuration (Modules/SourceCodeFiles/Filter/...)
#
# This file intentionally contains only paths and functions; nothing that you
# would tweak as configuration belongs here.

$ErrorActionPreference = 'Stop'

# Paths
$script:RepoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:DotnetExe   = Join-Path $script:RepoRoot '.dotnet\dotnet.exe'
$script:CtsRoot     = Join-Path $script:RepoRoot '.cts'
$script:BaselineDir = Join-Path $script:CtsRoot 'baseline'
$script:LogsDir     = Join-Path $script:CtsRoot 'logs'
$script:ConfigPath  = Join-Path $PSScriptRoot 'cts.config.json'
$script:ProjectsPath= Join-Path $PSScriptRoot 'projects.json'

function Get-Projects {
    param([string]$Filter)
    $all = Get-Content $script:ProjectsPath -Raw | ConvertFrom-Json
    if (-not $Filter) { return $all }
    $match = $all | Where-Object { $_.Key -ieq $Filter -or $_.CsProj -ieq $Filter }
    if (-not $match) {
        throw "Unknown -Project '$Filter'. Known keys: $(($all.Key) -join ', ')"
    }
    return @($match)
}

function Ensure-Cli {
    if (-not (Get-Command cts -ErrorAction SilentlyContinue)) {
        throw "'cts' is not on PATH. Install with: dotnet tool install cts --global --prerelease --add-source https://devdiv.pkgs.visualstudio.com/_packaging/VS/nuget/v3/index.json"
    }
    if (-not (Test-Path $script:DotnetExe)) {
        throw "Local dotnet at '$script:DotnetExe' not found. Run .\build.cmd once to bootstrap."
    }
}

function Build-Project {
    param([Parameter(Mandatory)] $Project)
    $csproj = Join-Path $script:RepoRoot $Project.CsProj
    Write-Host "  build $($Project.Key) ..." -ForegroundColor DarkGray -NoNewline
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & $script:DotnetExe build -c Debug $csproj -f net10.0 -v:q -nologo 2>&1
    $sw.Stop()
    if ($LASTEXITCODE -ne 0) {
        Write-Host " FAIL ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor Red
        $out | Select-Object -Last 30 | ForEach-Object { Write-Host "    $_" }
        throw "Build of $($Project.CsProj) failed with exit $LASTEXITCODE"
    }
    Write-Host " ok ($([int]$sw.Elapsed.TotalSeconds)s)" -ForegroundColor DarkGreen
}

function Get-ProjectDllPath {
    param([Parameter(Mandatory)] $Project)
    return (Join-Path $script:RepoRoot "artifacts\bin\$($Project.BinDir)\Debug\net10.0\$($Project.Dll)")
}

function Get-ProjectDllFilter {
    param([Parameter(Mandatory)] $Project)
    # CTS --filter is a glob relative to --rootPath (repo root).
    return "artifacts/bin/$($Project.BinDir)/Debug/net10.0/$($Project.Dll)"
}

function Get-ProjectTag {
    param([Parameter(Mandatory)] $Project)
    return "local-$($Project.Key.ToLower())"
}

function Assert-CleanRepo {
    Push-Location $script:RepoRoot
    try { $dirty = git status --porcelain }
    finally { Pop-Location }
    if ($dirty) {
        Write-Host "Working tree is dirty:" -ForegroundColor Red
        $dirty | ForEach-Object { Write-Host "  $_" }
        throw "CTS collect requires a clean working tree. Commit/stash first."
    }
}
