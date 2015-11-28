using System.Runtime.InteropServices;

namespace Microsoft.DotNet.ProjectModel
{
    public static class FileNameSuffixes
    {
        public static readonly string DynamicLib = 
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib" : ".so";

        public static readonly string ExeSuffix = 
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        public const string ProgramDatabase = ".pdb";

        public static readonly string StaticLib = 
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".lib" : ".a" ;

        public static readonly string Deps = ".deps";
    }
}
