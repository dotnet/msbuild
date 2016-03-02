using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.PlatformAbstractions;

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

        public static bool IsUbuntu
        {
            get
            {
                var osname = PlatformServices.Default.Runtime.OperatingSystem;
                return string.Equals(osname, "ubuntu", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsCentOS
        {
            get
            {
                var osname = PlatformServices.Default.Runtime.OperatingSystem;
                return string.Equals(osname, "centos", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsRHEL
        {
            get
            {
                var osname = PlatformServices.Default.Runtime.OperatingSystem;
                return string.Equals(osname, "rhel", StringComparison.OrdinalIgnoreCase);
            }
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
            else
            {
                return default(BuildPlatform);
            }
        }
    }
}
