// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An internal implementation of <see cref="Framework.SdkResolverContext"/>.
    /// </summary>
    internal sealed class SdkResolverContext : SdkResolverContextBase
    {
        public SdkResolverContext(Framework.SdkLogger logger, string projectFilePath, string solutionPath, Version msBuildVersion, IDictionary<string, string> globalProperties)
        {
            Logger = logger;
            ProjectFilePath = projectFilePath;
            SolutionFilePath = solutionPath;
            MSBuildVersion = msBuildVersion;
            GlobalProperties = new ReadOnlyDictionary<string, string>(globalProperties);
        }
    }
}
