[CmdletBinding(PositionalBinding=$false)]
Param(
  [switch] $build,
  [switch] $ci,
  [string] $configuration = "Debug",
  [switch] $help,
  [switch] $log = $True,
  [switch] $pack,
  [switch] $prepareMachine,
  [switch] $rebuild,
  [switch] $restore = $True,
  [switch] $sign,
  [switch] $test = $True,
  [string] $verbosity = "minimal",
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

Set-StrictMode -Version 2.0
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
    Write-Host "  -test                   Run all unit tests in the solution"
    Write-Host "  -sign                   Sign build outputs"
    Write-Host "  -pack                   Package build outputs into NuGet packages and Willow components"
    Write-Host ""
    Write-Host "Advanced settings:"
    Write-Host "  -ci                     Set when running on CI server"
    Write-Host "  -log                    Enable logging (by default on CI)"
    Write-Host "  -prepareMachine         Prepare machine for CI run"
    Write-Host ""
    Write-Host "Command line arguments not listed above are passed through to MSBuild."
    Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
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

function InstallDotNetCli {
  $DotNetCliVersion = GetVersionsPropsVersion -Name "DotNetCliVersion"
  $DotNetInstallVerbosity = ""

  if (!$env:DOTNET_INSTALL_DIR) {
    $env:DOTNET_INSTALL_DIR = Join-Path $RepoRoot "artifacts\.dotnet\$DotNetCliVersion"
  }

  $DotNetRoot = $env:DOTNET_INSTALL_DIR
  $DotNetInstallScript = Join-Path $DotNetRoot "dotnet-install.ps1"

  if (!(Test-Path $DotNetInstallScript)) {
    Create-Directory $DotNetRoot
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -UseBasicParsing -OutFile $DotNetInstallScript
  }

  if ($verbosity -eq "diagnostic") {
    $DotNetInstallVerbosity = "-Verbose"
  }

  # Install a stage 0
  $SdkInstallDir = Join-Path $DotNetRoot "sdk\$DotNetCliVersion"

  if (!(Test-Path $SdkInstallDir)) {
    # Use Invoke-Expression so that $DotNetInstallVerbosity is not positionally bound when empty
    Invoke-Expression -Command "$DotNetInstallScript -Version $DotNetCliVersion $DotNetInstallVerbosity"

    if($LASTEXITCODE -ne 0) {
      throw "Failed to install stage0"
    }
  }

  # Put the stage 0 on the path
  $env:PATH = "$DotNetRoot;$env:PATH"

  # Disable first run since we want to control all package sources
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations
  $env:DOTNET_MULTILEVEL_LOOKUP=0
}

function InstallNuGet {
  $NugetInstallDir = Join-Path $RepoRoot "artifacts\.nuget"
  $NugetExe = Join-Path $NugetInstallDir "nuget.exe"

  if (!(Test-Path -Path $NugetExe)) {
    Create-Directory $NugetInstallDir
    Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -UseBasicParsing -OutFile $NugetExe
  }
}

function InstallRepoToolset {
  $RepoToolsetVersion = GetVersionsPropsVersion -Name "RoslynToolsMicrosoftRepoToolsetVersion"
  $RepoToolsetDir = Join-Path $NuGetPackageRoot "roslyntools.microsoft.repotoolset\$RepoToolsetVersion\tools"
  $RepoToolsetBuildProj = Join-Path $RepoToolsetDir "Build.proj"

  if ($ci -or $log) {
    Create-Directory $LogDir
    $logCmd = "/bl:" + (Join-Path $LogDir "Toolset.binlog")
  } else {
    $logCmd = ""
  }

  if (!(Test-Path -Path $RepoToolsetBuildProj)) {
    $ToolsetProj = Join-Path $PSScriptRoot "Toolset.proj"
    msbuild $ToolsetProj /t:restore /m /nologo /clp:Summary /warnaserror /v:$verbosity $logCmd

    if($LASTEXITCODE -ne 0) {
      throw "Failed to build $ToolsetProj"
    }
  }

  return $RepoToolsetBuildProj
}

function LocateVisualStudio {
  $VSWhereVersion = GetVersionsPropsVersion -Name "VSWhereVersion"
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
  InstallDotNetCli
  InstallNuget
  $RepoToolsetBuildProj = InstallRepoToolset

  if ($prepareMachine) {
    Create-Directory $NuGetPackageRoot
    dotnet nuget locals all --clear

    if($LASTEXITCODE -ne 0) {
      throw "Failed to clear NuGet cache"
    }
  }

  if ($ci -or $log) {
    Create-Directory $LogDir
    $logCmd = "/bl:" + (Join-Path $LogDir "Build.binlog")
  } else {
    $logCmd = ""
  }

  $solution = Join-Path $RepoRoot "MSBuild.sln"
  
  msbuild $RepoToolsetBuildProj /m /nologo /clp:Summary /warnaserror /v:$verbosity $logCmd /p:Configuration=$configuration /p:SolutionPath=$solution /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci $properties

  if($LASTEXITCODE -ne 0) {
    throw "Failed to build $RepoToolsetBuildProj"
  }
}

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

if ($help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
  Print-Usage
  exit 0
}

$RepoRoot = Join-Path $PSScriptRoot "..\"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$ArtifactsConfigurationDir = Join-Path $ArtifactsDir $configuration
$LogDir = Join-Path $ArtifactsConfigurationDir "log"
$VersionsProps = Join-Path $PSScriptRoot "Versions.props"

try {
  if ($ci) {
    $TempDir = Join-Path $ArtifactsConfigurationDir "tmp"
    Create-Directory $TempDir

    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }

  if (!($env:NUGET_PACKAGES)) {
    $env:NUGET_PACKAGES = Join-Path $env:UserProfile ".nuget\packages"
  }

  $NuGetPackageRoot = $env:NUGET_PACKAGES

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
