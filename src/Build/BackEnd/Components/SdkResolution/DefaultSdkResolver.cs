// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    ///     Default SDK resolver for compatibility with VS2017 RTM.
    /// <remarks>
    ///     Default Sdk folder will to:
    ///         1) MSBuildSDKsPath environment variable if defined
    ///         2) When in Visual Studio, (VSRoot)\MSBuild\Sdks\
    ///         3) Outside of Visual Studio (MSBuild Root)\Sdks\
    /// </remarks>
    /// </summary>
    internal class DefaultSdkResolver : SdkResolver
    {
        public override string Name => "DefaultSdkResolver";

        public override int Priority => 10000;

        public override SdkResult Resolve(SdkReference sdk, SdkResolverContext context, SdkResultFactory factory)
        {
            var sdkPath = Path.Combine(BuildEnvironmentHelper.Instance.MSBuildSDKsPath, sdk.Name, "Sdk");

            // Note: On failure MSBuild will log a generic message, no need to indicate a failure reason here.
            return FileUtilities.DirectoryExistsNoThrow(sdkPath)
                ? factory.IndicateSuccess(sdkPath, string.Empty)
                : factory.IndicateFailure(null);
        }
    }
}
