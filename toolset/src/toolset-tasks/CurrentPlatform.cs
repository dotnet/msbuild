// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.PlatformAbstractions;
using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;

namespace Microsoft.DotNet.Cli.Build.Framework
{
    public static class CurrentPlatform
    {
        public static BuildPlatform Current
        {
            get
            {
                return DetermineCurrentPlatform();
            }
        }

        public static bool IsWindows
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            }
        }

        public static bool IsOSX
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            }
        }

        public static bool IsFreeBSD
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"));
            }
        }

        public static bool IsUbuntu
        {
            get
            {
                var osname = RuntimeEnvironment.OperatingSystem;
                return string.Equals(osname, "ubuntu", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsCentOS
        {
            get
            {
                var osname = RuntimeEnvironment.OperatingSystem;
                return string.Equals(osname, "centos", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsRHEL
        {
            get
            {
                var osname = RuntimeEnvironment.OperatingSystem;
                return string.Equals(osname, "rhel", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsFedora
        {
            get
            {
                var osname = RuntimeEnvironment.OperatingSystem;
                return string.Equals(osname, "fedora", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsOpenSuse
        {
            get
            {
                var osname = RuntimeEnvironment.OperatingSystem;
                return string.Equals(osname, "opensuse", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsUnix
        {
            get
            {
                return IsLinux || IsOSX || IsFreeBSD;
            }
        }

        public static bool IsDebian
        {
            get
            {
                var osname = RuntimeEnvironment.OperatingSystem;
                return string.Equals(osname, "debian", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsLinux
        {
            get
            {
                return IsUbuntu || IsCentOS || IsRHEL || IsDebian || IsFedora || IsOpenSuse;
            }
        }

        public static bool IsPlatform(BuildPlatform platform, string version = null)
        {
            return IsPlatform(platform) && (version == null || IsVersion(version));
        }

        public static bool IsAnyPlatform(params BuildPlatform[] platforms)
        {
            return platforms.Any(p => IsPlatform(p));
        }

        public static bool IsPlatform(BuildPlatform platform)
        {
            switch (platform)
            {
                case BuildPlatform.Windows:
                    return IsWindows;
                case BuildPlatform.Ubuntu:
                    return IsUbuntu;
                case BuildPlatform.OSX:
                    return IsOSX;
                case BuildPlatform.FreeBSD:
                    return IsFreeBSD;
                case BuildPlatform.CentOS:
                    return IsCentOS;
                case BuildPlatform.RHEL:
                    return IsRHEL;
                case BuildPlatform.Debian:
                    return IsDebian;
                case BuildPlatform.Fedora:
                    return IsFedora;
                case BuildPlatform.OpenSuse:
                    return IsOpenSuse;
                case BuildPlatform.Unix:
                    return IsUnix;
                case BuildPlatform.Linux:
                    return IsLinux;
                default:
                    throw new Exception("Unrecognized Platform.");
            }
        }

        public static bool IsVersion(string version)
        {
            return RuntimeEnvironment.OperatingSystemVersion.Equals(version, StringComparison.OrdinalIgnoreCase);
        }

        private static BuildPlatform DetermineCurrentPlatform()
        {
            if (IsWindows)
            {
                return BuildPlatform.Windows;
            }
            else if (IsOSX)
            {
                return BuildPlatform.OSX;
            }
            else if (IsUbuntu)
            {
                return BuildPlatform.Ubuntu;
            }
            else if (IsCentOS)
            {
                return BuildPlatform.CentOS;
            }
            else if (IsRHEL)
            {
                return BuildPlatform.RHEL;
            }
            else if (IsDebian)
            {
                return BuildPlatform.Debian;
            }
            else if (IsFedora)
            {
                return BuildPlatform.Fedora;
            }
            else if (IsOpenSuse)
            {
                return BuildPlatform.OpenSuse;
            }
            else if (IsFreeBSD)
            {
                return BuildPlatform.FreeBSD;
            }
            else
            {
                return default(BuildPlatform);
            }
        }
    }
}
