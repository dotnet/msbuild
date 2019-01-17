#!/bin/sh

configuration="debug"
host_type="core"
build_stage1=true
run_tests="--test"
run_restore="--restore"

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
    --skip_tests)
      run_tests=""
      shift
      ;;
    --skip_restore)
      run_restore=""
      shift
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
artifacts_dir="$RepoRoot/artifacts"

if [[ $build_stage1 == true ]];
then
	/bin/bash "$ScriptRoot/common/build.sh" $run_restore --build --ci --configuration $configuration /p:CreateBootstrap=true $properties
fi

bootstrapRoot="$artifacts_dir/bin/bootstrap"
# export to make this available to `eng/common/build.sh`
export artifacts_dir="$artifacts_dir/2"

if [ $host_type = "core" ]
then
	_InitializeMSBuildToUse="$bootstrapRoot/netcoreapp2.1/MSBuild/MSBuild.dll"
else
  echo "Unsupported hostType ($host_type)"
  exit 1
fi

# When using bootstrapped MSBuild:
# - Turn off node reuse (so that bootstrapped MSBuild processes don't stay running and lock files)
# - Do run tests
# - Don't try to create a bootstrap deployment
. "$ScriptRoot/common/build.sh" $run_restore --build $run_tests --ci --nodereuse false --configuration $configuration /p:CreateBootstrap=false $properties

