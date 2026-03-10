<#
.SYNOPSIS
    Builds external repos and projects using bootstrap MSBuild to validate callback changes.

.DESCRIPTION
    Clones dotnet/roslyn to Q:\, creates a WPF test project, then builds them using the
    locally-built bootstrap MSBuild with TaskHost callbacks enabled and multithreaded mode (-mt).
    Re-run after rebuilding bootstrap to validate new changes.

    WPF repo itself requires SDK 11.0 so we test with 'dotnet new wpf' instead.

.PARAMETER Repos
    Which to build. Default: both. Options: 'roslyn', 'wpf', 'both'

.PARAMETER SkipClone
    Skip cloning if repos already exist at Q:\roslyn and Q:\wpf.

.PARAMETER CleanBuild
    Delete artifacts/bin before building to force a clean build.

.EXAMPLE
    # First run: clone + build both
    .\scripts\test-external-repos.ps1

    # Re-run after rebuilding bootstrap (skip clone, just rebuild)
    .\scripts\test-external-repos.ps1 -SkipClone

    # Build only roslyn
    .\scripts\test-external-repos.ps1 -Repos roslyn -SkipClone
#>
param(
    [ValidateSet('roslyn', 'wpf', 'both')]
    [string]$Repos = 'both',

    [switch]$SkipClone,

    [switch]$CleanBuild
)

$ErrorActionPreference = 'Continue'

# --- Paths ---
$msbuildRoot   = "C:\Users\janprovaznik\dev\msbuild"
$bootstrapRoot = "$msbuildRoot\artifacts\bin\bootstrap"
$bootstrapDotnet = "$bootstrapRoot\core\dotnet.exe"
$bootstrapMSBuildExe = "$bootstrapRoot\net472\MSBuild\Current\Bin\MSBuild.exe"

# Find the SDK version in bootstrap
$sdkDir = Get-ChildItem "$bootstrapRoot\core\sdk" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$bootstrapSdkPath = $sdkDir.FullName

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " External Repo Build Validation" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Bootstrap dotnet : $bootstrapDotnet"
Write-Host "Bootstrap SDK    : $bootstrapSdkPath"
Write-Host "Bootstrap MSBuild: $bootstrapMSBuildExe"
Write-Host ""

# Verify bootstrap exists
if (-not (Test-Path $bootstrapDotnet)) {
    Write-Error "Bootstrap dotnet not found at $bootstrapDotnet. Run 'build.cmd' first."
    exit 1
}

# --- Environment: enable callbacks + multithreaded ---
$env:MSBUILDENABLETASKHOSTCALLBACKS = "1"
$env:DOTNET_ROOT = "$bootstrapRoot\core"
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = "$bootstrapRoot\core"
# Use bootstrap dotnet for SDK resolution
$env:PATH = "$bootstrapRoot\core;$env:PATH"

Write-Host "Environment:" -ForegroundColor Yellow
Write-Host "  MSBUILDENABLETASKHOSTCALLBACKS = 1"
Write-Host "  DOTNET_ROOT = $env:DOTNET_ROOT"
Write-Host ""

# --- Helper: Clone or update repo ---
function Ensure-Repo {
    param([string]$Org, [string]$Name, [string]$TargetDir)

    if (Test-Path $TargetDir) {
        if ($SkipClone) {
            Write-Host "[$Name] Using existing clone at $TargetDir" -ForegroundColor Green
            return
        }
        Write-Host "[$Name] Removing existing clone..." -ForegroundColor Yellow
        Remove-Item -Recurse -Force $TargetDir
    }

    Write-Host "[$Name] Cloning $Org/$Name to $TargetDir (shallow)..." -ForegroundColor Yellow
    git clone --depth 1 "https://github.com/$Org/$Name.git" $TargetDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to clone $Org/$Name"
        return $false
    }
    return $true
}

# --- Helper: Build a repo ---
function Build-Repo {
    param(
        [string]$Name,
        [string]$RepoDir,
        [string]$BuildCommand
    )

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host " Building $Name" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan

    Push-Location $RepoDir
    try {
        if ($CleanBuild -and (Test-Path "artifacts\bin")) {
            Write-Host "[$Name] Cleaning artifacts..." -ForegroundColor Yellow
            Remove-Item -Recurse -Force "artifacts\bin" -ErrorAction SilentlyContinue
        }

        $logFile = "Q:\${Name}-build.binlog"
        Write-Host "[$Name] Build command: $BuildCommand" -ForegroundColor Yellow
        Write-Host "[$Name] Binlog: $logFile" -ForegroundColor Yellow
        Write-Host ""

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-Expression $BuildCommand | Out-Host
        $exitCode = $LASTEXITCODE
        $sw.Stop()

        if ($exitCode -eq 0) {
            Write-Host ""
            Write-Host "[$Name] BUILD SUCCEEDED in $($sw.Elapsed.ToString('mm\:ss'))" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "[$Name] BUILD FAILED (exit code $exitCode) after $($sw.Elapsed.ToString('mm\:ss'))" -ForegroundColor Red
            Write-Host "[$Name] Check binlog: $logFile" -ForegroundColor Red
        }

        return $exitCode
    } finally {
        Pop-Location
    }
}

# --- Ensure Q: exists ---
if (-not (Test-Path "Q:\")) {
    Write-Error "Q:\ drive does not exist. Please create or mount it first."
    exit 1
}

$results = @{}

# --- Roslyn ---
if ($Repos -eq 'roslyn' -or $Repos -eq 'both') {
    $roslynDir = "Q:\roslyn"
    Ensure-Repo -Org "dotnet" -Name "roslyn" -TargetDir $roslynDir

    if (Test-Path $roslynDir) {
        # Patch global.json to allow rollForward from our bootstrap SDK
        $globalJsonPath = "$roslynDir\global.json"
        $globalJson = Get-Content $globalJsonPath -Raw | ConvertFrom-Json
        $globalJson.sdk.rollForward = "latestFeature"
        $globalJson | ConvertTo-Json -Depth 10 | Set-Content $globalJsonPath

        # Build Roslyn Compilers solution filter (smaller, faster than full Roslyn.slnx)
        # -mt enables multithreaded mode which ejects non-thread-safe tasks to TaskHost.
        $roslynBuildCmd = "& `"$bootstrapDotnet`" build `"$roslynDir\Compilers.slnf`" -c Debug -mt /bl:`"Q:\roslyn-build.binlog`" -v:m --no-restore"

        # Restore with bootstrap dotnet
        Write-Host "[roslyn] Restoring Compilers.slnf..." -ForegroundColor Yellow
        Push-Location $roslynDir
        & $bootstrapDotnet restore Compilers.slnf -v:m 2>&1 | Select-Object -Last 10
        $restoreExit = $LASTEXITCODE
        Pop-Location

        if ($restoreExit -ne 0) {
            Write-Host "[roslyn] RESTORE FAILED (exit code $restoreExit)" -ForegroundColor Red
            $results['roslyn'] = $restoreExit
        } else {
            $results['roslyn'] = Build-Repo -Name "roslyn" -RepoDir $roslynDir -BuildCommand $roslynBuildCmd
        }
    }
}

# --- WPF (dotnet new wpf) ---
if ($Repos -eq 'wpf' -or $Repos -eq 'both') {
    $wpfDir = "Q:\wpf-testproject"

    if (-not (Test-Path $wpfDir) -or -not $SkipClone) {
        Write-Host "[wpf] Creating new WPF project at $wpfDir..." -ForegroundColor Yellow
        if (Test-Path $wpfDir) { Remove-Item -Recurse -Force $wpfDir }
        New-Item -ItemType Directory -Path $wpfDir -Force | Out-Null

        # Use system dotnet for scaffolding — temporarily unset bootstrap env to avoid conflicts
        $savedDotnetRoot = $env:DOTNET_ROOT
        $savedResolverDir = $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR
        $env:DOTNET_ROOT = $null
        $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $null

        Push-Location $wpfDir
        & "C:\Program Files\dotnet\dotnet.exe" new wpf -n WpfTestApp --force 2>&1 | Write-Host
        Pop-Location

        # Restore bootstrap env
        $env:DOTNET_ROOT = $savedDotnetRoot
        $env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $savedResolverDir
    } else {
        Write-Host "[wpf] Using existing WPF project at $wpfDir" -ForegroundColor Green
    }

    if (Test-Path "$wpfDir\WpfTestApp") {
        $wpfBuildCmd = "& `"$bootstrapDotnet`" build `"$wpfDir\WpfTestApp\WpfTestApp.csproj`" -c Debug -mt /bl:`"Q:\wpf-build.binlog`" -v:m"

        $results['wpf'] = Build-Repo -Name "wpf" -RepoDir "$wpfDir\WpfTestApp" -BuildCommand $wpfBuildCmd
    } else {
        Write-Host "[wpf] Project creation failed." -ForegroundColor Red
        $results['wpf'] = 1
    }
}

# --- Summary ---
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Summary" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

foreach ($repo in $results.Keys) {
    $code = $results[$repo]
    $status = if ($code -eq 0) { "PASS" } else { "FAIL ($code)" }
    $color = if ($code -eq 0) { "Green" } else { "Red" }
    Write-Host "  $repo : $status" -ForegroundColor $color
}

Write-Host ""
Write-Host "Binlogs at Q:\*-build.binlog" -ForegroundColor Yellow
Write-Host "Re-run with -SkipClone after rebuilding bootstrap." -ForegroundColor Yellow
