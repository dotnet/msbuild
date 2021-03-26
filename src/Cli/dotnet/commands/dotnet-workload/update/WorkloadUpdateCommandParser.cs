// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUpdateCommandParser
    {
        public static readonly Argument PackageIdArgument = WorkloadInstallCommandParser.WorkloadIdArgument;

        public static readonly Option ConfigOption = WorkloadInstallCommandParser.ConfigOption;

        public static readonly Option AddSourceOption = WorkloadInstallCommandParser.AddSourceOption;

        public static readonly Option VersionOption = WorkloadInstallCommandParser.VersionOption;

        public static readonly Option VerbosityOption = WorkloadInstallCommandParser.VerbosityOption;

        public static Command GetCommand()
        {
            Command command = new("update", LocalizableStrings.CommandDescription);

            command.AddArgument(PackageIdArgument);
            command.AddOption(ConfigOption);
            command.AddOption(AddSourceOption);
            command.AddOption(VersionOption);
            command.AddOption(WorkloadCommandRestorePassThroughOptions.DisableParallelOption);
            command.AddOption(WorkloadCommandRestorePassThroughOptions.IgnoreFailedSourcesOption);
            command.AddOption(WorkloadCommandRestorePassThroughOptions.NoCacheOption);
            command.AddOption(WorkloadCommandRestorePassThroughOptions.InteractiveRestoreOption);
            command.AddOption(VerbosityOption);

            return command;
        }
    }
}
