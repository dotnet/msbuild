#!/usr/bin/env bash
set -euo pipefail

stamp_output=
if [[ "${1:-}" == "--stamp-output" ]]; then
  if [[ $# -lt 2 ]]; then
    echo "--stamp-output requires a path" >&2
    exit 1
  fi

  stamp_output="$2"
  shift 2
fi

repo_root="${BUILD_WORKSPACE_DIRECTORY:-$PWD}"

cd "$repo_root"
./build.sh "$@"

if [[ -n "$stamp_output" ]]; then
  mkdir -p "$(dirname "$stamp_output")"
  touch "$stamp_output"
fi
