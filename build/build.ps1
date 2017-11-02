[CmdletBinding(PositionalBinding=$false)]
Param(
  [switch] $build,
  [switch] $ci,
  [string] $configuration = "Debug",
  [switch] $deploy,
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
  [string] $verbosity = "minimal",
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"

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
    Write-Host "  -sign                   Sign build outputs"
    Write-Host "  -pack                   Package build outputs into NuGet packages and Willow components"
    Write-Host ""
    Write-Host "Advanced settings:"
    Write-Host "  -solution <value>       Path to solution to build"
    Write-Host "  -ci                     Set when running on CI server"
    Write-Host "  -log                    Enable logging (by default on CI)"
    Write-Host "  -prepareMachine         Prepare machine for CI run"
    Write-Host "  -fullMSBuild            Test against the desktop version of MSBuild"
    Write-Host ""
    Write-Host "Command line arguments not listed above are passed through to MSBuild."
    Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
}

function Create-Directory([string[]] $path) {
  if (!(Test-Path -path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function GetDotNetCliVersion {
  [xml]$xml = Get-Content $DependenciesProps

  foreach ($propertyGroup in $xml.Project.PropertyGroup) {
    if (Get-Member -inputObject $propertyGroup -name dotnetCliVersion) {
        return $propertyGroup.DotNetCliVersion
    }
  }

  throw "Failed to locate the .NET CLI Version"
}

function GetVSWhereVersion {
  [xml]$xml = Get-Content $DependenciesProps

  foreach ($propertyGroup in $xml.Project.PropertyGroup) {
    if (Get-Member -inputObject $propertyGroup -name VSWhereVersion)
    {
        return $propertyGroup.VSWhereVersion
    }
  }

  throw "Failed to locate the VSWhere Version"
}

function InstallDotNetCli {
  $dotnetCliVersion = GetDotNetCliVersion
  $dotnetInstallVerbosity = ""

  if (!$env:DOTNET_INSTALL_DIR) {
    $env:DOTNET_INSTALL_DIR = Join-Path $RepoRoot "artifacts\.dotnet\$dotnetCliVersion"
  }

  $dotnetInstallScript = Join-Path $env:DOTNET_INSTALL_DIR "dotnet-install.ps1"

  if (!(Test-Path $dotnetInstallScript)) {
    Create-Directory $env:DOTNET_INSTALL_DIR
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -UseBasicParsing -OutFile $dotnetInstallScript
  }

  if ($verbosity -eq "diagnostic") {
    $dotnetInstallVerbosity = "-Verbose"
  }

  # Install a stage 0
  $sdkInstallDir = Join-Path $env:DOTNET_INSTALL_DIR "sdk\$dotnetCliVersion"

  if (!(Test-Path $sdkInstallDir)) {
    # Use Invoke-Expression so that $dotnetInstallVerbosity is not positionally bound when empty
    Invoke-Expression -Command "$dotnetInstallScript -Version $dotnetCliVersion $dotnetInstallVerbosity"

    if($LASTEXITCODE -ne 0) {
      throw "Failed to install stage0"
    }
  }

  # Install 1.0 shared framework
  $netcoreApp10Version = "1.0.5"
  $netCoreApp10Dir = Join-Path $env:DOTNET_INSTALL_DIR "shared\Microsoft.NETCore.App\$netcoreApp10Version"

  if (!(Test-Path $netCoreApp10Dir)) {
    # Use Invoke-Expression so that $dotnetInstallVerbosity is not positionally bound when empty
    Invoke-Expression -Command "$dotnetInstallScript -Channel `"Preview`" -Version $netcoreApp10Version -SharedRuntime $dotnetInstallVerbosity"

    if($LASTEXITCODE -ne 0) {
      throw "Failed to install stage0"
    }
  }

  # Install 1.1 shared framework
  $netcoreApp11Version = "1.1.2"
  $netcoreApp11Dir = Join-Path $env:DOTNET_INSTALL_DIR "shared\Microsoft.NETCore.App\$netcoreApp11Version"

  if (!(Test-Path $netCoreApp11Dir)) {
    # Use Invoke-Expression so that $dotnetInstallVerbosity is not positionally bound when empty
    Invoke-Expression -Command "$dotnetInstallScript -Channel `"Release/1.1.0`" -Version $netcoreApp11Version -SharedRuntime $dotnetInstallVerbosity"

    if($LASTEXITCODE -ne 0) {
      throw "Failed to install stage0"
    }
  }

  # Put the stage 0 on the path
  $env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"

  # Disable first run since we want to control all package sources
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations
  $env:DOTNET_MULTILEVEL_LOOKUP=0
}

function LocateVisualStudio {
  $vswhereVersion = GetVSWhereVersion
  $vsWhereDir = Join-Path $ToolsRoot "vswhere\$vswhereVersion"
  $vsWhereExe = Join-Path $vsWhereDir "vswhere.exe"

  if (!(Test-Path $vsWhereExe)) {
    Create-Directory $vsWhereDir
    Invoke-WebRequest "http://github.com/Microsoft/vswhere/releases/download/$vswhereVersion/vswhere.exe" -UseBasicParsing -OutFile $vswhereExe
  }

  $vsInstallDir = & $vsWhereExe -latest -property installationPath -requires Microsoft.Component.MSBuild -requires Microsoft.VisualStudio.Component.VSSDK -requires Microsoft.Net.Component.4.6.TargetingPack -requires Microsoft.VisualStudio.Component.Roslyn.Compiler -requires Microsoft.VisualStudio.Component.VSSDK

  if (!(Test-Path $vsInstallDir)) {
    throw "Failed to locate Visual Studio (exit code '$lastExitCode')."
  }

  return $vsInstallDir
}

function Build {
  InstallDotNetCli

  # Preparation of a CI machine
  if ($prepareMachine) {
    Clear-NuGetCache
  }

  if ($fullMSBuild) {
    if (!($env:VSInstallDir)) {
      $env:VSInstallDir = LocateVisualStudio
    }

    $env:DOTNET_SDK_TEST_MSBUILD_PATH = Join-Path $env:VSInstallDir "MSBuild\15.0\Bin\msbuild.exe"
  }

  if ($ci -or $log) {
    Create-Directory($logDir)
    $logCmd = "/bl:" + (Join-Path $LogDir "Build.binlog")
  } else {
    $logCmd = ""
  }

  dotnet msbuild $BuildProj /nologo /clp:Summary /warnaserror /v:$verbosity $logCmd /p:Configuration=$configuration /p:SolutionPath=$solution /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci $properties

  if($LASTEXITCODE -ne 0) {
    throw "Failed to build"
  }
}

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

function Clear-NuGetCache() {
  if (!($env:NUGET_PACKAGES)) {
    $env:NUGET_PACKAGES = (Join-Path $env:USERPROFILE ".nuget\packages")
  }

  Create-Directory $env:NUGET_PACKAGES
  dotnet nuget locals all --clear
}

if ($help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
  Print-Usage
  exit 0
}

$RepoRoot = Join-Path $PSScriptRoot "..\"
$ToolsRoot = Join-Path $RepoRoot "artifacts\.tools"
$BuildProj = Join-Path $PSScriptRoot "build.proj"
$DependenciesProps = Join-Path $PSScriptRoot "Versions.props"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$LogDir = Join-Path (Join-Path $ArtifactsDir $configuration) "log"
$TempDir = Join-Path (Join-Path $ArtifactsDir $configuration) "tmp"

try {
  if ($ci) {
    Create-Directory $TempDir
    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }

  Build
  exit $lastExitCode
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
