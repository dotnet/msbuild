#!/usr/bin/env bash

function InitializeCustomSDKToolset {
  if [[ "$restore" != true ]]; then
    return
  fi

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them in source build.
  if [[ $properties == *"ArcadeBuildFromSource=true"* ]]; then
    return
  fi

  DISTRO=
  MAJOR_VERSION=
  if [ -e /etc/os-release ]; then
      . /etc/os-release
      DISTRO="$ID"
      MAJOR_VERSION="${VERSION_ID:+${VERSION_ID%%.*}}"
  fi

  InitializeDotNetCli true
  
  if [[ "$DISTRO" != "ubuntu" || "$MAJOR_VERSION" -le 16 ]]; then
    InstallDotNetSharedFramework "1.0.5"
    InstallDotNetSharedFramework "1.1.2"
  fi
  InstallDotNetSharedFramework "2.1.0"
  InstallDotNetSharedFramework "2.2.8"
  InstallDotNetSharedFramework "3.1.0"
  InstallDotNetSharedFramework "5.0.0"
  InstallDotNetSharedFramework "6.0.0"

  CreateBuildEnvScript
}

# Installs additional shared frameworks for testing purposes
function InstallDotNetSharedFramework {
  local version=$1
  local dotnet_root=$DOTNET_INSTALL_DIR 
  local fx_dir="$dotnet_root/shared/Microsoft.NETCore.App/$version"

  if [[ ! -d "$fx_dir" ]]; then
    GetDotNetInstallScript "$dotnet_root"
    local install_script=$_GetDotNetInstallScript

    bash "$install_script" --version $version --install-dir "$dotnet_root" --runtime "dotnet" --skip-non-versioned-files
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install Shared Framework $version to '$dotnet_root' (exit code '$lastexitcode')."
      ExitWithExitCode $lastexitcode
    fi
  fi
}

function CreateBuildEnvScript {
  mkdir -p $artifacts_dir
  scriptPath="$artifacts_dir/sdk-build-env.sh"
  scriptContents="
#!/usr/bin/env bash
export DOTNET_MULTILEVEL_LOOKUP=0

export DOTNET_ROOT=$DOTNET_INSTALL_DIR
export DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$DOTNET_INSTALL_DIR

export PATH=$DOTNET_INSTALL_DIR:\$PATH
export NUGET_PACKAGES=$NUGET_PACKAGES
"

  echo "$scriptContents" > ${scriptPath}
}

InitializeCustomSDKToolset
