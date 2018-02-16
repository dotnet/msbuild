#!/bin/bash

build=false
ci=false
configuration="Debug"
dogfood=false
log=false
pack=false
prepareMachine=false
rebuild=false
restore=false
sign=false
solution=""
test=false
verbosity="minimal"
properties=()

while [[ $# -gt 0 ]]; do
  lowerI="$(echo "$1" | awk '{print tolower($0)}')"
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
    --dogfood)
      dogfood=true
      shift 1
      ;;
    --help)
      echo "Common settings:"
      echo "  --configuration <value>  Build configuration Debug, Release"
      echo "  --verbosity <value>      Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
      echo "  --help                   Print help and exit"
      echo ""
      echo "Actions:"
      echo "  --restore                Restore dependencies"
      echo "  --build                  Build solution"
      echo "  --rebuild                Rebuild solution"
      echo "  --test                   Run all unit tests in the solution"
      echo "  --sign                   Sign build outputs"
      echo "  --pack                   Package build outputs into NuGet packages and Willow components"
      echo ""
      echo "Advanced settings:"
      echo "  --dogfood                Setup a dogfood environment using the local build"
      echo "                           For this to have an effect, you will need to source the build script."
      echo "                           If this option is specified, any actions (such as --build or --restore)"
      echo "                           will be ignored."
      echo "  --solution <value>       Path to solution to build"
      echo "  --ci                     Set when running on CI server"
      echo "  --log                    Enable logging (by default on CI)"
      echo "  --prepareMachine         Prepare machine for CI run"
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
      properties+=("$1")
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

function GetVersionsPropsVersion {
  echo "$( awk -F'[<>]' "/<$1>/{print \$3}" "$VersionsProps" )"
}

function InstallDotNetCli {
  DotNetCliVersion="$( GetVersionsPropsVersion DotNetCliVersion )"
  DotNetInstallVerbosity=""

  if $dogfood
  then
    export SDK_REPO_ROOT="$RepoRoot"
    export SDK_CLI_VERSION="$DotNetCliVersion"
    export MSBuildSDKsPath="$ArtifactsConfigurationDir/bin/Sdks"
    export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR="$MSBuildSDKsPath"
    export NETCoreSdkBundledVersionsProps="$DotNetRoot/sdk/$DotNetCliVersion/Microsoft.NETCoreSdk.BundledVersions.props"
    export CustomAfterMicrosoftCommonTargets="$MSBuildSDKsPath/Microsoft.NET.Build.Extensions/msbuildExtensions-ver/Microsoft.Common.Targets/ImportAfter/Microsoft.NET.Build.Extensions.targets"
    export MicrosoftNETBuildExtensionsTargets="$CustomAfterMicrosoftCommonTargets"
  fi

  if [ -z "$DOTNET_INSTALL_DIR" ]
  then
    export DOTNET_INSTALL_DIR="$ArtifactsDir/.dotnet/$DotNetCliVersion"
  fi

  DotNetRoot=$DOTNET_INSTALL_DIR
  DotNetInstallScript="$DotNetRoot/dotnet-install.sh"

  if [ ! -a "$DotNetInstallScript" ]
  then
    CreateDirectory "$DotNetRoot"
    curl "https://dot.net/v1/dotnet-install.sh" -sSL -o "$DotNetInstallScript"
  fi

  if [[ "$(echo "$verbosity" | awk '{print tolower($0)}')" == "diagnostic" ]]
  then
    DotNetInstallVerbosity="--verbose"
  fi

  # Install a stage 0
  SdkInstallDir="$DotNetRoot/sdk/$DotNetCliVersion"

  if [ ! -d "$SdkInstallDir" ]
  then
    bash "$DotNetInstallScript" --version "$DotNetCliVersion" $DotNetInstallVerbosity
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to install stage0"
      return $LASTEXITCODE
    fi
  fi

  # Install 1.0 shared framework
  NetCoreApp10Version="1.0.5"
  NetCoreApp10Dir="$DotNetRoot/shared/Microsoft.NETCore.App/$NetCoreApp10Version"

  if [ ! -d "$NetCoreApp10Dir" ]
  then
    bash "$DotNetInstallScript" --channel "Preview" --version $NetCoreApp10Version --shared-runtime $DotNetInstallVerbosity
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to install 1.0 shared framework"
      return $LASTEXITCODE
    fi
  fi

  # Install 1.1 shared framework
  NetCoreApp11Version="1.1.2"
  NetCoreApp11Dir="$DotNetRoot/shared/Microsoft.NETCore.App/$NetCoreApp11Version"

  if [ ! -d "$NetCoreApp11Dir" ]
  then
    bash "$DotNetInstallScript" --channel "Release/1.1.0" --version $NetCoreApp11Version --shared-runtime $DotNetInstallVerbosity
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to install 1.1 shared framework"
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

  if $ci || $log
  then
    CreateDirectory "$LogDir"
    logCmd="/bl:$LogDir/Build.binlog"
  else
    logCmd=""
  fi

  if [ ! -d "$RepoToolsetBuildProj" ]
  then
    ToolsetProj="$ScriptRoot/Toolset.proj"
    dotnet msbuild "$ToolsetProj" /t:restore /m /nologo /clp:Summary /warnaserror "/v:$verbosity" $logCmd
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to build $ToolsetProj"
      return $LASTEXITCODE
    fi
  fi
}

function Build {
  if ! InstallDotNetCli
  then
    return $?
  fi

  if ! InstallRepoToolset
  then
    return $?
  fi

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

  if [ $dogfood != true ]
  then
    if $ci || $log
    then
      CreateDirectory "$LogDir"
      logCmd="/bl:$LogDir/Build.binlog"
    else
      logCmd=""
    fi

    if [ -z "$solution" ]
    then
      solution="$RepoRoot/sdk.sln"
    fi

    dotnet msbuild $RepoToolsetBuildProj /m /nologo /clp:Summary /warnaserror "/v:$verbosity" $logCmd "/p:Configuration=$configuration" "/p:SolutionPath=$solution" /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci "${properties[@]}"
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to build $RepoToolsetBuildProj"
      return $LASTEXITCODE
    fi
  fi
}

function StopProcesses {
  echo "Killing running build processes..."
  pkill -9 "msbuild"
  pkill -9 "vbcscompiler"
}

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

RepoRoot="$ScriptRoot/.."
if [ -z $DOTNET_SDK_ARTIFACTS_DIR ]
then
  ArtifactsDir="$RepoRoot/artifacts"
else
  ArtifactsDir="$DOTNET_SDK_ARTIFACTS_DIR"
fi


ArtifactsConfigurationDir="$ArtifactsDir/$configuration"
LogDir="$ArtifactsConfigurationDir/log"
VersionsProps="$ScriptRoot/Versions.props"

# HOME may not be defined in some scenarios, but it is required by NuGet
if [ -z "$HOME" ]
then
  export HOME="$ArtifactsDir/.home/"
  CreateDirectory "$HOME"
fi

if $ci
then
  TempDir="$ArtifactsConfigurationDir/tmp"
  CreateDirectory "$TempDir"

  export TEMP="$TempDir"
  export TMP="$TempDir"
fi

if [ -z "$NUGET_PACKAGES" ]
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

# The script should be sourced if using --dogfood, which means in that case we don't want to exit
if [ $dogfood = true ]
then
  return $LASTEXITCODE
else
  exit $LASTEXITCODE
fi
