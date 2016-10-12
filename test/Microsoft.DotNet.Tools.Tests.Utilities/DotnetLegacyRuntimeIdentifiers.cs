// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class DotnetLegacyRuntimeIdentifiers
    {
        public static string InferLegacyRestoreRuntimeIdentifier()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                FrameworkDependencyFile fxDepsFile = new FrameworkDependencyFile();
                return fxDepsFile.SupportsCurrentRuntime() ?
                    RuntimeEnvironment.GetRuntimeIdentifier() :
                    DotnetFiles.VersionFileObject.BuildRid;

            }
            else
            {
                var arch = RuntimeEnvironment.RuntimeArchitecture.ToLowerInvariant();
                return "win7-" + arch;
            }
        }
    }
}