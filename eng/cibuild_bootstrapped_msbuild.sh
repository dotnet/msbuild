#! /bin/bash

configuration="debug"
host_type="core"
build_stage1=true
properties=

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
    *)
      properties="$properties $1"
      shift 1
      ;;
  esac
done

RepoRoot="$ScriptRoot/.."
ArtifactsDir="$RepoRoot/artifacts"
Stage1Dir="$RepoRoot/stage1"

if [[ $build_stage1 == true ]];
then
	/bin/bash "$ScriptRoot/common/build.sh" --restore --build --ci /p:CreateBootstrap=true $properties
fi

bootstrapRoot="$Stage1Dir/bin/bootstrap"

if [ $host_type = "core" ]
then
  . "$ScriptRoot/common/tools.sh"

  InitializeDotNetCli true

  _InitializeBuildTool="$_InitializeDotNetCli/dotnet"
  DOTNET_HOST_PATH="$_InitializeDotNetCli/dotnet"
  _InitializeBuildToolCommand="$bootstrapRoot/netcoreapp2.1/MSBuild/MSBuild.dll"
else
  echo "Unsupported hostType ($host_type)"
  exit 1
fi

mv $ArtifactsDir $Stage1Dir

export MSBUILDDONOTLAUNCHDEBUGGER=true
export RoslynCommandLineLogFile=artifacts/log/Debug/Roslyn_comm.log
export DOTNET_HOST_PATH="$_InitializeDotNetCli/dotnet"

# When using bootstrapped MSBuild:
# - Turn off node reuse (so that bootstrapped MSBuild processes don't stay running and lock files)
# - Do run tests
# - Don't try to create a bootstrap deployment
. "$ScriptRoot/common/build.sh" --restore --build --test --ci --nodereuse false /p:CreateBootstrap=false $properties

