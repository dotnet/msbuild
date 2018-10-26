#!/bin/bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  scriptroot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$scriptroot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
scriptroot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

. "$scriptroot/common/tools.sh"

InitializeTools

export SDK_REPO_ROOT="$RepoRoot"
export SDK_CLI_VERSION="$DotNetCliVersion"
export MSBuildSDKsPath="$ArtifactsConfigurationDir/bin/Sdks"
export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR="$MSBuildSDKsPath"
export NETCoreSdkBundledVersionsProps="$DotNetRoot/sdk/$DotNetCliVersion/Microsoft.NETCoreSdk.BundledVersions.props"
export CustomAfterMicrosoftCommonTargets="$MSBuildSDKsPath/Microsoft.NET.Build.Extensions/msbuildExtensions-ver/Microsoft.Common.Targets/ImportAfter/Microsoft.NET.Build.Extensions.targets"
export MicrosoftNETBuildExtensionsTargets="$CustomAfterMicrosoftCommonTargets"
