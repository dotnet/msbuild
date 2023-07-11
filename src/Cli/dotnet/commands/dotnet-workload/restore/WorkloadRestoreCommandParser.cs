// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Workloads.Workload.Restore;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadRestoreCommandParser
    {
        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("restore", LocalizableStrings.CommandDescription);

            command.Arguments.Add(RestoreCommandParser.SlnOrProjectArgument);
            WorkloadInstallCommandParser.AddWorkloadInstallCommandOptions(command);

            command.SetAction((parseResult) => new WorkloadRestoreCommand(parseResult).Execute());

            return command;
        }
    }
}
