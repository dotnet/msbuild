// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Pack
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Core NuGet Package Packer";

        public const string AppDescription = "pack for msbuild";

        public const string CmdOutputDir  = "OUTPUT_DIR";

        public const string CmdOutputDirDescription = "Directory in which to place built packages.";

        public const string CmdNoBuildOptionDescription = "Skip building the project prior to packing. By default, the project will be built.";

        public const string CmdIncludeSymbolsDescription = "Include packages with symbols in addition to regular packages in output directory.";

        public const string CmdIncludeSourceDescription  = "Include PDBs and source files. Source files go into the src folder in the resulting nuget package";

        public const string CmdServiceableDescription = "Set the serviceable flag in the package. For more information, please see https://aka.ms/nupkgservicing.";

        public const string CmdArgumentProject = "PROJECT";

        public const string CmdArgumentDescription = "The project to pack, defaults to the project file in the current directory. Can be a path to any project file";
    }
}
