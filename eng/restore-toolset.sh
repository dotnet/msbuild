#!/usr/bin/env bash

function InitializeCustomSDKToolset {
  if [[ "$restore" != true ]]; then
    return
  fi

  CreateBuildEnvScript
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

InitializeCustomSDKToolset
