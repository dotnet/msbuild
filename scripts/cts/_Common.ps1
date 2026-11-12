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
#
# These helpers target the local Windows developer workflow. On non-Windows
# we early-out with a clear message — the CI pipelines invoke `cts` directly
# and do not source this file.

$ErrorActionPreference = 'Stop'

if (-not $IsWindows -and $PSVersionTable.PSEdition -ne 'Desktop') {
    throw "scripts/cts/*.ps1 currently target Windows. The CI pipelines call cts directly; for local use on Linux/macOS, invoke cts manually."
}

# Paths. Use forward slashes everywhere — Join-Path normalises to the host
# separator on Windows and they are valid as-is on Linux/macOS, so the same
# helpers will work if the Windows-only guard above is ever relaxed.
$script:RepoRoot    = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$_dotnetFile        = if ($IsWindows -or $PSVersionTable.PSEdition -eq 'Desktop') { 'dotnet.exe' } else { 'dotnet' }
$script:DotnetExe   = Join-Path (Join-Path $script:RepoRoot '.dotnet') $_dotnetFile
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
    return (Join-Path $script:RepoRoot ("artifacts/bin/{0}/Debug/net10.0/{1}" -f $Project.BinDir, $Project.Dll))
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

function ConvertTo-ProcessArgumentList {
    # Start-Process -ArgumentList joins array items with a single space and does
    # NOT quote them, so any value containing whitespace (e.g. a repo path with
    # spaces) is split into multiple arguments by the target process. Quote each
    # token that needs it and escape embedded quotes so `cts` receives exactly
    # the intended arguments.
    param([Parameter(Mandatory)] [object[]]$Arguments)
    $quoted = foreach ($a in $Arguments) {
        $s = [string]$a
        if ($s -eq '' -or $s -match '[\s"]') {
            '"' + ($s -replace '"', '\"') + '"'
        }
        else {
            $s
        }
    }
    return ($quoted -join ' ')
}

function Assert-CleanRepo {
    Push-Location $script:RepoRoot
    try { $dirty = git status --porcelain; $gitExit = $LASTEXITCODE }
    finally { Pop-Location }
    if ($gitExit -ne 0) { throw "git status failed (exit $gitExit). Is this a git repo with git on PATH?" }
    if ($dirty) {
        Write-Host "Working tree is dirty:" -ForegroundColor Red
        $dirty | ForEach-Object { Write-Host "  $_" }
        throw "CTS collect requires a clean working tree. Commit/stash first."
    }
}
