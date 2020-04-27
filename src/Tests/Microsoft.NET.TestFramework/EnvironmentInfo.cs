// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP

using System;
using System.Runtime.InteropServices;
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

        public static string GetCompatibleRid(string targetFramework = null)
        {
            string rid = RuntimeInformation.RuntimeIdentifier;

            if (targetFramework == null)
            {
                return rid;
            }

            if (string.Equals(targetFramework, "netcoreapp1.0", StringComparison.OrdinalIgnoreCase))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Version osVersion;
                    if (Version.TryParse(DotNet.Cli.Utils.RuntimeEnvironment.OperatingSystemVersion, out osVersion))
                    {
                        if (osVersion > new Version(10, 11))
                        {
                            //  netcoreapp1.0 only supports osx.10.10 and osx.10.11
                            rid = "osx.10.11-x64";
                        }
                        else if (osVersion > new Version(10, 12))
                        {
                            //  netcoreapp1.1 only supports up to osx.10.12
                            rid = "osx.10.12-x64";
                        }
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
            string currentRid = RuntimeInformation.RuntimeIdentifier;

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
            else if (ridOS.Equals("osx", StringComparison.OrdinalIgnoreCase))
            {
                string restOfRid = currentRid.Substring(ridOS.Length + 1);
                string osxVersionString = restOfRid.Split('-')[0];
                //  From a string such as "10.14", get the second part, e.g. "14"
                string osxVersionString2 = osxVersionString.Split('.')[1];
                if (int.TryParse(osxVersionString2, out int osxVersion))
                {
                    //  .NET Core 1.1 - 10.11, 10.12
                    //  .NET Core 2.0 - 10.12+
                    if (osxVersion <= 11)
                    {
                        if (nugetFramework.Version >= new Version(2, 0, 0, 0))
                        {
                            return false;
                        }
                    }
                    else if (osxVersion == 12)
                    {
                        if (nugetFramework.Version < new Version(2, 0, 0, 0))
                        {
                            return false;
                        }
                    }
                    else if (osxVersion > 12)
                    {
                        //  .NET Core 2.0 is out of support, and doesn't seem to work with OS X 10.14
                        //  (it finds no assets for the RID), even though the support page says "10.12+"
                        if (nugetFramework.Version < new Version(2, 1, 0, 0))
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

#endif
