// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Uninstall.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUninstallCommandParser
    {
        public static readonly Argument PackageIdArgument = WorkloadInstallCommandParser.WorkloadIdArgument;

        public static Command GetCommand()
        {
            Command command = new Command("uninstall", LocalizableStrings.CommandDescription);
            command.AddArgument(PackageIdArgument);

            return command;
        }
    }
}
