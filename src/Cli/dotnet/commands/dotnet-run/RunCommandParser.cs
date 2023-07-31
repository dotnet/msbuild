// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Run;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RunCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-run";

        public static readonly CliOption<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        public static readonly CliOption<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly CliOption<string> RuntimeOption = CommonOptions.RuntimeOption;

        public static readonly CliOption<string> ProjectOption = new("--project")
        {
            Description = LocalizableStrings.CommandOptionProjectDescription
        };

        public static readonly CliOption<IEnumerable<string>> PropertyOption = new ForwardedOption<IEnumerable<string>>("--property", "-p")
        {
            Description = LocalizableStrings.PropertyOptionDescription
        }.SetForwardingFunction((values, parseResult) => parseResult.GetRunCommandPropertyValues().Select(value => $"-p:{value}"));

        public static readonly CliOption<string> LaunchProfileOption = new("--launch-profile", "-lp")
        {
            Description = LocalizableStrings.CommandOptionLaunchProfileDescription
        };

        public static readonly CliOption<bool> NoLaunchProfileOption = new("--no-launch-profile")
        {
            Description = LocalizableStrings.CommandOptionNoLaunchProfileDescription
        };

        public static readonly CliOption<bool> NoBuildOption = new("--no-build")
        {
            Description = LocalizableStrings.CommandOptionNoBuildDescription
        };

        public static readonly CliOption<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly CliOption<bool> InteractiveOption = CommonOptions.InteractiveMsBuildForwardOption;

        public static readonly CliOption SelfContainedOption = CommonOptions.SelfContainedOption;

        public static readonly CliOption NoSelfContainedOption = CommonOptions.NoSelfContainedOption;

        public static readonly CliArgument<IEnumerable<string>> ApplicationArguments = new("applicationArguments")
        {
            DefaultValueFactory = _ => Array.Empty<string>(),
            Description = "Arguments passed to the application that is being run."
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            DocumentedCommand command = new("run", DocsLink, LocalizableStrings.AppFullName);

            command.Options.Add(ConfigurationOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.Options.Add(ProjectOption);
            command.Options.Add(PropertyOption);
            command.Options.Add(LaunchProfileOption);
            command.Options.Add(NoLaunchProfileOption);
            command.Options.Add(NoBuildOption);
            command.Options.Add(InteractiveOption);
            command.Options.Add(NoRestoreOption);
            command.Options.Add(SelfContainedOption);
            command.Options.Add(NoSelfContainedOption);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(CommonOptions.ArchitectureOption);
            command.Options.Add(CommonOptions.OperatingSystemOption);
            command.Options.Add(CommonOptions.DisableBuildServersOption);

            command.Arguments.Add(ApplicationArguments);

            command.SetAction(RunCommand.Run);

            return command;
        }
    }
}
