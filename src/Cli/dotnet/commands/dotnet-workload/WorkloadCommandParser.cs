// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCommandParser
    {
        public static Command GetCommand(bool includeAllCommands)
        {
            var command = new Command("workload", LocalizableStrings.CommandDescription);

            command.AddCommand(WorkloadInstallCommandParser.GetCommand());
            command.AddCommand(WorkloadUpdateCommandParser.GetCommand());
            command.AddCommand(WorkloadListCommandParser.GetCommand());
            if (includeAllCommands)
            {
                command.AddCommand(WorkloadUninstallCommandParser.GetCommand());
                command.AddCommand(WorkloadRestoreCommandParser.GetCommand());
            }

            return command;
        }
    }
}
