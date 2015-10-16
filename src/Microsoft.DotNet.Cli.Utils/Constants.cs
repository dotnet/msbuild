using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class Constants
    {
        // TODO: On Unix, exe suffix is empty string...
        public static readonly string ExeSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
        public static readonly string CoreConsoleName = "coreconsole" + ExeSuffix;
        public static readonly string CoreRunName = "corerun" + ExeSuffix;
        public static readonly string DefaultConfiguration = "Debug";
        public static readonly string BinDirectoryName = "bin";

        public static readonly string HostsPathEnvironmentVariable = "DOTNET_CLR_HOSTS_PATH";
        public static readonly string CSCPathEnvironmentVariable = "DOTNET_CSC_PATH";
    }
}
