// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.InternalAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class DotnetRuntimeIdentifiers
    {
        public static IEnumerable<string> InferCurrentRuntimeIdentifiers()
        {
            // On non-Windows machines, the CLI may be used on a newer version of a supported OS
            // and the current RID may not be available in the runtime.* NuGet packages, yet.
            // so fallback to the RID that was used to build the CLI - which will have the correct
            // runtime.* NuGet packages available.
            IEnumerable<string> fallbackIdentifiers = null;
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Windows)
            {
                string buildRid = DotnetFiles.VersionFileObject.BuildRid;
                if (!string.IsNullOrEmpty(buildRid))
                {
                    fallbackIdentifiers = new string[] { DotnetFiles.VersionFileObject.BuildRid };
                }
            }

            return RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers(fallbackIdentifiers);
        }
    }
}
