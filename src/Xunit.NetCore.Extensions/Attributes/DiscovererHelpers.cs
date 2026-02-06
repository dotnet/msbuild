// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.InteropServices;

namespace Xunit
{
    internal static class DiscovererHelpers
    {
        internal static bool TestPlatformApplies(TestPlatforms platforms)
        {
            if (platforms.HasFlag(TestPlatforms.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return true;
            }

#if NET
            if (platforms.HasFlag(TestPlatforms.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.NetBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.illumos) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("ILLUMOS")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.Solaris) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("SOLARIS")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.iOS) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("IOS")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.tvOS) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("TVOS")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.Android) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.Browser) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.MacCatalyst) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("MACCATALYST")))
            {
                return true;
            }

            if (platforms.HasFlag(TestPlatforms.Wasi) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("WASI")))
            {
                return true;
            }
#endif

            return false;
        }
    }
}
