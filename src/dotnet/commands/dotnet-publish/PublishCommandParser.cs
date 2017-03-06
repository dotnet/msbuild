// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class PublishCommandParser
    {
        public static Command Publish() =>
            Create.Command("publish",
                           ".NET Publisher",
                           Accept.ExactlyOneArgument,
                           CommonOptions.HelpOption(),
                           Create.Option("-f|--framework",
                                         "Target framework to publish for. The target framework has to be specified in the project file.",
                                         Accept.AnyOneOf(Suggest.TargetFrameworksFromProjectFile)
                                               .With(name: "FRAMEWORK")),
                           Create.Option("-r|--runtime",
                                         "Publish the project for a given runtime. This is used when creating self-contained deployment. Default is to publish a framework-dependent app.",
                                         Accept.AnyOneOf(Suggest.RunTimesFromProjectFile)
                                               .With(name: "RUNTIME_IDENTIFIER")),
                           Create.Option("-o|--output",
                                         "Output directory in which to place the published artifacts.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "OUTPUT_DIR")),
                           Create.Option("-c|--configuration", "Configuration to use for building the project.  Default for most projects is  \"Debug\".",
                                         Accept.ExactlyOneArgument
                                               .With(name: "CONFIGURATION")
                                               .WithSuggestionsFrom("DEBUG", "RELEASE")),
                           Create.Option("--version-suffix", "Defines the value for the $(VersionSuffix) property in the project.",
                                         Accept.ExactlyOneArgument
                                               .With(name: "VERSION_SUFFIX")),
                           CommonOptions.VerbosityOption());
    }
}