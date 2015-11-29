using System.Runtime.InteropServices;

namespace Microsoft.DotNet.ProjectModel
{
    public static class FileNameSuffixes
    {
        public static PlatformFileNameSuffixes DotNet { get; } = new PlatformFileNameSuffixes
        {
            DynamicLib = ".dll",
            Exe = ".exe",
            ProgramDatabase = ".pdb",
            StaticLib = ".lib",
            Deps = ".deps",
        };

        public static PlatformFileNameSuffixes Windows { get; } = new PlatformFileNameSuffixes
        {
            DynamicLib = ".dll",
            Exe = ".exe",
            ProgramDatabase = ".pdb",
            StaticLib = ".lib",
            Deps = ".deps",
        };

        public static PlatformFileNameSuffixes OSX { get; } = new PlatformFileNameSuffixes
        {
            DynamicLib = ".dylib",
            Exe = "",
            ProgramDatabase = ".pdb",
            StaticLib = ".a",
            Deps = ".deps"
        };

        public struct PlatformFileNameSuffixes
        {
            public string DynamicLib { get; set; }

            public string Exe { get; set; }

            public string ProgramDatabase { get; set; }

            public string StaticLib { get; set; }

            public string Deps { get; set; }
        }
    }
}
