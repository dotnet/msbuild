using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class Constants
    {
        public static readonly string ExeSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        public static readonly string HostExecutableName = "corehost" + ExeSuffix;
        public static readonly string DefaultConfiguration = "Debug";
        public static readonly string BinDirectoryName = "bin";
        public static readonly string ObjDirectoryName = "obj";
        
        public static readonly string DynamicLibSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll"   : 
                                                         RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? ".dylib" : ".so";

        public static readonly string StaticLibSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".lib" : ".a" ;
    }
}
