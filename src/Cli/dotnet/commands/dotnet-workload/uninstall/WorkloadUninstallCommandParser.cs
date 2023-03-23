// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Collections.Generic;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Uninstall.LocalizableStrings;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload.Uninstall;
using Microsoft.DotNet.Workloads.Workload;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUninstallCommandParser
    {
        public static readonly Argument<IEnumerable<string>> WorkloadIdArgument = WorkloadInstallCommandParser.WorkloadIdArgument;

        public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new Command("uninstall", LocalizableStrings.CommandDescription);
            command.AddArgument(WorkloadIdArgument);
            command.AddOption(WorkloadInstallCommandParser.SkipSignCheckOption);

            command.SetHandler((parseResult) => new WorkloadUninstallCommand(parseResult).Execute());

            return command;
        }
    }
}
