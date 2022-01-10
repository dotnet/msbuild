// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using SdkResolverContextBase2 = Microsoft.Build.Framework.SdkResolverContext2;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An internal implementation of <see cref="Framework.SdkResolverContext2"/>.
    /// </summary>
    internal sealed class SdkResolverContext : SdkResolverContextBase2
    {
        public SdkResolverContext(Framework.SdkLogger logger, string projectFilePath, string solutionPath, Version msBuildVersion, bool interactive, bool isRunningInVisualStudio, string options)
        {
            Logger = logger;
            ProjectFilePath = projectFilePath;
            SolutionFilePath = solutionPath;
            MSBuildVersion = msBuildVersion;
            Interactive = interactive;
            IsRunningInVisualStudio = isRunningInVisualStudio;
            Options = options;
        }
    }
}
