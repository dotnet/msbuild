#! /bin/bash

configuration="Debug"
build_stage1=true
onlyDocChanged=0
skipTests=false
properties=
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

# This script intentionally does NOT source eng/common/tools.sh. Sourcing would pull tools.sh's variables
# into this scope and force in-proc execution of build.sh. Instead we invoke build.sh out-of-proc (see the
# /bin/bash calls below) and compute the few values we need (repo paths, build tool) directly.
build_script="$ScriptRoot/common/build.sh"

# Arguments common to both the stage 1 and stage 2 builds, including any caller-supplied properties.
common_build_args="--restore --build --ci --prepareMachine --configuration $configuration $properties"

if [[ $build_stage1 == true ]]
then
  echo "/bin/bash \"$build_script\" $common_build_args"
  /bin/bash "$build_script" $common_build_args || exit $?

  # Move the stage 1 outputs aside so stage 2 gets a clean artifacts dir to build into.
  rm -rf "$Stage1Dir"
  mv "$ArtifactsDir" "$Stage1Dir"
fi

# Resolve the bootstrapped build tool AFTER the stage 1 outputs have been moved into $Stage1Dir, so the
# globbed paths below point at files that actually exist.
bootstrapRoot="$Stage1Dir/bin/bootstrap"

# Communicate the bootstrapped build tool to the (out-of-proc) stage 2 build.sh via environment
# variables so it does not require sourcing tools.sh here. tools.sh's InitializeBuildTool honors
# _BuildToolPath / _BuildToolCommand.
export _BuildToolPath="$bootstrapRoot/core/dotnet"
export _BuildToolCommand="msbuild"

export DOTNET_ROOT="$bootstrapRoot/core"

# Ensure that debug bits fail fast, rather than hanging waiting for a debugger attach.
export MSBUILDDONOTLAUNCHDEBUGGER=true

# Opt into performance logging.
export DOTNET_PERFLOG_DIR="$PerfLogDir"

# Point child processes (stage 2 build.sh, tests, and the MSBuild grandchildren they spawn) at the
# freshly-built bootstrap .NET host, so task hosts launch with the bits under test. This matches
# DOTNET_ROOT and the bootstrap's own expectation that tests invoke the bootstrap dotnet (see
# eng/BootStrapMsBuild.targets).
export DOTNET_HOST_PATH="$bootstrapRoot/core/dotnet"
export DOTNET_INSTALL_DIR="$bootstrapRoot/core"

# stage2Properties are passed to the stage 2 build only since some MSBuild switches (like /mt) may not
# work with the SDK MSBuild used in stage 1. Mirroring the branches:
#   onlyDocChanged=1 -> bootstrap not created (artifacts not needed downstream)
#   skipTests        -> bootstrap IS created, tests omitted
#   default          -> bootstrap created, tests run
stage2_build_args="$common_build_args $stage2Properties"
if [ $onlyDocChanged = 0 ]
then
  if [ "$skipTests" != true ]
  then
    stage2_build_args="$stage2_build_args --test"
  fi
else
  stage2_build_args="$stage2_build_args /p:CreateBootstrap=false"
fi

echo "/bin/bash \"$build_script\" $stage2_build_args"
/bin/bash "$build_script" $stage2_build_args
exit $?
