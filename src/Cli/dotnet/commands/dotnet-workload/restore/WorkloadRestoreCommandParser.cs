// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload.Restore;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadRestoreCommandParser
    {
        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new Command("restore", LocalizableStrings.CommandDescription);

            command.AddArgument(RestoreCommandParser.SlnOrProjectArgument);
            WorkloadInstallCommandParser.AddWorkloadInstallCommandOptions(command);

            command.SetHandler((parseResult) => new WorkloadRestoreCommand(parseResult).Execute());

            return command;
        }
    }
}
