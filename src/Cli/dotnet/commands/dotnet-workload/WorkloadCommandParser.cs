// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Reporter = Microsoft.DotNet.Cli.Utils.Reporter;
using Product = Microsoft.DotNet.Cli.Utils.Product;
using LocalizableStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;
using System.Collections.Generic;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.IO;
using Microsoft.DotNet.Configurer;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Workloads.Workload.List;

namespace Microsoft.DotNet.Cli
{
    internal class WorkloadCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-workload";

        private static readonly Command Command = ConstructCommand();

        public static readonly Option<bool> InfoOption = new Option<bool>("--info");

        public static Command GetCommand()
        {
            Command.AddOption(InfoOption);
            return Command;
        }

        private static IWorkloadResolver GetStandardWorkloadResolver()
        {
            string dotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
            ReleaseVersion sdkReleaseVersion = new(Product.Version);
            SdkFeatureBand band = new(sdkReleaseVersion);
            string userProfileDir = CliFolderPathCalculator.DotnetUserProfileFolderPath;

            SdkDirectoryWorkloadManifestProvider workloadManifestProvider =
                new(dotnetPath, sdkReleaseVersion.ToString(), userProfileDir);

            IWorkloadResolver workloadResolver = NET.Sdk.WorkloadManifestReader.WorkloadResolver.Create(
                workloadManifestProvider, dotnetPath,
                sdkReleaseVersion.ToString(), userProfileDir);

            return workloadResolver;
        }

        private static int ProcessArgs(ParseResult parseResult)
        {
            if (parseResult.HasOption(InfoOption) && parseResult.RootSubCommandResult() == "workload")
            {
                IWorkloadListHelper workloadListHelper = new WorkloadListHelper();
                IEnumerable<WorkloadId> installedList = workloadListHelper.InstalledSdkWorkloadIds;
                InstalledWorkloadsCollection installedWorkloads = workloadListHelper.AddInstalledVsWorkloads(installedList);

                /*PrintableTable<KeyValuePair<string, string>> table = new();
                table.AddColumn(LocalizableStrings.WorkloadIdColumn, workload => workload.Key);
                table.AddColumn(LocalizableStrings.WorkloadManfiestVersionColumn, workload =>
                {
                    var m = workloadListHelper.WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                    return m.Version + "/" +
                    new WorkloadManifestInfo(m.Id, m.Version, Path.GetDirectoryName(m.ManifestPath)!).ManifestFeatureBand;
                });
                table.AddColumn(LocalizableStrings.WorkloadSourceColumn, workload => workload.Value);
                table.AddColumn("Install Type", workload => workload);
                table.AddColumn("Workload Path", workload => workload);

                table.PrintRows(installedWorkloads.AsEnumerable(), l => Reporter.Output.WriteLine(l));
                */
                Reporter.Output.WriteLine("Test");
                return 0;
            }
            return parseResult.HandleMissingCommand();
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("workload", DocsLink, LocalizableStrings.CommandDescription);

            command.AddCommand(WorkloadInstallCommandParser.GetCommand());
            command.AddCommand(WorkloadUpdateCommandParser.GetCommand());
            command.AddCommand(WorkloadListCommandParser.GetCommand());
            command.AddCommand(WorkloadSearchCommandParser.GetCommand());
            command.AddCommand(WorkloadUninstallCommandParser.GetCommand());
            command.AddCommand(WorkloadRepairCommandParser.GetCommand());
            command.AddCommand(WorkloadRestoreCommandParser.GetCommand());
            command.AddCommand(WorkloadElevateCommandParser.GetCommand());

            command.SetHandler((parseResult) => ProcessArgs(parseResult));

            return command;
        }
    }
}
