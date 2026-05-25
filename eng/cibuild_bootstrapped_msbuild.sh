#! /bin/bash

configuration="Debug"
host_type="core"
build_stage1=true
onlyDocChanged=0
skipTests=false
properties=
extra_properties=
stage2Properties=

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

while [[ $# -gt 0 ]]; do
  lowerI="$(echo $1 | awk '{print tolower($0)}')"
  case "$lowerI" in
    --configuration)
      configuration=$2
      shift 2
      ;;
    --build_stage1)
      build_stage1=$2
      shift 2
      ;;
    --host_type)
      host_type=$2
      shift 2
      ;;
    --onlydocchanged)
      onlyDocChanged=$2
      shift 2
      ;;
    --skiptests)
      skipTests=true
      shift 1
      ;;
    --stage2properties)
      stage2Properties=$2
      shift 2
      ;;
    *)
      properties="$properties $1"
      shift 1
      ;;
  esac
done

RepoRoot="$ScriptRoot/.."
ArtifactsDir="$RepoRoot/artifacts"
Stage1Dir="$RepoRoot/stage1"
PerfLogDir="$ArtifactsDir/log/$configuration/PerformanceLogs"

. "$ScriptRoot/common/tools.sh"
InitializeDotNetCli true

if [[ $build_stage1 == true ]];
then
	/bin/bash "$ScriptRoot/common/build.sh" --restore --build --ci --configuration $configuration $properties $extra_properties || exit $?
fi

bootstrapRoot="$Stage1Dir/bin/bootstrap"

if [ $host_type = "core" ]
then
  # Read the resolved BootstrapSdkVersion (Max(hardcoded floor, NETCoreSdkVersion))
  # from the same project that drove the stage1 bootstrap layout, so the value here
  # matches the on-disk sdk/<version> directory. User properties are forwarded so
  # /p:BootstrapSdkVersion=... overrides used during stage1 are honored.
  bootstrap_csproj="$RepoRoot/src/MSBuild.Bootstrap/MSBuild.Bootstrap.csproj"
  if ! sdk_version_raw=$("$_InitializeDotNetCli/dotnet" msbuild "$bootstrap_csproj" -getProperty:BootstrapSdkVersion -nologo $properties 2>&1); then
    echo "ERROR: Failed to invoke 'dotnet msbuild -getProperty:BootstrapSdkVersion' on $bootstrap_csproj:" >&2
    echo "$sdk_version_raw" >&2
    exit 1
  fi
  sdk_version=$(echo "$sdk_version_raw" | tr -d '[:space:]')
  if [ -z "$sdk_version" ]; then
    echo "ERROR: Could not resolve BootstrapSdkVersion from $bootstrap_csproj." >&2
    exit 1
  fi

  _InitializeBuildTool="${bootstrapRoot}/core/dotnet"
  _InitializeBuildToolCommand="${bootstrapRoot}/core/sdk/${sdk_version}/MSBuild.dll"
  _InitializeBuildToolFramework="net10.0"

  export DOTNET_ROOT="${bootstrapRoot}/core"

else
  echo "Unsupported hostType ($host_type)"
  exit 1
fi

mv $ArtifactsDir $Stage1Dir

# Ensure that debug bits fail fast, rather than hanging waiting for a debugger attach.
export MSBUILDDONOTLAUNCHDEBUGGER=true

# Opt into performance logging.
export DOTNET_PERFLOG_DIR=$PerfLogDir

# Prior to 3.0, the Csc task uses this environment variable to decide whether to run
# a CLI host or directly execute the compiler.
export DOTNET_HOST_PATH="$_InitializeDotNetCli/dotnet"

# When using bootstrapped MSBuild:
# - Turn off node reuse (so that bootstrapped MSBuild processes don't stay running and lock files)
# - Create bootstrap environment as it's required when also running tests
# - stage2Properties are passed to all Stage 2 builds since some MSBuild switches (like /mt) may not work with the SDK MSBuild used in Stage 1
if [ $onlyDocChanged = 0 ]
then
    if [ "$skipTests" = true ]
    then
        . "$ScriptRoot/common/build.sh" --restore --build --ci --nodereuse false --configuration $configuration $properties $extra_properties $stage2Properties
    else
        . "$ScriptRoot/common/build.sh" --restore --build --test --ci --nodereuse false --configuration $configuration $properties $extra_properties $stage2Properties
    fi
else
    . "$ScriptRoot/common/build.sh" --restore --build --ci --nodereuse false --configuration $configuration /p:CreateBootstrap=false $properties $extra_properties $stage2Properties
fi
