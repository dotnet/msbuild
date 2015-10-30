using System.Runtime.InteropServices;

namespace Microsoft.Extensions.ProjectModel
{
    public static class RuntimeIdentifier
    {
        public static string Current { get; } = DetermineRID();

        private static string DetermineRID()
        {
            // TODO: Not this, obviously. Do proper RID detection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win7-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "ubuntu.14.04-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx.10.10-x64";
            }
            return "unknown";
        }
    }
}
