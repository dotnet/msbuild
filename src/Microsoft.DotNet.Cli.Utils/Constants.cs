// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class Constants
    {
        private static Platform CurrentPlatform => RuntimeEnvironment.OperatingSystemPlatform;
        public const string DefaultConfiguration = "Debug";

        public static readonly string ProjectFileName = "project.json";
        public static readonly string ExeSuffix = CurrentPlatform == Platform.Windows ? ".exe" : string.Empty;

        public static readonly string BinDirectoryName = "bin";
        public static readonly string ObjDirectoryName = "obj";

        public static readonly string MSBUILD_EXE_PATH = "MSBUILD_EXE_PATH";
        public static readonly string MSBuildExtensionsPath = "MSBuildExtensionsPath";

        public static readonly string ProjectArgumentName = "<PROJECT>";
        public static readonly string SolutionArgumentName = "<SLN_FILE>";
        public static readonly string ToolPackageArgumentName = "<PACKAGE_ID>";
    }
}
