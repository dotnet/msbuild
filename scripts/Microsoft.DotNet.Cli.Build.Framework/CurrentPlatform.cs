using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.PlatformAbstractions;

public static class CurrentPlatform
{
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

    public static bool IsLinux
    {
        get
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
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
}