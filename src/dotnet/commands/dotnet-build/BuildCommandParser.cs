// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class BuildCommandParser
    {
        public static Command Build() =>
            Create.Command(
                "build",
                ".NET Builder",
                Accept.ZeroOrMoreArguments,
                CommonOptions.HelpOption(),
                Create.Option(
                    "-o|--output",
                    "Output directory in which to place built artifacts.",
                    Accept.ExactlyOneArgument
                          .With(name: "OUTPUT_DIR")
                          .ForwardAs(o => $"/p:OutputPath={o.Arguments.Single()}")),
                CommonOptions.FrameworkOption(),
                CommonOptions.RuntimeOption(),
                CommonOptions.ConfigurationOption(),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "--no-incremental",
                    "Disables incremental build."),
                Create.Option(
                    "--no-dependencies",
                    "Set this flag to ignore project-to-project references and only build the root project",
                    Accept.NoArguments
                          .ForwardAs("/p:BuildProjectReferences=false")),
                CommonOptions.VerbosityOption());
    }
}