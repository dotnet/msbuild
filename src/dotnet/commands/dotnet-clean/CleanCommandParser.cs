// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Clean.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class CleanCommandParser
    {
        public static Command Clean() =>
            Create.Command(
                "clean",
                ".NET Clean Command",
                Accept.ZeroOrMoreArguments,
                CommonOptions.HelpOption(),
                Create.Option("-o|--output", 
                              "Directory in which the build outputs have been placed.",
                              Accept.ExactlyOneArgument
                        .With(name: "OUTPUT_DIR")
                        .ForwardAs(o => $"/p:OutputPath={o.Arguments.Single()}")),
                CommonOptions.FrameworkOption(),
                CommonOptions.ConfigurationOption(),
                CommonOptions.VerbosityOption());
    }
}