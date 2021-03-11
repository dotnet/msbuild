// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadInstallCommandParser
    {
        public static readonly Argument WorkloadIdArgument = new Argument<string>(LocalizableStrings.WorkloadIdArgumentName)
        {
            Arity = ArgumentArity.OneOrMore,
            Description = LocalizableStrings.WorkloadIdArgumentDescription
        };

        public static readonly Option VersionOption = new Option<string>("--version", LocalizableStrings.VersionOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.VersionOptionName
        };

        public static readonly Option ConfigOption = new Option<string>("--configfile", LocalizableStrings.ConfigFileOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.ConfigFileOptionName
        };

        public static readonly Option AddSourceOption = new Option<string[]>("--add-source", LocalizableStrings.AddSourceOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.AddSourceOptionName
        }.AllowSingleArgPerToken();

        public static readonly Option FrameworkOption = new Option<string>("--framework", LocalizableStrings.FrameworkOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.FrameworkOptionName
        };

        public static readonly Option VerbosityOption = CommonOptions.VerbosityOption();

        public static Command GetCommand()
        {
            var command = new Command("install", LocalizableStrings.CommandDescription);

            command.AddArgument(WorkloadIdArgument);
            command.AddOption(VersionOption);
            command.AddOption(ConfigOption);
            command.AddOption(AddSourceOption);
            command.AddOption(FrameworkOption);
            command.AddOption(WorkloadCommandRestorePassThroughOptions.DisableParallelOption);
            command.AddOption(WorkloadCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.AddOption(WorkloadCommandRestorePassThroughOptions.NoCacheOption);
            command.AddOption(WorkloadCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);

            return command;
        }
    }
}
