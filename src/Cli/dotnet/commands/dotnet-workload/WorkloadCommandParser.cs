// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-workload";

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("workload", DocsLink, LocalizableStrings.CommandDescription);

            command.AddCommand(WorkloadInstallCommandParser.GetCommand());
            command.AddCommand(WorkloadUpdateCommandParser.GetCommand());
            command.AddCommand(WorkloadListCommandParser.GetCommand());
            command.AddCommand(WorkloadSearchCommandParser.GetCommand());
            command.AddCommand(WorkloadUninstallCommandParser.GetCommand());
            command.AddCommand(WorkloadRepairCommandParser.GetCommand());
            command.AddCommand(WorkloadRestoreCommandParser.GetCommand());
            command.AddCommand(WorkloadElevateCommandParser.GetCommand());

            command.SetHandler((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
