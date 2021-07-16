// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadListCommandParser
    {
        // arguments are a list of workload to be detected
        public static readonly Option<bool> MachineReadableOption = new Option<bool>("--machine-readable") {IsHidden = true};

        public static readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption();

        public static readonly Option<string> VersionOption = WorkloadUpdateCommandParser.VersionOption;

        public static readonly Option<string> TempDirOption = WorkloadUpdateCommandParser.TempDirOption;
        
        public static readonly Option<bool> IncludePreviewsOption = WorkloadUpdateCommandParser.IncludePreviewsOption;

        public static Command GetCommand()
        {
            var command = new Command("list", LocalizableStrings.CommandDescription);
            command.AddOption(MachineReadableOption);
            command.AddOption(VerbosityOption);
            command.AddOption(VersionOption);
            command.AddOption(TempDirOption);
            command.AddOption(IncludePreviewsOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            return command;
        }
    }
}
