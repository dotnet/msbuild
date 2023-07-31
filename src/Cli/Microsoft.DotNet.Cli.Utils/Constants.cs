// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public static class Constants
    {
        public const string DefaultConfiguration = "Debug";

        public static readonly string ProjectFileName = "project.json";
        public static readonly string ToolManifestFileName = "dotnet-tools.json";
        public static readonly string DotConfigDirectoryName = ".config";
        public static readonly string ExeSuffix =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        public static readonly string BinDirectoryName = "bin";
        public static readonly string ObjDirectoryName = "obj";
        public static readonly string GitDirectoryName = ".git";

        public static readonly string MSBUILD_EXE_PATH = "MSBUILD_EXE_PATH";
        public static readonly string MSBuildExtensionsPath = "MSBuildExtensionsPath";
        public static readonly string EnableDefaultItems = "EnableDefaultItems";

        public static readonly string ProjectArgumentName = "<PROJECT>";
        public static readonly string SolutionArgumentName = "<SLN_FILE>";
        public static readonly string ToolPackageArgumentName = "<PACKAGE_ID>";

        public static readonly string AnyRid = "any";

        public static readonly string RestoreInteractiveOption = "--interactive";
    }
}
