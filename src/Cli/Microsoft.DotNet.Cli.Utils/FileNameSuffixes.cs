// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class FileNameSuffixes
    {
        public const string DepsJson = ".deps.json";
        public const string RuntimeConfigJson = ".runtimeconfig.json";
        public const string RuntimeConfigDevJson = ".runtimeconfig.dev.json";

        public static PlatformFileNameSuffixes CurrentPlatform
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return Windows;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return OSX;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return Linux;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD")))
                {
                    return FreeBSD;
                }
                else
                {
                    throw new InvalidOperationException("Unknown Platform");
                }
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

        public static PlatformFileNameSuffixes FreeBSD { get; } = Linux;

        public struct PlatformFileNameSuffixes
        {
            public string DynamicLib { get; internal set; }

            public string Exe { get; internal set; }

            public string ProgramDatabase { get; internal set; }

            public string StaticLib { get; internal set; }
        }
    }
}
