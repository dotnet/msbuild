configuration="debug"
host_type="core"
build_stage1=true

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
ArtifactsBinDir="$ArtifactsDir/bin"
LogDir="$ArtifactsDir/log/$configuration"
TempDir="$ArtifactsDir/tmp/$configuration"

if [[ $build_stage1 == true ]];
then
	/bin/bash "$ScriptRoot/common/build.sh" --restore --build --ci /p:CreateBootstrap=true /p:Projects="$RepoRoot/MSBuild.sln" $properties
fi

bootstrapRoot="$ArtifactsBinDir/bootstrap"

if [ $host_type = "core" ]
then
	_InitializeMSBuildToUse="$bootstrapRoot/netcoreapp2.1/MSBuild/MSBuild.dll"
else
  echo "Unsupported hostType ($host_type)"
  exit 1
fi

# forcing the slashes here or they get normalized and lost later
export ArtifactsDir="$ArtifactsDir\\2\\"

# When using bootstrapped MSBuild:
# - Turn off node reuse (so that bootstrapped MSBuild processes don't stay running and lock files)
# - Do run tests
# - Don't try to create a bootstrap deployment
. "$ScriptRoot/common/build.sh" --restore --build --test --ci --nodereuse false /p:CreateBootstrap=false /p:Projects="$RepoRoot/MSBuild.sln" $properties
