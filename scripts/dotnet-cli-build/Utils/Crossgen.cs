using System.IO;
using System.Linq;
using System;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Build
{
    internal static class Crossgen
    {
        public static string GetCrossgenPathForVersion(string coreClrVersion)
        {
            string arch = PlatformServices.Default.Runtime.RuntimeArchitecture;
            string packageId;
            if (CurrentPlatform.IsWindows)
            {
                packageId = $"runtime.win7-{arch}.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else if (CurrentPlatform.IsUbuntu)
            {
                packageId = "runtime.ubuntu.14.04-x64.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else if (CurrentPlatform.IsCentOS || CurrentPlatform.IsRHEL)
            {
                // CentOS runtime is in the runtime.rhel.7-x64... package.
                packageId = "runtime.rhel.7-x64.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else if (CurrentPlatform.IsOSX)
            {
                packageId = "runtime.osx.10.10-x64.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else if (CurrentPlatform.IsDebian)
            {
                packageId = "runtime.debian.8.2-x64.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else
            {
                return null;
            }

            return Path.Combine(
                Dirs.NuGetPackages,
                packageId,
                coreClrVersion,
                "tools",
                $"crossgen{Constants.ExeSuffix}");
        }
    }
}
