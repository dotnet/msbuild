// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildCommandParser
    {
        public static Command Build() =>
            Create.Command("build",
                           ".NET Builder",
                           CommonOptions.HelpOption(),
                           Create.Option("-o|--output",
                                         "Output directory in which to place built artifacts.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "OUTPUT_DIR")),
                           Create.Option("-f|--framework",
                                         "Target framework to build for. The target framework has to be specified in the project file.",
                                         Accept.AnyOneOf(Suggest.TargetFrameworksFromProjectFile)),
                           Create.Option("-r|--runtime",
                                         "Target runtime to build for. The default is to build a portable application.",
                                         Accept.AnyOneOf(Suggest.RunTimesFromProjectFile)),
                           Create.Option("-c|--configuration",
                                         "Configuration to use for building the project. Default for most projects is  \"Debug\".",
                                         Accept.ExactlyOneArgument
                                               .With(name: "CONFIGURATION")
                                               .WithSuggestionsFrom("DEBUG", "RELEASE")),
                           Create.Option("--version-suffix",
                                         "Defines the value for the $(VersionSuffix) property in the project",
                                         Accept.ExactlyOneArgument
                                               .With(name: "VERSION_SUFFIX")),
                           Create.Option("--no-incremental",
                                         "Disables incremental build."),
                           Create.Option("--no-dependencies",
                                         "Set this flag to ignore project-to-project references and only build the root project"),
                           CommonOptions.VerbosityOption());
    }
}