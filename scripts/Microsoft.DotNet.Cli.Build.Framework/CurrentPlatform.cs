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

        public static bool IsUnix
        {
            get
            {
                return IsLinux || IsOSX;
            }
        }

        public static bool IsLinux
        {
            get
            {
                return IsUbuntu || IsCentOS;
            }
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
                case BuildPlatform.CentOS:
                    return IsCentOS;
                case BuildPlatform.Unix:
                    return IsUnix;
                case BuildPlatform.Linux:
                    return IsLinux;
                default:
                    throw new Exception("Unrecognized Platform.");
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
            else
            {
                return default(BuildPlatform);
            }
        }
    }
}