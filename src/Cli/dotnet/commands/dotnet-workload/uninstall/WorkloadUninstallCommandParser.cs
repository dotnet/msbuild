// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUninstallCommandParser
    {
        public static readonly Argument WorkloadIdArgument = WorkloadInstallCommandParser.WorkloadIdArgument;

        public static readonly Option VerbosityOption = WorkloadInstallCommandParser.VerbosityOption;
        
        public static readonly Option VersionOption = new Option<string>("--sdk-version", Workloads.Workload.Install.LocalizableStrings.VersionOptionDescription)
        {
            ArgumentHelpName = Workloads.Workload.Install.LocalizableStrings.VersionOptionName
        };

        public static Command GetCommand()
        {
            Command command = new Command("uninstall", LocalizableStrings.CommandDescription);
            command.AddArgument(WorkloadIdArgument);

            return command;
        }
    }
}
