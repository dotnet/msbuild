function InitializeCustomSDKToolset {
  if [[ "$restore" != true ]]; then
    return
  fi

  # Enable verbose VS Test Console logging
  export VSTEST_BUILD_TRACE=1
  export VSTEST_TRACE_BUILD=1

  export DOTNET_CLI_TELEMETRY_PROFILE='$ARCHITECTURE;https://github.com/dotnet/cli'

  # Turn off MSBuild Node re-use
  export MSBUILDDISABLENODEREUSE=1

  # Workaround for the sockets issue when restoring with many nuget feeds.
  export DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0

  INSTALL_ARCHITECTURE=$ARCHITECTURE
  archlower="$(echo $ARCHITECTURE | awk '{print tolower($0)}')"
  if [[ $archlower == 'arm'* ]]; then
      INSTALL_ARCHITECTURE="x64"
  fi

  # Increases the file descriptors limit for this bash. It prevents an issue we were hitting during restore
  FILE_DESCRIPTOR_LIMIT=$( ulimit -n )
  if [ $FILE_DESCRIPTOR_LIMIT -lt 1024 ]
  then
      echo "Increasing file description limit to 1024"
      ulimit -n 1024
  fi

  # The following frameworks and tools are used only for testing.
  # Don't install in source build or when cross-compiling
  if [[ "${DotNetBuildFromSource:-}" == "true" || "$ARCHITECTURE" != "$INSTALL_ARCHITECTURE" ]]; then
    return
  fi

  InitializeDotNetCli true
  InstallDotNetSharedFramework "1.1.2"
  InstallDotNetSharedFramework "2.0.0"
  InstallDotNetSharedFramework "2.2.0"
}

# Installs additional shared frameworks for testing purposes
function InstallDotNetSharedFramework {
  local version=$1
  local dotnet_root=$DOTNET_INSTALL_DIR
  local fx_dir="$dotnet_root/shared/Microsoft.NETCore.App/$version"

  if [[ ! -d "$fx_dir" ]]; then
    GetDotNetInstallScript "$dotnet_root"
    local install_script=$_GetDotNetInstallScript

    bash "$install_script" --version $version --install-dir "$dotnet_root" --runtime "dotnet" || true
  fi
}

InitializeCustomSDKToolset
