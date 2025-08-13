#!/usr/bin/env bash

function InitializeCustomSDKToolset {
  if [[ "$restore" != true ]]; then
    return
  fi

  CreateBuildEnvScript
  CreateLocalSdkRunnerScript
}

function CreateBuildEnvScript {
  mkdir -p $artifacts_dir
  scriptPath="$artifacts_dir/sdk-build-env.sh"
  scriptContents="
#!/usr/bin/env bash
export DOTNET_MULTILEVEL_LOOKUP=0

export DOTNET_ROOT=$artifacts_dir/bin/bootstrap/core
export PATH=$artifacts_dir/bin/bootstrap/core:\$PATH
export DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0
"

  echo "$scriptContents" > ${scriptPath}
}

function CreateLocalSdkRunnerScript {
  mkdir -p $artifacts_dir
  scriptPath="$artifacts_dir/localsdk"
  dotnetBin="$artifacts_dir/bin/bootstrap/core/dotnet"
  
  cat > "${scriptPath}" << 'EOF'
#!/usr/bin/env bash

artifacts_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dotnet_bin="$artifacts_dir/bin/bootstrap/core/dotnet"

if [[ ! -x "$dotnet_bin" ]]; then
  echo "localsdk: could not find dotnet at $dotnet_bin. Did you run build.sh?" >&2
  exit 2
fi

if [[ $# -lt 1 ]]; then
  echo "Usage: $(basename "$0") <dotnet-command> [args...]"
  echo "Example: $(basename "$0") --info"
  echo "Example: $(basename "$0") msbuild foo.csproj /t:Build"
  exit 1
fi

# Execute dotnet with the same environment variables as sdk-build-env.sh
# but scoped to this process only
env \
  DOTNET_MULTILEVEL_LOOKUP=0 \
  DOTNET_ROOT="$artifacts_dir/bin/bootstrap/core" \
  PATH="$artifacts_dir/bin/bootstrap/core:$PATH" \
  DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0 \
  "$dotnet_bin" "$@"
EOF

  chmod +x "${scriptPath}"
}

InitializeCustomSDKToolset
