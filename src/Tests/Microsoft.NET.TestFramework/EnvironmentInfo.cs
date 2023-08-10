// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                    Version osVersion = Environment.OSVersion.Version;
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
            else if (string.Equals(targetFramework, "netcoreapp1.1", StringComparison.OrdinalIgnoreCase))
            {
                if (OperatingSystem.IsWindows())
                {
                    // netcoreapp1.1 used version-specific RIDs to find host binaries, so use win10 here
                    rid = "win10-x64";
                }
            }

            return rid;
        }

        //  Encode relevant information from https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md
        //  so that we can check if a test targeting a particular version of .NET Core should be
        //  able to run on the current OS
        public static bool SupportsTargetFramework(string targetFramework)
        {
            NuGetFramework nugetFramework = null;
            try
            {
                nugetFramework = NuGetFramework.Parse(targetFramework);
            }
            catch
            {
                return false;
            }

            if (nugetFramework == null)
            {
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                return true;
            }

            if (OperatingSystem.IsLinux())
            {
                var osRelease = File.ReadAllLines("/etc/os-release");
                string osId = osRelease
                    .First(line => line.StartsWith("ID=", StringComparison.OrdinalIgnoreCase))
                    .Substring("ID=".Length)
                    .Trim('\"', '\'');

                string versionString = osRelease
                    .First(line => line.StartsWith("VERSION_ID=", StringComparison.OrdinalIgnoreCase))
                    .Substring("VERSION_ID=".Length)
                    .Trim('\"', '\'');
                if (osId.Equals("alpine", StringComparison.OrdinalIgnoreCase))
                {
                    if (nugetFramework.Version < new Version(2, 1, 0, 0))
                    {
                        return false;
                    }
                }
                else if (Version.TryParse(versionString, out Version osVersion))
                {
                    if (osId.Equals("fedora", StringComparison.OrdinalIgnoreCase))
                    {
                        if (osVersion.Major <= 27)
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
                        else if (osVersion.Major == 28)
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
                        else if (osVersion.Major >= 29)
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
                    else if (osId.Equals("rhel", StringComparison.OrdinalIgnoreCase))
                    {
                        if (osVersion.Major == 6)
                        {
                            if (nugetFramework.Version < new Version(2, 0, 0, 0))
                            {
                                return false;
                            }
                        }
                    }
                    else if (osId.Equals("ubuntu", StringComparison.OrdinalIgnoreCase))
                    {
                        if (osVersion > new Version(16, 04))
                        {
                            if (nugetFramework.Version < new Version(2, 0, 0, 0))
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                //  .NET Core 1.1 - 10.11, 10.12
                //  .NET Core 2.0 - 10.12+
                //  .NET Core 2.1 - 10.12-10.15
                //  .NET 5 <= 11.0
                //  .NET 6 <= 12
                //  .NET 7 <= 13
                Version osVersion = Environment.OSVersion.Version;
                if (osVersion <= new Version(10, 11))
                {
                    if (nugetFramework.Version >= new Version(2, 0, 0, 0))
                    {
                        return false;
                    }
                }
                else if (osVersion == new Version(10, 12))
                {
                    if (nugetFramework.Version < new Version(2, 0, 0, 0))
                    {
                        return false;
                    }
                }
                else if (osVersion > new Version(10, 12) && osVersion <= new Version(10, 15))
                {
                    //  .NET Core 2.0 is out of support, and doesn't seem to work with OS X 10.14
                    //  (it finds no assets for the RID), even though the support page says "10.12+"
                    if (nugetFramework.Version < new Version(2, 1, 0, 0))
                    {
                        return false;
                    }
                }
                else if (osVersion == new Version(11, 0))
                {
                    if (nugetFramework.Version < new Version(5, 0, 0, 0))
                    {
                        return false;
                    }
                }
                else if (osVersion == new Version(12, 0))
                {
                    if (nugetFramework.Version < new Version(6, 0, 0, 0))
                    {
                        return false;
                    }
                }
                else if (osVersion > new Version(12, 0))
                {
                    if (nugetFramework.Version < new Version(7, 0, 0, 0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

#endif
