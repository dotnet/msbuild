// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class PublishCommandParser
    {
        public static Command Publish() =>
            Create.Command(
                "publish",
                ".NET Publisher",
                Accept.ZeroOrMoreArguments(),
                CommonOptions.HelpOption(),
                Create.Option("-f|--framework",
                              "Target framework to publish for. The target framework has to be specified in the project file.",
                              Accept.ExactlyOneArgument()
                                    .WithSuggestionsFrom(_ => Suggest.TargetFrameworksFromProjectFile())
                                    .With(name: "FRAMEWORK")
                                    .ForwardAs(o => $"/p:TargetFramework={o.Arguments.Single()}")),
                Create.Option("-r|--runtime",
                              "Publish the project for a given runtime. This is used when creating self-contained deployment. Default is to publish a framework-dependent app.",
                              Accept.ExactlyOneArgument()
                                    .WithSuggestionsFrom(_ => Suggest.RunTimesFromProjectFile())
                                    .With(name: "RUNTIME_IDENTIFIER")
                                    .ForwardAs(o => $"/p:RuntimeIdentifier={o.Arguments.Single()}")),
                Create.Option("-o|--output",
                              "Output directory in which to place the published artifacts.",
                              Accept.ExactlyOneArgument()
                                    .With(name: "OUTPUT_DIR")
                                    .ForwardAs(o => $"/p:PublishDir={o.Arguments.Single()}")),
                Create.Option("-c|--configuration", "Configuration to use for building the project.  Default for most projects is  \"Debug\".",
                              Accept.ExactlyOneArgument()
                                    .With(name: "CONFIGURATION")
                                    .WithSuggestionsFrom("DEBUG", "RELEASE")
                                    .ForwardAs(o => $"/p:Configuration={o.Arguments.Single()}")),
                Create.Option("--version-suffix", "Defines the value for the $(VersionSuffix) property in the project.",
                              Accept.ExactlyOneArgument()
                                    .With(name: "VERSION_SUFFIX")
                                    .ForwardAs(o => $"/p:VersionSuffix={o.Arguments.Single()}")),
                Create.Option("--filter", "The XML file that contains the list of packages to be excluded from publish step.",
                              Accept.ExactlyOneArgument()
                                    .With(name: "PROFILE_XML")
                                    .ForwardAs(o => $"/p:FilterProjectFiles={o.Arguments.Single()}")),
                CommonOptions.VerbosityOption());
    }
}