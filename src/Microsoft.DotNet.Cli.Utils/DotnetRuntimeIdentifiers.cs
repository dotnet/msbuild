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
            IEnumerable<string> fallbackIdentifiers = null;

            // If the machine's RID isn't supported by the shared framework (i.e. the CLI
            // is being used on a newer version of an OS), add the RID that the CLI was built 
            // with as a fallback.  The RID the CLI was built with will have the correct 
            // runtime.* NuGet packages available.
            // For example, when a user is using osx.10.12, but we only support osx.10.10 and 
            // osx.10.11, the project.json "runtimes" section cannot contain osx.10.12, since
            // that RID isn't contained in the runtime graph - users will get a restore error.
            FrameworkDependencyFile fxDepsFile = new FrameworkDependencyFile();
            if (!fxDepsFile.IsCurrentRuntimeSupported())
            {
                string buildRid = DotnetFiles.VersionFileObject.BuildRid;
                if (!string.IsNullOrEmpty(buildRid))
                {
                    fallbackIdentifiers = new string[] { buildRid };
                }
            }

            return RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers(fallbackIdentifiers);
        }
    }
}
