// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.DotNet.Workloads.Workload.List;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadListCommandParser
    {
        // arguments are a list of workload to be detected
        public static readonly Option<bool> MachineReadableOption = new Option<bool>("--machine-readable") {IsHidden = true};

        public static readonly Option<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

        public static readonly Option<string> TempDirOption = 
            new Option<string>("--temp-dir", Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings.TempDirOptionDescription).Hide();
        
        public static readonly Option<bool> IncludePreviewsOption = 
            new Option<bool>("--include-previews", Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings.IncludePreviewOptionDescription).Hide();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("list", LocalizableStrings.CommandDescription);
            command.AddOption(MachineReadableOption);
            command.AddOption(CommonOptions.HiddenVerbosityOption);
            command.AddOption(VersionOption);
            command.AddOption(TempDirOption);
            command.AddOption(IncludePreviewsOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions(true);

            command.SetHandler((parseResult) => new WorkloadListCommand(parseResult).Execute());

            return command;
        }
    }
}
