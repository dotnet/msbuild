// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload.Update;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadUpdateCommandParser
    {
        public static readonly Option<string> ConfigOption = WorkloadInstallCommandParser.ConfigOption;

        public static readonly Option<string[]> SourceOption = WorkloadInstallCommandParser.SourceOption;

        public static readonly Option<string> VersionOption = WorkloadInstallCommandParser.VersionOption;

        public static readonly Option<bool> IncludePreviewsOption = WorkloadInstallCommandParser.IncludePreviewOption;

        public static readonly Option<string> DownloadToCacheOption = WorkloadInstallCommandParser.DownloadToCacheOption;

        public static readonly Option<string> TempDirOption = WorkloadInstallCommandParser.TempDirOption;

        public static readonly Option<bool> PrintDownloadLinkOnlyOption =
            WorkloadInstallCommandParser.PrintDownloadLinkOnlyOption;

        public static readonly Option<string> FromCacheOption =
            WorkloadInstallCommandParser.FromCacheOption;

        public static readonly Option<bool> FromPreviousSdkOption = new Option<bool>("--from-previous-sdk", LocalizableStrings.FromPreviousSdkOptionDescription);

        public static readonly Option<bool> AdManifestOnlyOption = new Option<bool>("--advertising-manifests-only", LocalizableStrings.AdManifestOnlyOptionDescription);

        public static readonly Option<bool> PrintRollbackOption = new Option<bool>("--print-rollback")
        {
            IsHidden = true
        };

        public static readonly Option<string> FromRollbackFileOption = WorkloadInstallCommandParser.FromRollbackFileOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("update", LocalizableStrings.CommandDescription);

            command.AddOption(ConfigOption);
            command.AddOption(SourceOption);
            command.AddOption(VersionOption);
            command.AddOption(PrintDownloadLinkOnlyOption);
            command.AddOption(FromCacheOption);
            command.AddOption(IncludePreviewsOption);
            command.AddOption(DownloadToCacheOption);
            command.AddOption(TempDirOption);
            command.AddOption(FromPreviousSdkOption);
            command.AddOption(AdManifestOnlyOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddOption(PrintRollbackOption);
            command.AddOption(FromRollbackFileOption);

            command.SetHandler((parseResult) => new WorkloadUpdateCommand(parseResult).Execute());

            return command;
        }
    }
}
