// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.Repair;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Repair.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadRepairCommandParser
    {
        public static readonly CliOption<string> ConfigOption = InstallingWorkloadCommandParser.ConfigOption;

        public static readonly CliOption<string[]> SourceOption = InstallingWorkloadCommandParser.SourceOption;

        public static readonly CliOption<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("repair", LocalizableStrings.CommandDescription);

            command.Options.Add(VersionOption);
            command.Options.Add(ConfigOption);
            command.Options.Add(SourceOption);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);

            command.SetAction((parseResult) => new WorkloadRepairCommand(parseResult).Execute());

            return command;
        }
    }
}
