// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Tools.Run;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RunCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-run";

        public static readonly Option<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly Option<string> RuntimeOption = CommonOptions.RuntimeOption;

        public static readonly Option<string> ProjectOption = new Option<string>("--project", LocalizableStrings.CommandOptionProjectDescription);

        public static readonly Option<IEnumerable<string>> PropertyOption =
            new ForwardedOption<IEnumerable<string>>(new string[] { "--property", "-p" }, LocalizableStrings.PropertyOptionDescription)
                .SetForwardingFunction((values, parseResult) => parseResult.GetRunCommandPropertyValues().Select(value => $"-p:{value}"));

        public static readonly Option<string> LaunchProfileOption = new Option<string>(new string[] { "--launch-profile", "-lp" }, LocalizableStrings.CommandOptionLaunchProfileDescription);

        public static readonly Option<bool> NoLaunchProfileOption = new Option<bool>("--no-launch-profile", LocalizableStrings.CommandOptionNoLaunchProfileDescription);

        public static readonly Option<bool> NoBuildOption = new Option<bool>("--no-build", LocalizableStrings.CommandOptionNoBuildDescription);

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveMsBuildForwardOption;

        public static readonly Option SelfContainedOption = CommonOptions.SelfContainedOption;

        public static readonly Option NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

        public static readonly Argument<IEnumerable<string>> ApplicationArguments = new Argument<IEnumerable<string>>("applicationArguments", () => Array.Empty<string>(), "Arguments passed to the application that is being run.");

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("run", DocsLink, LocalizableStrings.AppFullName);

            command.AddOption(ConfigurationOption);
            command.AddOption(FrameworkOption);
            command.AddOption(RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.AddOption(ProjectOption);
            command.AddOption(PropertyOption);
            command.AddOption(LaunchProfileOption);
            command.AddOption(NoLaunchProfileOption);
            command.AddOption(NoBuildOption);
            command.AddOption(InteractiveOption);
            command.AddOption(NoRestoreOption);
            command.AddOption(SelfContainedOption);
            command.AddOption(NoSelfContainedOption);
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddOption(CommonOptions.ArchitectureOption);
            command.AddOption(CommonOptions.OperatingSystemOption);

            command.AddArgument(ApplicationArguments);

            command.SetHandler(RunCommand.Run);

            return command;
        }
    }
}
