#! /bin/bash

configuration="Debug"
test=false
ci=false
binary_log=false
exclude_ci_binary_log=false
stage2=false
stage2Arguments=
properties=

source="${BASH_SOURCE[0]}"

# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done

scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
while [[ $# -gt 0 ]]; do
  lowerI="$(echo $1 | awk '{print tolower($0)}')"
  case "$lowerI" in
    --configuration)
      configuration=$2
      shift 2
      ;;
    --test)
      test=true
      shift 1
      ;;
    --ci)
      ci=true
      shift 1
      ;;
    --binarylog|-bl)
      binary_log=true
      shift 1
      ;;
    --excludecibinarylog|-nobl)
      exclude_ci_binary_log=true
      shift 1
      ;;
    --stage2)
      stage2=true
      shift 1
      ;;
    --stage2arguments)
      stage2Arguments=$2
      # Supplying stage2Arguments implies a multi-stage build.
      stage2=true
      shift 2
      ;;
    *)
      properties="$properties $1"
      shift 1
      ;;
  esac
done

build_script="$scriptroot/common/build.sh"

# Arguments common to the stage1 and stage2 builds, including any caller-supplied properties.
common_build_args="--configuration $configuration $properties"

# Forward the binary-log-related switches to both the stage 1 and stage 2 builds.
if [ "$ci" = true ]; then
  common_build_args="$common_build_args --ci"
fi
if [ "$binary_log" = true ]; then
  common_build_args="$common_build_args --binaryLog"
fi
if [ "$exclude_ci_binary_log" = true ]; then
  common_build_args="$common_build_args --excludeCIBinarylog"
fi

build_args="$common_build_args"

# If the caller requested a multi-stage build, add the --prepareMachine switch to the stage 1 build so that it kills any lingering processes from stage 1 before stage 2 starts.
# Also disable the pipeline set result masking for stage 1 so that a stage 1 failure surfaces its real exit code to this wrapper (stage 2 is the terminal build that reports the pipeline result).
if [ "$stage2" = true ]; then
  build_args="$build_args --prepareMachine --disablePipelineSetResult"
fi

if [ "$test" = true ] && [ "$stage2" != true ]; then
  build_args="$build_args --test"
fi

# Log the stage 1 build command so that it's clear which arguments flow to it.
if [ "$stage2" = true ]; then
  echo "Stage 1 build: /bin/bash \"$build_script\" $build_args"
fi

/bin/bash "$build_script" $build_args
stage1_exit_code=$?

if [ "$stage2" != true ]; then
  exit $stage1_exit_code
fi

### END of stage1 build ###

if [ $stage1_exit_code -ne 0 ]; then
  echo "Stage 1 build failed with exit code $stage1_exit_code"
  exit $stage1_exit_code
fi

repo_root="$scriptroot/.."
artifacts_dir="$repo_root/artifacts"
stage1_dir="$repo_root/stage1"
stage1_bin_dir="$stage1_dir/bin"
perf_log_dir="$artifacts_dir/log/$configuration/PerformanceLogs"
bootstrap_root="$stage1_bin_dir/bootstrap"

# Clean a previous stage1 artifacts folder and move the stage 1 outputs aside so stage 2 gets a clean artifacts dir to build into.
rm -rf "$stage1_dir"
mv "$artifacts_dir" "$stage1_dir"

# The move above relocated the stage 1 binlog out of the published $artifacts_dir/log directory. Copy it
# back so CI publishes it alongside the stage 2 binlog. Best-effort: never fail the build if the stage 1
# binlog isn't there (e.g. when no binary log was produced).
stage1_binlog="$stage1_dir/log/$configuration/Build.binlog"
if [ -f "$stage1_binlog" ]; then
  published_log_dir="$artifacts_dir/log/$configuration"
  mkdir -p "$published_log_dir"
  cp -f "$stage1_binlog" "$published_log_dir/Build.stage1.binlog" || true
fi

build_tool_path="$bootstrap_root/core/dotnet"
build_tool_command="msbuild"
export DOTNET_ROOT="$bootstrap_root/core"

# Communicate the bootstrapped build tool to the (out-of-proc) stage 2 build.sh via environment
# variables so it does not require sourcing tools.sh here. tools.sh's InitializeBuildTool
# honors _BuildToolPath / _BuildToolCommand and only consumes Path and Command.
export _BuildToolPath="$build_tool_path"
export _BuildToolCommand="$build_tool_command"

# Ensure that debug bits fail fast, rather than hanging waiting for a debugger attach.
export MSBUILDDONOTLAUNCHDEBUGGER=true

# Opt into performance logging.
export DOTNET_PERFLOG_DIR="$perf_log_dir"

# Point child processes (stage 2 build.sh, tests, and the MSBuild grandchildren they spawn) at the
# freshly-built bootstrap .NET host, so task hosts launch with the bits under test. This matches
# DOTNET_ROOT and the bootstrap's own expectation that tests invoke the
# bootstrap dotnet (see eng/BootStrapMsBuild.targets).
export DOTNET_HOST_PATH="$bootstrap_root/core/dotnet"
export DOTNET_INSTALL_DIR="$bootstrap_root/core"

stage2_build_args="$common_build_args"

# Give the stage 2 binary log a distinct name so it doesn't collide with the stage 1 binlog
# (both otherwise default to Build.binlog) when CI publishes them to the same artifacts location.
# Only do this when a binary log will actually be produced, so we don't force one to be created:
# Arcade emits a binlog for CI builds (--ci) or when --binaryLog is passed explicitly, unless it's
# suppressed with --excludeCIBinarylog.
if { [ "$ci" = true ] || [ "$binary_log" = true ]; } && [ "$exclude_ci_binary_log" != true ]; then
  stage2_build_args="$stage2_build_args --binaryLogName Build.stage2.binlog"
fi

# Only run tests in stage2 when supplying the '--test' switch in a multi-stage build.
if [ "$test" = true ]; then
  stage2_build_args="$stage2_build_args --test"
fi

# $stage2Arguments are appended to the stage 2 build only.
# Use this for switches like /mt that should not be passed to the stage1 build
# until a stable version of MT is available in the images.
stage2_build_args="$stage2_build_args $stage2Arguments"

echo "Stage 2 build: /bin/bash \"$build_script\" $stage2_build_args"
# Needs to run out-of-proc to not inherit the stage 1 build's state variables.
/bin/bash "$build_script" $stage2_build_args
exit $?
