// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.PlatformAbstractions;
using NuGet.Frameworks;

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

        //  Encode relevant information from https://github.com/dotnet/core/blob/master/os-lifecycle-policy.md
        //  so that we can check if a test targeting a particular version of .NET Core should be
        //  able to run on the current OS
        public static bool SupportsTargetFramework(string targetFramework)
        {
            var nugetFramework = NuGetFramework.Parse(targetFramework);
            string currentRid = DotNet.PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier();

            string ridOS = currentRid.Split('.')[0];
            if (ridOS.Equals("alpine", StringComparison.OrdinalIgnoreCase))
            {
                if (nugetFramework.Version < new Version(2, 1, 0, 0))
                {
                    return false;
                }
            }
            else if (ridOS.Equals("fedora", StringComparison.OrdinalIgnoreCase))
            {
                string restOfRid = currentRid.Substring(ridOS.Length + 1);
                string fedoraVersionString = restOfRid.Split('-')[0];
                if (int.TryParse(fedoraVersionString, out int fedoraVersion))
                {
                    if (fedoraVersion <= 27)
                    {
                        if (nugetFramework.Version < new Version(2, 1, 0, 0))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (fedoraVersion == 28)
                    {
                        if (nugetFramework.Version < new Version(2, 1, 0, 0))
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else if (fedoraVersion >= 29)
                    {
                        if (nugetFramework.Version < new Version(2, 2, 0, 0))
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }
            else if (ridOS.Equals("rhel", StringComparison.OrdinalIgnoreCase))
            {
                string restOfRid = currentRid.Substring(ridOS.Length + 1);
                string rhelVersionString = restOfRid.Split('-')[0];
                if (int.TryParse(rhelVersionString, out int rhelVersion))
                {
                    if (rhelVersion == 6)
                    {
                        if (nugetFramework.Version < new Version(2, 0, 0, 0))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
