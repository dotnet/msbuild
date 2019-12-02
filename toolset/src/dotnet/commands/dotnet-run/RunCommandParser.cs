// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Run;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RunCommandParser
    {
        public static Command Run() =>
            CreateWithRestoreOptions.Command(
                "run",
                LocalizableStrings.AppFullName,
                treatUnmatchedTokensAsErrors: false,
                arguments: Accept.ZeroOrMoreArguments()
                    .MaterializeAs(o => new RunCommand
                    (
                        configuration: o.SingleArgumentOrDefault("--configuration"),
                        framework: o.SingleArgumentOrDefault("--framework"),
                        runtime: o.SingleArgumentOrDefault("--runtime"),
                        noBuild: o.HasOption("--no-build"),
                        project: o.SingleArgumentOrDefault("--project"),
                        launchProfile: o.SingleArgumentOrDefault("--launch-profile"),
                        noLaunchProfile: o.HasOption("--no-launch-profile"),
                        noRestore: o.HasOption("--no-restore") || o.HasOption("--no-build"),
                        interactive: o.HasOption(Utils.Constants.RestoreInteractiveOption),
                        restoreArgs: o.OptionValuesToBeForwarded(),
                        args: o.Arguments
                    )),
                options: new[]
                {
                    CommonOptions.HelpOption(),
                    CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription),
                    CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription),
                    CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription),
                    Create.Option(
                        "-p|--project",
                        LocalizableStrings.CommandOptionProjectDescription,
                        Accept.ExactlyOneArgument()),
                    Create.Option(
                        "--launch-profile",
                        LocalizableStrings.CommandOptionLaunchProfileDescription,
                        Accept.ExactlyOneArgument()),
                    Create.Option(
                        "--no-launch-profile",
                        LocalizableStrings.CommandOptionNoLaunchProfileDescription,
                        Accept.NoArguments()),
                    Create.Option(
                        "--no-build",
                        LocalizableStrings.CommandOptionNoBuildDescription,
                        Accept.NoArguments()),
                    CommonOptions.InteractiveMsBuildForwardOption(),
                    CommonOptions.NoRestoreOption(),
                    CommonOptions.VerbosityOption()
                });
    }
}
