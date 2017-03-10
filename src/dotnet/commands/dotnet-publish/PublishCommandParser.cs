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
                Accept.ZeroOrMoreArguments,
                CommonOptions.HelpOption(),
                CommonOptions.FrameworkOption(),
                CommonOptions.RuntimeOption(),
                Create.Option("-o|--output",
                              "Output directory in which to place the published artifacts.",
                              Accept.ExactlyOneArgument
                                    .With(name: "OUTPUT_DIR")
                                    .ForwardAs(o => $"/p:PublishDir={o.Arguments.Single()}")),
                CommonOptions.ConfigurationOption(),
                Create.Option("--version-suffix", "Defines the value for the $(VersionSuffix) property in the project.",
                              Accept.ExactlyOneArgument
                                    .With(name: "VERSION_SUFFIX")
                                    .ForwardAs(o => $"/p:VersionSuffix={o.Arguments.Single()}")),
                Create.Option("--filter", "The XML file that contains the list of packages to be excluded from publish step.",
                              Accept.ExactlyOneArgument
                                    .With(name: "PROFILE_XML")
                                    .ForwardAs(o => $"/p:FilterProjectFiles={o.Arguments.Single()}")),
                CommonOptions.VerbosityOption());
    }
}