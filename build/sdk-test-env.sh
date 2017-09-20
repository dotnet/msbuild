#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done

REPO_ROOT="$( cd -P "$( dirname "$SOURCE" )/../" && pwd )"

STAGE0_DIR=$REPO_ROOT/.dotnet_cli
SDK_CLI_VERSION="$( cat $REPO_ROOT/DotnetCLIVersion.txt )"

export DOTNET_MULTILEVEL_LOOKUP=0
export PATH=$STAGE0_DIR:$PATH
export NUGET_PACKAGES=$REPO_ROOT/packages
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export MSBuildSDKsPath=$REPO_ROOT/bin/Debug/Sdks
export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=$REPO_ROOT/bin/Debug/Sdks
export NETCoreSdkBundledVersionsProps=$REPO_ROOT/.dotnet_cli/sdk/%SDK_CLI_VERSION%/Microsoft.NETCoreSdk.BundledVersions.props
export CustomAfterMicrosoftCommonTargets=$REPO_ROOT/bin/Debug/Sdks/Microsoft.NET.Build.Extensions/msbuildExtensions-ver/Microsoft.Common.Targets/ImportAfter/Microsoft.NET.Build.Extensions.targets
export MicrosoftNETBuildExtensionsTargets=$REPO_ROOT/bin/Debug/Sdks/Microsoft.NET.Build.Extensions/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
