function InitializeCustomSDKToolset {
  if [[ "$dogfood" == true ]]; then
    export SDK_REPO_ROOT="$RepoRoot"
    export SDK_CLI_VERSION="$DotNetCliVersion"
    export MSBuildSDKsPath="$ArtifactsConfigurationDir/bin/Sdks"
    export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR="$MSBuildSDKsPath"
    export NETCoreSdkBundledVersionsProps="$DotNetRoot/sdk/$DotNetCliVersion/Microsoft.NETCoreSdk.BundledVersions.props"
    export CustomAfterMicrosoftCommonTargets="$MSBuildSDKsPath/Microsoft.NET.Build.Extensions/msbuildExtensions-ver/Microsoft.Common.Targets/ImportAfter/Microsoft.NET.Build.Extensions.targets"
    export MicrosoftNETBuildExtensionsTargets="$CustomAfterMicrosoftCommonTargets"
  fi

  if [[ "$restore" != true ]]; then
    return
  fi

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them in source build.
  if [[ "$DotNetBuildFromSource" == "true" ]]; then
    return
  fi
  
  InstallDotNetSharedFramework $DOTNET_INSTALL_DIR "1.0.5"
  InstallDotNetSharedFramework $DOTNET_INSTALL_DIR "1.1.2"
  InstallDotNetSharedFramework $DOTNET_INSTALL_DIR "2.1.0"
}

# Installs additional shared frameworks for testing purposes
function InstallDotNetSharedFramework {
  local dotnet_root=$1
  local version=$2
  local fx_dir="$dotnet_root/shared/Microsoft.NETCore.App/$version"

  if [[ ! -d "$fx_dir" ]]; then
    local install_script=`GetDotNetInstallScript $dotnet_root`
    
    bash "$install_script" --version $version --install-dir $dotnet_root --shared-runtime
    local lastexitcode=$?
    
    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install Shared Framework $version to '$dotnet_root' (exit code '$lastexitcode')."
      ExitWithExitCode $lastexitcode
    fi
  fi
}

InitializeCustomSDKToolset