#!/bin/bash

if [ "$1" ==  "-fromScript" ]; then
  otherScript=$2
  shift 2
  echo Executed from $otherScript with arguments: $*
fi

build=false
ci=false
configuration="Debug"
help=false
nolog=false
pack=false
prepareMachine=false
rebuild=false
norestore=false
sign=false
skipTests=false
bootstrapOnly=false
verbosity="minimal"
hostType="core"
properties=""

function Help() { 
  echo "Common settings:"
  echo "  -configuration <value>  Build configuration Debug, Release"
  echo "  -verbosity <value>      Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
  echo "  -help                   Print help and exit"
  echo ""
  echo "Actions:"
  echo "  -norestore              Don't automatically run restore"
  echo "  -build                  Build solution"
  echo "  -rebuild                Rebuild solution"
  echo "  -skipTests              Don't run tests"
  echo "  -bootstrapOnly          Don't run build again with bootstrapped MSBuild"
  echo "  -sign                   Sign build outputs"
  echo "  -pack                   Package build outputs into NuGet packages and Willow components"
  echo ""
  echo "Advanced settings:"
  echo "  -ci                     Set when running on CI server"
  echo "  -nolog                  Disable logging"
  echo "  -prepareMachine         Prepare machine for CI run"
  echo ""
  echo "Command line arguments not listed above are passed through to MSBuild."
}

while [[ $# > 0 ]]; do
  lowerI="$(echo $1 | awk '{print tolower($0)}')"
  case $lowerI in
    -build)
      build=true
      shift 1
      ;;
    -ci)
      ci=true
      shift 1
      ;;
    -configuration)
      configuration=$2
      shift 2
      ;;
    -help)
      Help
      exit 0
      ;;
    -nolog)
      nolog=true
      shift 1
      ;;
    -pack)
      pack=true
      shift 1
      ;;
    -preparemachine)
      prepareMachine=true
      shift 1
      ;;
    -rebuild)
      rebuild=true
      shift 1
      ;;
    -norestore)
      norestore=true
      shift 1
      ;;
    -sign)
      sign=true
      shift 1
      ;;
    -skiptests)
      skipTests=true
      shift 1
      ;;
    -bootstraponly)
      bootstrapOnly=true
      shift 1
      ;;
    -verbosity)
      verbosity=$2
      shift 2
      ;;
    *)
      properties="$properties $1"
      shift 1
      ;;
  esac
done

function CreateDirectory {
  if [ ! -d "$1" ]
  then
    mkdir -p "$1"
  fi
}

function QQ {
  echo '"'$*'"'
}

function GetLogCmd {
  if $ci || $log
  then
    CreateDirectory $LogDir

    local logCmd="/bl:$(QQ $LogDir/$1.binlog)"

    # When running under CI, also create a text log, so it can be viewed in the Jenkins UI
    if $ci
    then
      logCmd="$logCmd /fl /flp:Verbosity=diag\;LogFile=$(QQ $LogDir/$1.log)"
    fi
  else
    logCmd=""
  fi

  echo $logCmd
}

function CallMSBuild {
  local commandLine=""

  if [ -z "$msbuildHost" ]
  then
    commandLine="$(QQ $msbuildToUse) $*"
  else
    commandLine="$(QQ $msbuildHost) $(QQ $msbuildToUse) $*"
  fi

  echo ============= MSBuild command ============= 
  echo "$commandLine"
  echo ===========================================

  eval $commandLine

  LASTEXITCODE=$?

  if [ $LASTEXITCODE != 0 ]
  then
    echo "Failed to run MSBuild"
    StopProcesses
    exit $LASTEXITCODE
  fi
}

function GetVersionsPropsVersion {
  echo "$( awk -F'[<>]' "/<$1>/{print \$3}" "$VersionsProps" )"
}

function InstallDotNetCli {
  DotNetCliVersion="$( GetVersionsPropsVersion DotNetCliVersion )"
  DotNetInstallVerbosity=""

  if [ -z "$DOTNET_INSTALL_DIR" ]
  then
    export DOTNET_INSTALL_DIR="$RepoRoot/artifacts/.dotnet/$DotNetCliVersion"
  fi

  DotNetRoot=$DOTNET_INSTALL_DIR
  DotNetInstallScript="$DotNetRoot/dotnet-install.sh"

  if [ ! -a "$DotNetInstallScript" ]
  then
    CreateDirectory "$DotNetRoot"
    curl "https://dot.net/v1/dotnet-install.sh" -sSL -o "$DotNetInstallScript"
  fi

  if [[ "$(echo $verbosity | awk '{print tolower($0)}')" == "diagnostic" ]]
  then
    DotNetInstallVerbosity="--verbose"
  fi

  # Install a stage 0
  SdkInstallDir="$DotNetRoot/sdk/$DotNetCliVersion"

  if [ ! -d "$SdkInstallDir" ]
  then
    bash "$DotNetInstallScript" --version $DotNetCliVersion $DotNetInstallVerbosity
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to install stage0"
      return $LASTEXITCODE
    fi
  fi

  # Put the stage 0 on the path
  export PATH="$DotNetRoot:$PATH"

  # Disable first run since we want to control all package sources
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations
  export DOTNET_MULTILEVEL_LOOKUP=0
}

function InstallRepoToolset {
  RepoToolsetVersion="$( GetVersionsPropsVersion RoslynToolsRepoToolsetVersion )"
  RepoToolsetDir="$NuGetPackageRoot/roslyntools.repotoolset/$RepoToolsetVersion/tools"
  RepoToolsetBuildProj="$RepoToolsetDir/Build.proj"

  local logCmd=$(GetLogCmd Toolset)

  if [ ! -d "$RepoToolsetBuildProj" ]
  then
    ToolsetProj="$ScriptRoot/Toolset.proj"
    CallMSBuild $(QQ $ToolsetProj) /t:restore /m /nologo /clp:Summary /warnaserror /v:$verbosity $logCmd
  fi
}

function ErrorHostType {
  echo "Unsupported hostType ($hostType)"
  exit 1
}

function Build {
  InstallDotNetCli

  if [ "$hostType" = "core" ]
  then
    msbuildHost="$DOTNET_INSTALL_DIR/dotnet"
    export DOTNET_HOST_PATH=$msbuildHost
  else
    ErrorHostType
  fi

  InstallRepoToolset

  if $prepareMachine
  then
    CreateDirectory "$NuGetPackageRoot"
    dotnet nuget locals all --clear
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to clear NuGet cache"
      return $LASTEXITCODE
    fi
  fi

  local logCmd=$(GetLogCmd Build)

  solution="$RepoRoot/MSBuild.sln"

  commonMSBuildArgs="/m /nologo /clp:Summary /v:$verbosity /p:Configuration=$configuration /p:SolutionPath=$(QQ $solution) /p:CIBuild=$ci"

  # Only enable warnaserror on CI runs.
  if $ci
  then
    commonMSBuildArgs="$commonMSBuildArgs /warnaserror"
  fi

  # Only test using stage 0 MSBuild if -bootstrapOnly is specified
  local testStage0=false
  if $bootstrapOnly
  then
    testStage0=$test
  fi

  CallMSBuild $(QQ $RepoToolsetBuildProj) $commonMSBuildArgs $logCmd /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Test=$testStage0 /p:Sign=$sign /p:Pack=$pack /p:CreateBootstrap=true $properties

  if ! $bootstrapOnly
  then
    bootstrapRoot="$ArtifactsConfigurationDir/bootstrap"

    if [ $hostType = "core" ]
    then
      msbuildToUse="$bootstrapRoot/netcoreapp2.0/MSBuild/MSBuild.dll"
    else
      ErrorHostType
    fi

    export ArtifactsDir="$ArtifactsDir/2"

    local logCmd=$(GetLogCmd BuildWithBootstrap)

      # When using bootstrapped MSBuild:
      # - Turn off node reuse (so that bootstrapped MSBuild processes don't stay running and lock files)
      # - Don't sign
      # - Don't pack
      # - Do run tests (if not skipped)
      # - Don't try to create a bootstrap deployment
    CallMSBuild $(QQ $RepoToolsetBuildProj) $commonMSBuildArgs /nr:false $logCmd /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Test=$test /p:Sign=false /p:Pack=false /p:CreateBootstrap=false $properties

    StopProcesses
  fi
}

function StopProcesses {
  echo "Killing running build processes..."
  pkill -9 -il "dotnet"
  pkill -9 -il "vbcscompiler"
}

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

RepoRoot="$ScriptRoot/.."
ArtifactsDir="$RepoRoot/artifacts"
ArtifactsConfigurationDir="$ArtifactsDir/$configuration"
LogDir="$ArtifactsConfigurationDir/log"
VersionsProps="$ScriptRoot/Versions.props"

msbuildToUse="msbuild"

log=false
if ! $nolog
then
  log=true
fi

restore=false
if ! $norestore
then
  restore=true
fi

test=false
if ! $skipTests
then
  test=true
fi

if [ "$hostType" != "core" ]; then
  ErrorHostType
fi

# HOME may not be defined in some scenarios, but it is required by NuGet
if [ -z $HOME ]
then
  export HOME="$RepoRoot/artifacts/.home/"
  CreateDirectory "$HOME"
fi

if $ci
then
  TempDir="$ArtifactsConfigurationDir/tmp"
  CreateDirectory "$TempDir"

  export TEMP="$TempDir"
  export TMP="$TempDir"
fi

if [ -z $NUGET_PACKAGES ]
then
  export NUGET_PACKAGES="$HOME/.nuget/packages"
fi

NuGetPackageRoot=$NUGET_PACKAGES

Build
LASTEXITCODE=$?

if $ci && $prepareMachine
then
  StopProcesses
fi

exit $LASTEXITCODE
