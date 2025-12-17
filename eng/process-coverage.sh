#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"
repoRoot=''
verbosity='minimal'

while [[ $# > 0 ]]; do
  opt="$(echo "$1" | tr "[:upper:]" "[:lower:]")"
  case "$opt" in
    --reporoot)
      repoRoot=$2
      shift
      ;;
    --verbosity)
      verbosity=$2
      shift
      ;;
    *)
      echo "Invalid argument: $1"
      usage
      exit 1
      ;;
  esac

  shift
done

eng_root="${repoRoot%%/}/eng"
. "$eng_root/restore-dotnet-coverage.sh"

artifacts_dir="${repoRoot%%/}/artifacts"
tools_dir="${repoRoot%%/}/.tools"

cd $repoRoot

coverageResultsDir="$artifacts_dir/CoverageResults"
rm -rf $coverageResultsDir || true

dotnetCoverageTool=$tools_dir/dotnet-coverage/dotnet-coverage

mergedCoverage=$artifacts_dir/CoverageResults/merged.coverage
mergedCobertura=$artifacts_dir/CoverageResults/merged.cobertura.xml

mkdir -p $coverageResultsDir

$dotnetCoverageTool merge -o $mergedCoverage $artifacts_dir/TestResults/**/*.coverage
$dotnetCoverageTool merge -o $mergedCobertura -f cobertura $mergedCoverage

cd $repoRoot
