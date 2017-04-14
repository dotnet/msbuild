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
                    (
                        configuration: o.SingleArgumentOrDefault("--configuration"),
                        framework: o.SingleArgumentOrDefault("--framework"),
                        noBuild: o.HasOption("--no-build"),
                        project: o.SingleArgumentOrDefault("--project"),
                        args: o.Arguments
                    )),
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
                        Accept.NoArguments())
                });
    }
}