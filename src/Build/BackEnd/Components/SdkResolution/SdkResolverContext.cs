// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An internal implementation of <see cref="Framework.SdkResolverContext"/>.
    /// </summary>
    internal sealed class SdkResolverContext : SdkResolverContextBase
    {
        public SdkResolverContext(Framework.SdkLogger logger, string projectFilePath, string solutionPath, Version msBuildVersion, bool interactive, bool isRunningInVisualStudio)
        {
            Logger = logger;
            ProjectFilePath = projectFilePath;
            SolutionFilePath = solutionPath;
            MSBuildVersion = msBuildVersion;
            Interactive = interactive;
            IsRunningInVisualStudio = isRunningInVisualStudio;
        }
    }
}
