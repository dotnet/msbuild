// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadRestoreCommandParser
    {
        public static readonly Option ConfigOption = WorkloadInstallCommandParser.ConfigOption;

        public static readonly Option AddSourceOption = WorkloadInstallCommandParser.AddSourceOption;

        public static readonly Option VerbosityOption = WorkloadInstallCommandParser.VerbosityOption;

        public static Command GetCommand()
        {
            Command command = new Command("restore", LocalizableStrings.CommandDescription);

            command.AddOption(ConfigOption);
            command.AddOption(AddSourceOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            command.AddOption(VerbosityOption);

            return command;
        }
    }
}
