// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class PackCommandParser
    {
        public static Command Pack() =>
            Create.Command("pack",
                           ".NET Core NuGet Package Packer",
                           CommonOptions.HelpOption(),
                           Create.Option("-o|--output",
                                         "Directory in which to place built packages.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "OUTPUT_DIR")),
                           Create.Option("--no-build",
                                         "Skip building the project prior to packing. By default, the project will be built."),
                           Create.Option("--include-symbols",
                                         "Include packages with symbols in addition to regular packages in output directory."),
                           Create.Option("--include-source",
                                         "Include PDBs and source files. Source files go into the src folder in the resulting nuget package"),
                           Create.Option("-c|--configuration",
                                         "Configuration to use for building the project.  Default for most projects is  \"Debug\".",
                                         Accept.ExactlyOneArgument
                                               .With(name: "CONFIGURATION")
                                               .WithSuggestionsFrom("DEBUG",
                                                                    "RELEASE")),
                           Create.Option("--version-suffix",
                                         "Defines the value for the $(VersionSuffix) property in the project.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "VERSION_SUFFIX")),
                           Create.Option("-s|--serviceable",
                                         "Set the serviceable flag in the package. For more information, please see https://aka.ms/nupkgservicing."),
                           CommonOptions.VerbosityOption());
    }
}