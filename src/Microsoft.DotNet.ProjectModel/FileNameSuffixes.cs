using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.ProjectModel
{
    public static class FileNameSuffixes
    {
        public const string Deps = ".deps";

        public static PlatformFileNameSuffixes CurrentPlatform
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { return Windows; }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) { return Linux; }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { return OSX; }

                throw new InvalidOperationException("Unknown Platform");
            }
        }

        public static PlatformFileNameSuffixes DotNet { get; } = new PlatformFileNameSuffixes
        {
            DynamicLib = ".dll",
            Exe = ".exe",
            ProgramDatabase = ".pdb",
            StaticLib = ".lib"
        };

        public static PlatformFileNameSuffixes Windows { get; } = new PlatformFileNameSuffixes
        {
            DynamicLib = ".dll",
            Exe = ".exe",
            ProgramDatabase = ".pdb",
            StaticLib = ".lib"
        };

        public static PlatformFileNameSuffixes OSX { get; } = new PlatformFileNameSuffixes
        {
            DynamicLib = ".dylib",
            Exe = "",
            ProgramDatabase = ".pdb",
            StaticLib = ".a"
        };

        public static PlatformFileNameSuffixes Linux { get; } = new PlatformFileNameSuffixes
        {
            DynamicLib = ".so",
            Exe = "",
            ProgramDatabase = ".pdb",
            StaticLib = ".a"
        };

        public struct PlatformFileNameSuffixes
        {
            public string DynamicLib { get; internal set; }

            public string Exe { get; internal set; }

            public string ProgramDatabase { get; internal set; }

            public string StaticLib { get; internal set; }
        }
    }
}
