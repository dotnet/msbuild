#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

build=false
ci=false
configuration='Debug'
help=false
pack=false
prepare_machine=false
rebuild=false
restore=false
sign=false
projects=''
test=false
verbosity='minimal'
properties=''

repo_root="$scriptroot/.."

if [[ -z $DOTNET_SDK_ARTIFACTS_DIR ]]; then
  artifacts_dir="$repo_root/artifacts"
else
  artifacts_dir="$DOTNET_SDK_ARTIFACTS_DIR"
fi

artifacts_configuration_dir="$artifacts_dir/$configuration"
toolset_dir="$artifacts_dir/toolset"
log_dir="$artifacts_configuration_dir/log"
build_log="$log_dir/Build.binlog"
toolset_restore_log="$log_dir/ToolsetRestore.binlog"
temp_dir="$artifacts_configuration_dir/tmp"

global_json_file="$repo_root/global.json"
build_driver=""
toolset_build_proj=""

while (($# > 0)); do
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
      echo "  --perf                   Run all performance tests in the solution"
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
      echo "  --prepareMachine         Prepare machine for CI run"
      echo ""
      echo "Command line arguments not listed above are passed through to MSBuild."
      exit 0
      ;;
    --pack)
      pack=true
      shift 1
      ;;
    --preparemachine)
      prepare_machine=true
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
    --Perf)
      perf=true
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

# ReadJson [filename] [json key]
# Result: Sets 'readjsonvalue' to the value of the provided json key
# Note: this method may return unexpected results if there are duplicate
# keys in the json
function ReadJson {
  local file=$1
  local key=$2

  local unamestr="$(uname)"
  local sedextended='-r'
  if [[ "$unamestr" == 'Darwin' ]]; then
    sedextended='-E'
  fi;

  readjsonvalue="$(grep -m 1 "\"$key\"" $file | sed $sedextended 's/^ *//;s/.*: *"//;s/",?//')"
  if [[ ! "$readjsonvalue" ]]; then
    echo "Error: Cannot find \"$key\" in $file" >&2;
    ExitWithExitCode 1
  fi;
}

function InitializeDotNetCli {
  # Disable first run since we want to control all package sources
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations to ensure build determinism
  export DOTNET_MULTILEVEL_LOOKUP=0

  # Source Build uses DotNetCoreSdkDir variable
  if [[ -n "$DotNetCoreSdkDir" ]]; then
    export DOTNET_INSTALL_DIR="$DotNetCoreSdkDir"
  fi

  ReadJson "$global_json_file" "version"
  local dotnet_sdk_version="$readjsonvalue"
  local dotnet_root=""

  # Use dotnet installation specified in DOTNET_INSTALL_DIR if it contains the required SDK version, 
  # otherwise install the dotnet CLI and SDK to repo local .dotnet directory to avoid potential permission issues.
  if [[ -d "$DOTNET_INSTALL_DIR/sdk/$dotnet_sdk_version" ]]; then
    dotnet_root="$DOTNET_INSTALL_DIR"
  else
    dotnet_root="$repo_root/.dotnet"
    export DOTNET_INSTALL_DIR="$dotnet_root"

    if [[ "$restore" == true ]]; then
      InstallDotNetSdk $dotnet_root $dotnet_sdk_version
    fi
  fi

  build_driver="$dotnet_root/dotnet"
}

function InstallDotNetSdk {
  local root=$1
  local version=$2

  local install_script=`GetDotNetInstallScript $root`

  bash "$install_script" --version $version --install-dir $root
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    echo "Failed to install dotnet SDK (exit code '$lastexitcode')."
    ExitWithExitCode $lastexitcode
  fi
}

function GetDotNetInstallScript {
  local root=$1
  local install_script="$root/dotnet-install.sh"

  if [[ ! -a "$install_script" ]]; then
    mkdir -p "$root"

    # Use curl if available, otherwise use wget
    if command -v curl > /dev/null; then
      curl "https://dot.net/v1/dotnet-install.sh" -sSL --retry 10 --create-dirs -o "$install_script"
    else
      wget -q -O "$install_script" "https://dot.net/v1/dotnet-install.sh"
    fi
  fi

  # return value
  echo "$install_script"
}

function InitializeToolset {
  ReadJson $global_json_file "RoslynTools.RepoToolset"
  local toolset_version=$readjsonvalue
  local toolset_location_file="$toolset_dir/$toolset_version.txt"

  if [[ -a "$toolset_location_file" ]]; then
    local path=`cat $toolset_location_file`
    if [[ -a "$path" ]]; then
      toolset_build_proj=$path
      return
    fi
  fi  

  if [[ "$restore" != true ]]; then
    echo "Toolset version $toolsetVersion has not been restored."
    ExitWithExitCode 2
  fi
  
  local proj="$toolset_dir/restore.proj"

  echo '<Project Sdk="RoslynTools.RepoToolset"/>' > $proj
  "$build_driver" msbuild $proj /t:__WriteToolsetLocation /m /nologo /clp:None /warnaserror /bl:$toolset_restore_log /v:$verbosity /p:__ToolsetLocationOutputFile=$toolset_location_file 
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    echo "Failed to restore toolset (exit code '$lastexitcode'). See log: $toolset_restore_log"
    ExitWithExitCode $lastexitcode
  fi

  toolset_build_proj=`cat $toolset_location_file`
}

function InitializeCustomToolset {
  if [[ "$dogfood" == true ]]; then
    export SDK_REPO_ROOT="$RepoRoot"
    export SDK_CLI_VERSION="$DotNetCliVersion"
    export MSBuildSDKsPath="$ArtifactsConfigurationDir/bin/Sdks"
    export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR="$MSBuildSDKsPath"
    export NETCoreSdkBundledVersionsProps="$DotNetRoot/sdk/$DotNetCliVersion/Microsoft.NETCoreSdk.BundledVersions.props"
    export CustomAfterMicrosoftCommonTargets="$MSBuildSDKsPath/Microsoft.NET.Build.Extensions/msbuildExtensions-ver/Microsoft.Common.Targets/ImportAfter/Microsoft.NET.Build.Extensions.targets"
    export MicrosoftNETBuildExtensionsTargets="$CustomAfterMicrosoftCommonTargets"
  fi

  if [[ "$restore" != true ]]; then
    return
  fi

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them in source build.
  if [[ "$DotNetBuildFromSource" == "true" ]]; then
    return
  fi
  
  InstallDotNetSharedFramework $DOTNET_INSTALL_DIR "1.0.5"
  InstallDotNetSharedFramework $DOTNET_INSTALL_DIR "1.1.2"
  InstallDotNetSharedFramework $DOTNET_INSTALL_DIR "2.0.0"
}

# Installs additional shared frameworks for testing purposes
function InstallDotNetSharedFramework {
  local dotnet_root=$1
  local version=$2
  local fx_dir="$dotnet_root/shared/Microsoft.NETCore.App/$version"

  if [[ ! -d "$fx_dir" ]]; then
    local install_script=`GetDotNetInstallScript $dotnet_root`
    
    bash "$install_script" --version $version --install-dir $dotnet_root --shared-runtime
    local lastexitcode=$?
    
    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install Shared Framework $version to '$dotnet_root' (exit code '$lastexitcode')."
      ExitWithExitCode $lastexitcode
    fi
  fi
}

function Build {
  "$build_driver" msbuild $toolset_build_proj /m /nologo /clp:Summary /warnaserror \
    /v:$verbosity /bl:$build_log /p:Configuration=$configuration /p:Projects=$projects /p:RepoRoot="$repo_root" \
    /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci \
    $properties
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    echo "Failed to build $toolset_build_proj"
    ExitWithExitCode $lastexitcode
  fi
}

function ExitWithExitCode {
  if [[ "$ci" == true && "$prepare_machine" == true ]]; then
    StopProcesses
  fi
  exit $1
}

function StopProcesses {
  echo "Killing running build processes..."
  pkill -9 "dotnet"
  pkill -9 "vbcscompiler"
}

function Main {
  # HOME may not be defined in some scenarios, but it is required by NuGet
  if [[ -z $HOME ]]; then
    export HOME="$repo_root/artifacts/.home/"
    mkdir -p "$HOME"
  fi

  if [[ -z $projects ]]; then
    projects="$repo_root/*.sln"
  fi

  if [[ -z $NUGET_PACKAGES ]]; then
    if [[ $ci ]]; then
      export NUGET_PACKAGES="$repo_root/.packages"
    else
      export NUGET_PACKAGES="$HOME/.nuget/packages"
    fi
  fi

  mkdir -p "$toolset_dir"
  mkdir -p "$log_dir"
  
  if [[ $ci ]]; then
    mkdir -p "$temp_dir"
    export TEMP="$temp_dir"
    export TMP="$temp_dir"
  fi

  InitializeDotNetCli
  InitializeToolset
  InitializeCustomToolset

  if [[ "$dogfood" != true ]]; then
    Build
  fi
}

Main
