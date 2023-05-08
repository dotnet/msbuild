// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            command.AddOption(CommonOptions.VerbosityOption);

            command.SetHandler((parseResult) => new WorkloadUninstallCommand(parseResult).Execute());

            return command;
        }
    }
}
