// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.PlatformAbstractions;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class EnvironmentInfo
    {
        public static bool HasSharedFramework(string framework)
        {
            string runtimesFolder =
                Path.Combine(new RepoDirectoriesProvider().DotnetRoot, "shared", "Microsoft.NETCore.App");

            var sharedRuntimes = Directory.EnumerateDirectories(runtimesFolder);

            if (framework == "netcoreapp1.0")
            {
                if (!sharedRuntimes.Any(s => s.StartsWith("1.0")))
                {
                    return false;
                }
            }
            else if (framework == "netcoreapp1.1")
            {
                if (!sharedRuntimes.Any(s => s.StartsWith("1.1")))
                {
                    return false;
                }
            }
            else if (framework == "netcoreapp2.0")
            {
                if (!sharedRuntimes.Any(s => s.StartsWith("2.0")))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
