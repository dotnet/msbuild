// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An internal implementation of <see cref="Framework.SdkResolverContext"/>.
    /// </summary>
    internal sealed class SdkResolverContext : SdkResolverContextBase
    {
        public SdkResolverContext(Framework.SdkLogger logger, string projectFilePath, string solutionPath, Version msBuildVersion, bool interactive)
        {
            Logger = logger;
            ProjectFilePath = projectFilePath;
            SolutionFilePath = solutionPath;
            MSBuildVersion = msBuildVersion;
            Interactive = interactive;
        }
    }
}
