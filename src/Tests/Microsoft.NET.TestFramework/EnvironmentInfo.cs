// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.NET.TestFramework
{
    public static class EnvironmentInfo
    {
        public static string ExecutableExtension
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            }
        }

        public static string GetCompatibleRid(string targetFramework)
        {
            string rid = DotNet.PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier();

            if (string.Equals(targetFramework, "netcoreapp1.0", StringComparison.OrdinalIgnoreCase))
            {
                // netcoreapp1.0 only supports osx.10.10 and osx.10.11
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Version osVersion;
                    if (Version.TryParse(DotNet.PlatformAbstractions.RuntimeEnvironment.OperatingSystemVersion, out osVersion) &&
                        osVersion > new Version(10, 11))
                    {
                        rid = "osx.10.11-x64";
                    }
                }
            }

            return rid;
        }
    }
}
