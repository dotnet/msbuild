// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadRestoreCommandParser
    {
        public static Command GetCommand()
        {
            Command command = new Command("restore", LocalizableStrings.CommandDescription);

            command.AddArgument(RestoreCommandParser.SlnOrProjectArgument);
            WorkloadInstallCommandParser.AddWorkloadInstallCommandOptions(command);
            return command;
        }
    }
}
