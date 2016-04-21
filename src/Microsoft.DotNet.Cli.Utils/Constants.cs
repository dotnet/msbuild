// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class Constants
    {
        private static Platform CurrentPlatform => PlatformServices.Default.Runtime.OperatingSystemPlatform;
        public const string DefaultConfiguration = "Debug";

        public static readonly string ProjectFileName = "project.json";
        public static readonly string ExeSuffix = CurrentPlatform == Platform.Windows ? ".exe" : string.Empty;
        public static readonly string ConfigSuffix = ".config";

        // Priority order of runnable suffixes to look for and run
        public static readonly string[] RunnableSuffixes = CurrentPlatform == Platform.Windows
                                                         ? new string[] { ".exe", ".cmd", ".bat" }
                                                         : new string[] { string.Empty };

        public static readonly string BinDirectoryName = "bin";
        public static readonly string ObjDirectoryName = "obj";

        public static readonly string DynamicLibSuffix = CurrentPlatform == Platform.Windows ? ".dll" :
                                                         CurrentPlatform == Platform.Darwin ? ".dylib" : ".so";

        public static readonly string LibCoreClrFileName = (CurrentPlatform == Platform.Windows ? "coreclr" : "libcoreclr");
        public static readonly string LibCoreClrName = LibCoreClrFileName + DynamicLibSuffix;

        public static readonly string RuntimeIdentifier = CurrentPlatform == Platform.Windows ? "win7-x64" :
                                                          CurrentPlatform == Platform.Darwin ? "osx.10.10-x64" : "ubuntu.14.04-x64";

        public static readonly string StaticLibSuffix = CurrentPlatform == Platform.Windows ? ".lib" : ".a";

        public static readonly string ResponseFileSuffix = ".rsp";

        public static readonly string PublishedHostExecutableName = "dotnet";
        public static readonly string HostExecutableName = "corehost" + ExeSuffix;
        public static readonly string[] HostBinaryNames = new string[] {
            HostExecutableName,
            (CurrentPlatform == Platform.Windows ? "hostpolicy" : "libhostpolicy") + DynamicLibSuffix,
            (CurrentPlatform == Platform.Windows ? "hostfxr" : "libhostfxr") + DynamicLibSuffix
        };

    }
}
