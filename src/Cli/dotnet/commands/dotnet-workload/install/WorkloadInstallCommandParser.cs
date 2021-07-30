// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.Install.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadInstallCommandParser
    {
        public static readonly Argument<IEnumerable<string>> WorkloadIdArgument =
            new Argument<IEnumerable<string>>(LocalizableStrings.WorkloadIdArgumentName)
            {
                Arity = ArgumentArity.OneOrMore, Description = LocalizableStrings.WorkloadIdArgumentDescription
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

        public static readonly Option<string> VersionOption =
            new Option<string>("--sdk-version", LocalizableStrings.VersionOptionDescription)
            {
                ArgumentHelpName = LocalizableStrings.VersionOptionName
            };

        public static readonly Option<bool> IncludePreviewOption =
            new Option<bool>("--include-previews", LocalizableStrings.IncludePreviewOptionDescription);

        public static readonly Option<string> FromCacheOption = new Option<string>("--from-cache", LocalizableStrings.FromCacheOptionDescription);

        public static readonly Option<string> DownloadToCacheOption = new Option<string>("--download-to-cache", LocalizableStrings.DownloadToCacheOptionDescription);

        public static readonly Option<bool> SkipManifestUpdateOption = new Option<bool>("--skip-manifest-update", LocalizableStrings.SkipManifestUpdateOptionDescription);

        public static readonly Option<string> TempDirOption = new Option<string>("--temp-dir", LocalizableStrings.TempDirOptionDescription);

        public static readonly Option<VerbosityOptions> VerbosityOption = CommonOptions.VerbosityOption();

        public static Command GetCommand()
        {
            var command = new Command("install", LocalizableStrings.CommandDescription);

            command.AddArgument(WorkloadIdArgument);
            AddWorkloadInstallCommandOptions(command);

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
        }
    }
}
