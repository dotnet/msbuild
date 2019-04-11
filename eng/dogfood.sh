#!/bin/bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  scriptroot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$scriptroot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
scriptroot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

. "$scriptroot/common/tools.sh"
. "$scriptroot/configure-toolset.sh"
InitializeToolset
. "$scriptroot/restore-toolset.sh"

ReadGlobalVersion "dotnet"
dotnet_sdk_version=$_ReadGlobalVersion

export SDK_REPO_ROOT="$repo_root"
export SDK_CLI_VERSION="$dotnet_sdk_version"
export MSBuildSDKsPath="$artifacts_dir/bin/$configuration/Sdks"
export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR="$MSBuildSDKsPath"
export NETCoreSdkBundledVersionsProps="$DOTNET_INSTALL_DIR/sdk/$dotnet_sdk_version/Microsoft.NETCoreSdk.BundledVersions.props"
export MicrosoftNETBuildExtensionsTargets="$MSBuildSDKsPath/Microsoft.NET.Build.Extensions/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets"
