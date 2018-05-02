[CmdletBinding(PositionalBinding=$false)]
Param(
  [switch] $build,
  [switch] $ci,
  [string] $configuration = "Debug",
  [switch] $deploy,
  [switch] $dogfood,
  [switch] $fullMSBuild,
  [switch] $help,
  [switch] $log,
  [switch] $pack,
  [switch] $prepareMachine,
  [switch] $rebuild,
  [switch] $restore,
  [switch] $sign,
  [string] $solution = "",
  [switch] $test,
  [switch] $perf,
  [string] $verbosity = "minimal",
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Print-Usage() {
    Write-Host "Common settings:"
    Write-Host "  -configuration <value>  Build configuration Debug, Release"
    Write-Host "  -verbosity <value>      Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
    Write-Host "  -help                   Print help and exit"
    Write-Host ""
    Write-Host "Actions:"
    Write-Host "  -restore                Restore dependencies"
    Write-Host "  -build                  Build solution"
    Write-Host "  -rebuild                Rebuild solution"
    Write-Host "  -deploy                 Deploy built VSIXes"
    Write-Host "  -test                   Run all unit tests in the solution"
    Write-Host "  -perf                   Run all performance tests in the solution"
    Write-Host "  -sign                   Sign build outputs"
    Write-Host "  -pack                   Package build outputs into NuGet packages and Willow components"
    Write-Host ""
    Write-Host "Advanced settings:"
    Write-Host "  -dogfood                Setup a dogfood environment using the local build"
    Write-Host "                          This ignores any actions (such as -build or -restore) that were specified"
    Write-Host "                          If any additional arguments are specified, they will be interpreted as a"
    Write-Host "                          command to be run in the dogfood context.  If no additional arguments are"
    Write-Host "                          specified, then the value of the DOTNET_SDK_DOGFOOD_SHELL environment, if"
    Write-Host "                          it is set, will be used."
    Write-Host "  -solution <value>       Path to solution to build"
    Write-Host "  -ci                     Set when running on CI server"
    Write-Host "  -log                    Enable logging (by default on CI)"
    Write-Host "  -prepareMachine         Prepare machine for CI run"
    Write-Host "  -fullMSBuild            Test against the desktop version of MSBuild"
    Write-Host ""
    Write-Host "Command line arguments not listed above are passed through to MSBuild."
    Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
}

if ($help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
  Print-Usage
  exit 0
}

function Create-Directory([string[]] $Path) {
  if (!(Test-Path -Path $Path)) {
    New-Item -Path $Path -Force -ItemType "Directory" | Out-Null
  }
}

function GetVersionsPropsVersion([string[]] $Name) {
  [xml]$Xml = Get-Content $VersionsProps

  foreach ($PropertyGroup in $Xml.Project.PropertyGroup) {
    if (Get-Member -InputObject $PropertyGroup -name $Name) {
        return $PropertyGroup.$Name
    }
  }

  throw "Failed to locate the $Name property"
}

function InitializeDotNetCli {
  # Don't resolve runtime, shared framework, or SDK from other locations to ensure build determinism
  $env:DOTNET_MULTILEVEL_LOOKUP=0
  
  # Disable first run since we do not need all ASP.NET packages restored.
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Source Build uses DotNetCoreSdkDir variable
  if ($env:DotNetCoreSdkDir -ne $null) {
    $env:DOTNET_INSTALL_DIR = $env:DotNetCoreSdkDir    
  }

  # Use dotnet installation specified in DOTNET_INSTALL_DIR if it contains the required SDK version, 
  # otherwise install the dotnet CLI and SDK to repo local .dotnet directory to avoid potential permission issues.
  if (($env:DOTNET_INSTALL_DIR -ne $null) -and (Test-Path(Join-Path $env:DOTNET_INSTALL_DIR "sdk\$($GlobalJson.sdk.version)"))) {
    $dotnetRoot = $env:DOTNET_INSTALL_DIR
  } else {
    $dotnetRoot = Join-Path $RepoRoot ".dotnet"
    $env:DOTNET_INSTALL_DIR = $dotnetRoot
    
    if ($restore) {
      InstallDotNetSdk $dotnetRoot $DotNetCliVersion
    }
  }

  $global:BuildDriver = Join-Path $dotnetRoot "dotnet.exe"    
  $global:BuildArgs = "msbuild"
}

function InstallDotNetSdk([string] $dotnetRoot, [string] $version) {
  $installScript = GetDotNetInstallScript $dotnetRoot
  
  & $installScript -Version $version -InstallDir $dotnetRoot
  if ($lastExitCode -ne 0) {
    throw "Failed to install dotnet SDK $version to '$dotnetRoot' (exit code '$lastExitCode')."
  }
}

function GetDotNetInstallScript([string] $dotnetRoot) {
  $installScript = "$dotnetRoot\dotnet-install.ps1"
  if (!(Test-Path $installScript)) { 
    Create-Directory $dotnetRoot
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
  }

  return $installScript
}

function InstallDotNetSharedFramework([string]$dotnetRoot, [string]$version) {
  $fxDir = Join-Path $dotnetRoot "shared\Microsoft.NETCore.App\$version"

  if (!(Test-Path $fxDir)) {
    $installScript = GetDotNetInstallScript $dotnetRoot
    & $installScript -Version $version -InstallDir $dotnetRoot -SharedRuntime

    if($lastExitCode -ne 0) {
      throw "Failed to install shared Framework $version to '$dotnetRoot' (exit code '$lastExitCode')."
    }
  }
}

function InitializeCustomToolset {    
  if ($fullMSBuild) {
    if (!($env:VSInstallDir)) {
      $env:VSInstallDir = LocateVisualStudio
    }

    $env:DOTNET_SDK_TEST_MSBUILD_PATH = Join-Path $env:VSInstallDir "MSBuild\15.0\Bin\msbuild.exe"
  }

  if ($dogfood)
  {
    $env:SDK_REPO_ROOT = $RepoRoot
    $env:SDK_CLI_VERSION = $DotNetCliVersion
    $env:MSBuildSDKsPath = Join-Path $ArtifactsConfigurationDir "bin\Sdks"
    $env:DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR = $env:MSBuildSDKsPath
    $env:NETCoreSdkBundledVersionsProps = Join-Path $env:DOTNET_INSTALL_DIR "sdk\$DotNetCliVersion\Microsoft.NETCoreSdk.BundledVersions.props"
    $env:MicrosoftNETBuildExtensionsTargets = Join-Path $env:MSBuildSDKsPath "Microsoft.NET.Build.Extensions\msbuildExtensions\Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets"
 
    if ($properties -eq $null -and $env:DOTNET_SDK_DOGFOOD_SHELL -ne $null)
    {
      $properties = , $env:DOTNET_SDK_DOGFOOD_SHELL
    }
    if ($properties -ne $null)
    {
      $Host.UI.RawUI.WindowTitle = "SDK Test ($RepoRoot) ($configuration)"
      & $properties[0] $properties[1..($properties.Length-1)]
    }
  }

  if (-not $restore) {
    return
  }

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them in source build.
  if ($env:DotNetBuildFromSource -eq "true") {
    return
  }
  
  $dotnetRoot = $env:DOTNET_INSTALL_DIR

  InstallDotNetSharedFramework $dotnetRoot "1.0.5"
  InstallDotNetSharedFramework $dotnetRoot "1.1.2"
  InstallDotNetSharedFramework $dotnetRoot "2.0.0"

  CreateBuildEnvScript
  InstallNuget
}

function InstallNuGet {
  $NugetInstallDir = Join-Path $ArtifactsDir ".nuget"
  $NugetExe = Join-Path $NugetInstallDir "nuget.exe"

  if (!(Test-Path -Path $NugetExe)) {
    Create-Directory $NugetInstallDir
    Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -UseBasicParsing -OutFile $NugetExe
  }
}

function InitializeToolset {
  $toolsetVersion = $GlobalJson.'msbuild-sdks'.'RoslynTools.RepoToolset'
  $toolsetLocationFile = Join-Path $ToolsetDir "$toolsetVersion.txt"

  if (Test-Path $toolsetLocationFile) {
    $path = Get-Content $toolsetLocationFile
    if (Test-Path $path) {
      $global:ToolsetBuildProj = $path
      return
    }
  }

  if (-not $restore) {
    throw "Toolset version $toolsetVersion has not been restored."
  }

  $proj = Join-Path $ToolsetDir "restore.proj"  

  '<Project Sdk="RoslynTools.RepoToolset"/>' | Set-Content $proj
  & $BuildDriver $BuildArgs $proj /t:__WriteToolsetLocation /m /nologo /clp:None /warnaserror /bl:$ToolsetRestoreLog /v:$verbosity /p:__ToolsetLocationOutputFile=$toolsetLocationFile
    
  if ($lastExitCode -ne 0) {
    throw "Failed to restore toolset (exit code '$lastExitCode'). See log: $ToolsetRestoreLog"
  }

  $global:ToolsetBuildProj = Get-Content $toolsetLocationFile
}

function LocateVisualStudio {
  $VSWhereVersion = $GlobalJson.vswhere.version
  $VSWhereDir = Join-Path $ArtifactsDir ".tools\vswhere\$VSWhereVersion"
  $VSWhereExe = Join-Path $vsWhereDir "vswhere.exe"

  if (!(Test-Path $VSWhereExe)) {
    Create-Directory $VSWhereDir
    Invoke-WebRequest "http://github.com/Microsoft/vswhere/releases/download/$VSWhereVersion/vswhere.exe" -UseBasicParsing -OutFile $VSWhereExe
  }

  $VSInstallDir = & $VSWhereExe -latest -property installationPath -requires Microsoft.Component.MSBuild -requires Microsoft.VisualStudio.Component.VSSDK -requires Microsoft.Net.Component.4.6.TargetingPack -requires Microsoft.VisualStudio.Component.Roslyn.Compiler -requires Microsoft.VisualStudio.Component.VSSDK

  if (!(Test-Path $VSInstallDir)) {
    throw "Failed to locate Visual Studio (exit code '$LASTEXITCODE')."
  }

  return $VSInstallDir
}

function Build {
  & $BuildDriver $BuildArgs $ToolsetBuildProj /m /nologo /clp:Summary /warnaserror /v:$verbosity /bl:$BuildLog /p:Configuration=$configuration /p:Projects=$solution /p:RepoRoot=$RepoRoot /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci $properties

  if ($lastExitCode -ne 0) {
    throw "Failed to build (exit code '$lastExitCode'). See log: $BuildLog"
  }
}

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

function CreateBuildEnvScript()
{
  Create-Directory $ArtifactsDir
  $scriptPath = Join-Path $ArtifactsDir "sdk-build-env.bat"
  $scriptContents = @"
@echo off
title SDK Build ($RepoRoot)
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_MULTILEVEL_LOOKUP=0

set PATH=$env:DOTNET_INSTALL_DIR;%PATH%
set NUGET_PACKAGES=$env:NUGET_PACKAGES
"@

  Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII
}

try {
  $RepoRoot = Join-Path $PSScriptRoot ".."
  $RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot);

  $ArtifactsDir = $env:DOTNET_SDK_ARTIFACTS_DIR
  if (!($ArtifactsDir)) {
    $ArtifactsDir = Join-Path $RepoRoot "artifacts"
  }

  $ArtifactsConfigurationDir = Join-Path $ArtifactsDir $configuration
  $ToolsetDir = Join-Path $ArtifactsDir "toolset"
  $LogDir = Join-Path $ArtifactsConfigurationDir "log"
  $BuildLog = Join-Path $LogDir "Build.binlog"
  $ToolsetRestoreLog = Join-Path $LogDir "ToolsetRestore.binlog"
  $TempDir = Join-Path $ArtifactsConfigurationDir "tmp"
  $GlobalJson = Get-Content(Join-Path $RepoRoot "global.json") | ConvertFrom-Json
  $DotNetCliVersion = $GlobalJson.sdk.version

  if ($solution -eq "") {
    $solution = Join-Path $RepoRoot "*.sln"
  }

  if ($env:NUGET_PACKAGES -eq $null) {
    # Use local cache on CI to ensure deterministic build,
    # use global cache in dev builds to avoid cost of downloading packages.
    $env:NUGET_PACKAGES = if ($ci) { Join-Path $RepoRoot ".packages" } 
                          else { Join-Path $env:UserProfile ".nuget\packages" }
  }

  Create-Directory $ToolsetDir
  Create-Directory $LogDir
  
  if ($ci) {
    Create-Directory $TempDir
    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }

  InitializeDotNetCli
  InitializeToolset
  InitializeCustomToolset

  if (-not $dogfood)
  {
    Build
  }
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
  Pop-Location
  if ($ci -and $prepareMachine) {
    Stop-Processes
  }
}
