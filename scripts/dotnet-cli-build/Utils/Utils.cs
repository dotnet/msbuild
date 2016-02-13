using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Build
{
    public static class Utils
    {
        public static void CleanNuGetTempCache()
        {
            // Clean NuGet Temp Cache on Linux (seeing some issues on Linux)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Directory.Exists("/tmp/NuGet"))
            {
                Directory.Delete("/tmp/NuGet", recursive: true);
            }
        }

        public static string GetOSName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
}
