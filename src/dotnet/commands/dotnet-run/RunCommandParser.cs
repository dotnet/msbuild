// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Run;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RunCommandParser
    {
        public static Command Run() =>
            Create.Command(
                "run",
                LocalizableStrings.AppFullName,
                treatUnmatchedTokensAsErrors: false,
                arguments: Accept.ZeroOrMoreArguments()
                                 .MaterializeAs(o => new RunCommand
                                 {
                                     Configuration = o.SingleArgumentOrDefault("--configuration"),
                                     Framework = o.SingleArgumentOrDefault("--framework"),
                                     NoBuild = o.HasOption("--no-build"),
                                     Project = o.SingleArgumentOrDefault("--project"),
                                     Args = o.Arguments
                                 }),
                options: new[]
                {
                    CommonOptions.HelpOption(),
                    CommonOptions.ConfigurationOption(),
                    CommonOptions.FrameworkOption(),
                    Create.Option(
                        "-p|--project",
                        LocalizableStrings.CommandOptionProjectDescription,
                        Accept.ExactlyOneArgument()),
                    Create.Option(
                        "--no-build",
                        LocalizableStrings.CommandOptionNoBuildDescription,
                        Accept.NoArguments().ForwardAs("/p:NoBuild=true"))
                });
    }
}