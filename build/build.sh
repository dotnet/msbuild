#!/bin/bash

build=false
ci=false
configuration="Debug"
help=false
log=false
pack=false
prepareMachine=false
rebuild=false
restore=false
sign=false
solution=""
test=false
verbosity="minimal"
properties=""

while [[ $# > 0 ]]; do
  lowerI="$(echo $1 | awk '{print tolower($0)}')"
  case $lowerI in
    --build)
      build=true
      shift 1
      ;;
    --ci)
      ci=true
      shift 1
      ;;
    --configuration)
      configuration=$2
      shift 2
      ;;
    --help)
      echo "Common settings:"
      echo "  --configuration <value>  Build configuration Debug, Release"
      echo "  --verbosity <value>    Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
      echo "  --help           Print help and exit"
      echo ""
      echo "Actions:"
      echo "  --restore        Restore dependencies"
      echo "  --build          Build solution"
      echo "  --rebuild        Rebuild solution"
      echo "  --test           Run all unit tests in the solution"
      echo "  --sign           Sign build outputs"
      echo "  --pack           Package build outputs into NuGet packages and Willow components"
      echo ""
      echo "Advanced settings:"
      echo "  --solution <value>     Path to solution to build"
      echo "  --ci           Set when running on CI server"
      echo "  --log          Enable logging (by default on CI)"
      echo "  --prepareMachine     Prepare machine for CI run"
      echo ""
      echo "Command line arguments not listed above are passed through to MSBuild."
      exit 0
      ;;
    --log)
      log=true
      shift 1
      ;;
    --pack)
      pack=true
      shift 1
      ;;
    --prepareMachine)
      prepareMachine=true
      shift 1
      ;;
    --rebuild)
      rebuild=true
      shift 1
      ;;
    --restore)
      restore=true
      shift 1
      ;;
    --sign)
      sign=true
      shift 1
      ;;
    --solution)
      solution=$2
      shift 2
      ;;
    --test)
      test=true
      shift 1
      ;;
    --verbosity)
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

function GetDotNetCliVersion {
  echo "$( awk -F'[<>]' "/<DotNetCliVersion>/{print \$3}" "$RepoRoot/build/Versions.props" )"
}

function InstallDotNetCli {
  dotnetCliVersion="$( GetDotNetCliVersion )"
  dotnetInstallVerbosity=""

  if [ -z "$DOTNET_INSTALL_DIR" ]
  then
    export DOTNET_INSTALL_DIR="$RepoRoot/artifacts/.dotnet/$dotnetCliVersion"
  fi

  dotnetInstallScript="$DOTNET_INSTALL_DIR/dotnet-install.sh"

  if [ ! -a "$dotnetInstallScript" ]
  then
    CreateDirectory "$DOTNET_INSTALL_DIR"
    curl "https://dot.net/v1/dotnet-install.sh" -sSL -o "$dotnetInstallScript"
  fi

  if [[ "$(echo $verbosity | awk '{print tolower($0)}')" == "diagnostic" ]]
  then
    dotnetInstallVerbosity="--verbose"
  fi

  # Install a stage 0
  sdkInstallDir="$DOTNET_INSTALL_DIR/sdk/$dotnetCliVersion"

  if [ ! -d "$sdkInstallDir" ]
  then
    bash "$dotnetInstallScript" --version $dotnetCliVersion $dotnetInstallVerbosity

    if [ $? != 0 ]
    then
      return $?
    fi
  fi

  # Install 1.0 shared framework
  netcoreApp10Version="1.0.5"
  netcoreApp10Dir="$DOTNET_INSTALL_DIR/shared/Microsoft.NETCore.App/$netcoreApp10Version"

  if [ ! -d "$netcoreApp10Dir" ]
  then
    bash "$dotnetInstallScript" --channel "Preview" --version $netcoreApp10Version --shared-runtime $dotnetInstallVerbosity

    if [ $? != 0 ]
    then
      return $?
    fi
  fi

  # Install 1.1 shared framework
  netcoreApp11Version="1.1.2"
  netcoreApp11Dir="$DOTNET_INSTALL_DIR/shared/Microsoft.NETCore.App/$netcoreApp11Version"

  if [ ! -d "$netcoreApp11Dir" ]
  then
    bash "$dotnetInstallScript" --channel "Release/1.1.0" --version $netcoreApp11Version --shared-runtime $dotnetInstallVerbosity

    if [ $? != 0 ]
    then
      return $?
    fi
  fi

  # Put the stage 0 on the path
  export PATH="$DOTNET_INSTALL_DIR:$PATH"

  # Disable first run since we want to control all package sources
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations
  export DOTNET_MULTILEVEL_LOOKUP=0
}

function Build {
  InstallDotNetCli

  if [ $? != 0 ]
  then
    return $?
  fi

  # Preparation of a CI machine
  if $prepareMachine
  then
    ClearNuGetCache
  fi

  if $ci || $log
  then
    CreateDirectory $LogDir
    logCmd="/bl:$LogDir/Build.binlog"
  else
    logCmd=""
  fi

  dotnet msbuild $BuildProj /nologo /clp:Summary /warnaserror /v:$verbosity $logCmd /p:Configuration=$configuration /p:SolutionPath=$solution /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci $properties

  if [ $? != 0 ]
  then
    return $?
  fi
}

function StopProcesses {
  echo "Killing running build processes..."
  pkill -9 "msbuild"
  pkill -9 "vbcscompiler"
}

function ClearNuGetCache {
  if [ -z "$NUGET_PACKAGES" ]
  then
    export NUGET_PACKAGES="$HOME/.nuget/packages/"
  fi

  CreateDirectory "$NUGET_PACKAGES"
  dotnet nuget locals all --clear
}

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

RepoRoot="$ScriptRoot/../"
ToolsRoot="$RepoRoot/artifacts/.tools"
BuildProj="$ScriptRoot/build.proj"
DependenciesProps="$ScriptRoot/Versions.props"
ArtifactsDir="$RepoRoot/artifacts"
LogDir="$ArtifactsDir/$configuration/log"
TempDir="$ArtifactsDir/$configuration/tmp"

# HOME may not be defined in some scenarios, but it is required by NuGet
if [ -z $HOME ]
then
  export HOME="$RepoRoot/artifacts/.home/"
  CreateDirectory "$HOME"
fi

if $ci
then
  CreateDirectory "$TempDir"
  export TEMP="$TempDir"
  export TMP="$TempDir"
fi

Build

if $ci && $prepareMachine
then
  StopProcesses
fi
