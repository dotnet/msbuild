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
            else if (ridOS.Equals("ubuntu", StringComparison.OrdinalIgnoreCase))
            {
                string restOfRid = currentRid.Substring(ridOS.Length + 1);
                string ubuntuVersionString = restOfRid.Split('-')[0];
                if (float.TryParse(ubuntuVersionString, System.Globalization.CultureInfo.InvariantCulture, out float ubuntuVersion))
                {
                    if (ubuntuVersion > 16.04f)
                    {
                        if (nugetFramework.Version < new Version(2, 0, 0, 0))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    return true;
                }
            }
            else if (ridOS.Equals("osx", StringComparison.OrdinalIgnoreCase))
            {
                string restOfRid = currentRid.Substring(ridOS.Length + 1);
                string osxVersionString = restOfRid.Split('-')[0];
                if (float.TryParse(osxVersionString, out float osxVersion))
                {
                    //  .NET Core 1.1 - 10.11, 10.12
                    //  .NET Core 2.0 - 10.12+
                    //  .NET Core 2.1 - 10.12-10.15
                    //  .NET 5 <= 11.0
                    //  .NET 6 <= 12
                    //  .NET 7 <= 13
                    if (osxVersion <= 10.11f)
                    {
                        if (nugetFramework.Version >= new Version(2, 0, 0, 0))
                        {
                            return false;
                        }
                    }
                    else if (osxVersion == 10.12f)
                    {
                        if (nugetFramework.Version < new Version(2, 0, 0, 0))
                        {
                            return false;
                        }
                    }
                    else if (osxVersion > 10.12f && osxVersion <= 10.15f)
                    {
                        //  .NET Core 2.0 is out of support, and doesn't seem to work with OS X 10.14
                        //  (it finds no assets for the RID), even though the support page says "10.12+"
                        if (nugetFramework.Version < new Version(2, 1, 0, 0))
                        {
                            return false;
                        }
                    }
                    else if (osxVersion == 11.0f)
                    {
                        if (nugetFramework.Version < new Version(5, 0, 0, 0))
                        {
                            return false;
                        }
                    }
                    else if (osxVersion == 12.0f)
                    {
                        if (nugetFramework.Version < new Version(6, 0, 0, 0))
                        {
                            return false;
                        }
                    }
                    else if (osxVersion > 12.0f)
                    {
                        if (nugetFramework.Version < new Version(7, 0, 0, 0))
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
