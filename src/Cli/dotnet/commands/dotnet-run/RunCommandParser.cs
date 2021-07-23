// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Run.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RunCommandParser
    {
        public static readonly Option<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly Option<string> RuntimeOption = CommonOptions.RuntimeOption(LocalizableStrings.RuntimeOptionDescription);

        public static readonly Option<string> ProjectOption = new Option<string>("--project", LocalizableStrings.CommandOptionProjectDescription);

        public static readonly Option<string> ProjectOptionShort = new Option<string>("-p", LocalizableStrings.CommandOptionProjectDescription)
        {
            IsHidden = true
        };

        public static readonly Option<string> LaunchProfileOption = new Option<string>("--launch-profile", LocalizableStrings.CommandOptionLaunchProfileDescription);

        public static readonly Option<bool> NoLaunchProfileOption = new Option<bool>("--no-launch-profile", LocalizableStrings.CommandOptionNoLaunchProfileDescription);

        public static readonly Option<bool> NoBuildOption = new Option<bool>("--no-build", LocalizableStrings.CommandOptionNoBuildDescription);

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption();

        public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveMsBuildForwardOption();

        public static Command GetCommand()
        {
            var command = new Command("run", LocalizableStrings.AppFullName);

            command.AddOption(ConfigurationOption);
            command.AddOption(FrameworkOption);
            command.AddOption(RuntimeOption);
            command.AddOption(ProjectOption);
            command.AddOption(ProjectOptionShort);
            command.AddOption(LaunchProfileOption);
            command.AddOption(NoLaunchProfileOption);
            command.AddOption(NoBuildOption);
            command.AddOption(InteractiveOption);
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.VerbosityOption());
            command.AddOption(CommonOptions.ArchitectureOption());
            command.AddOption(CommonOptions.OperatingSystemOption());
            command.TreatUnmatchedTokensAsErrors = false;

            return command;
        }
    }
}
