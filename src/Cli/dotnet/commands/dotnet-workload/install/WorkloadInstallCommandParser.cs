// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Workloads.Workload.Install;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadInstallCommandParser
    {
        public static readonly Argument<IEnumerable<string>> WorkloadIdArgument =
            new Argument<IEnumerable<string>>(LocalizableStrings.WorkloadIdArgumentName)
            {
                Arity = ArgumentArity.OneOrMore,
                Description = LocalizableStrings.WorkloadIdArgumentDescription
            };

        public static readonly Option<string> ConfigOption =
            new Option<string>("--configfile", LocalizableStrings.ConfigFileOptionDescription)
            {
                ArgumentHelpName = LocalizableStrings.ConfigFileOptionName
            };

        public static readonly Option<string[]> SourceOption =
            new Option<string[]>(new string[] { "-s", "--source" }, LocalizableStrings.SourceOptionDescription)
            {
                ArgumentHelpName = LocalizableStrings.SourceOptionName
            }.AllowSingleArgPerToken();

        public static readonly Option<bool> PrintDownloadLinkOnlyOption =
            new Option<bool>("--print-download-link-only", LocalizableStrings.PrintDownloadLinkOnlyDescription)
            {
                IsHidden = true
            };

        public static readonly Option<bool> SkipSignCheckOption =
            new Option<bool>("--skip-sign-check", LocalizableStrings.SkipSignCheckOptionDescription)
            {
                IsHidden = true
            };

        public static readonly Option<string> VersionOption =
            new Option<string>("--sdk-version", LocalizableStrings.VersionOptionDescription)
            {
                ArgumentHelpName = LocalizableStrings.VersionOptionName,
                IsHidden = true
            };

        public static readonly Option<bool> IncludePreviewOption =
            new Option<bool>("--include-previews", LocalizableStrings.IncludePreviewOptionDescription);

        public static readonly Option<string> FromCacheOption = new Option<string>("--from-cache", LocalizableStrings.FromCacheOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.FromCacheOptionArgumentName,
            IsHidden = true
        };

        public static readonly Option<string> DownloadToCacheOption = new Option<string>("--download-to-cache", LocalizableStrings.DownloadToCacheOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.DownloadToCacheOptionArgumentName,
            IsHidden = true
        };

        public static readonly Option<bool> SkipManifestUpdateOption = new Option<bool>("--skip-manifest-update", LocalizableStrings.SkipManifestUpdateOptionDescription);

        public static readonly Option<string> TempDirOption = new Option<string>("--temp-dir", LocalizableStrings.TempDirOptionDescription);

        public static readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption;

        public static readonly Option<string> FromRollbackFileOption = new Option<string>("--from-rollback-file", Microsoft.DotNet.Workloads.Workload.Update.LocalizableStrings.FromRollbackDefinitionOptionDescription)
        {
            IsHidden = true
        };

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("install", LocalizableStrings.CommandDescription);

            command.AddArgument(WorkloadIdArgument);
            AddWorkloadInstallCommandOptions(command);

            command.SetHandler((parseResult) => new WorkloadInstallCommand(parseResult).Execute());

            return command;
        }

        internal static void AddWorkloadInstallCommandOptions(Command command)
        {
            command.AddOption(VersionOption);
            command.AddOption(ConfigOption);
            command.AddOption(SourceOption);
            command.AddOption(SkipManifestUpdateOption);
            command.AddOption(PrintDownloadLinkOnlyOption);
            command.AddOption(FromCacheOption);
            command.AddOption(DownloadToCacheOption);
            command.AddOption(IncludePreviewOption);
            command.AddOption(TempDirOption);
            command.AddWorkloadCommandNuGetRestoreActionConfigOptions();
            command.AddOption(VerbosityOption);
            command.AddOption(FromRollbackFileOption);
            command.AddOption(SkipSignCheckOption);
        }
    }
}
