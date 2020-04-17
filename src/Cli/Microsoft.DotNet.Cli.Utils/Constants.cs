// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class Constants
    {
        public const string DefaultConfiguration = "Debug";

        public static readonly string ProjectFileName = "project.json";
        public static readonly string DotConfigDirectoryName = ".config";
        public static readonly string ExeSuffix =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        public static readonly string BinDirectoryName = "bin";
        public static readonly string ObjDirectoryName = "obj";

        public static readonly string MSBUILD_EXE_PATH = "MSBUILD_EXE_PATH";
        public static readonly string MSBuildExtensionsPath = "MSBuildExtensionsPath";
        public static readonly string EnableDefaultItems = "EnableDefaultItems";

        public static readonly string ProjectArgumentName = "<PROJECT>";
        public static readonly string SolutionArgumentName = "<SLN_FILE>";
        public static readonly string ToolPackageArgumentName = "<PACKAGE_ID>";

        public static readonly string AnyRid = "any";

        public static readonly string RestoreInteractiveOption= "--interactive";
        public const string MsBuildInteractivePropertyName = "NuGetInteractive";
        public static readonly string MsBuildInteractiveOption= $"-property:{MsBuildInteractivePropertyName}=true";
    }
}
