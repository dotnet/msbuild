// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// System.OperatingSystem static methods were added in net5.0.
    /// This class creates stand-in methods for net472 builds.
    /// Assumes only Windows is supported.
    /// </summary>
    internal static class OperatingSystem
    {
        public static bool IsOSPlatform(string platform)
        {
            return platform?.Equals("WINDOWS", StringComparison.OrdinalIgnoreCase) ?? throw new ArgumentNullException(nameof(platform));
        }

        public static bool IsOSPlatformVersionAtLeast(string platform, int major, int minor = 0, int build = 0, int revision = 0)
            => IsOSPlatform(platform) && IsOSVersionAtLeast(major, minor, build, revision);

        public static bool IsLinux() => false;

        public static bool IsFreeBSD() => false;

        public static bool IsFreeBSDVersionAtLeast(int major, int minor = 0, int build = 0, int revision = 0) => false;

        public static bool IsMacOS() => false;

        public static bool IsMacOSVersionAtLeast(int major, int minor = 0, int build = 0) => false;

        public static bool IsWindows() => true;

        public static bool IsWindowsVersionAtLeast(int major, int minor = 0, int build = 0, int revision = 0)
            => IsWindows() && IsOSVersionAtLeast(major, minor, build, revision);

        private static bool IsOSVersionAtLeast(int major, int minor, int build, int revision)
        {
            Version current = Environment.OSVersion.Version;

            if (current.Major != major)
            {
                return current.Major > major;
            }

            if (current.Minor != minor)
            {
                return current.Minor > minor;
            }

            if (current.Build != build)
            {
                return current.Build > build;
            }

            return current.Revision >= revision;
        }
    }
}
#endif

