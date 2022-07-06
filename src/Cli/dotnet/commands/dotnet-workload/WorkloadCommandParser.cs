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
using Microsoft.DotNet.Workloads.Workload.Install;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-workload";

        private static readonly Command Command = ConstructCommand();

        public static readonly Option<bool> InfoOption = new Option<bool>("--info");

        public static Command GetCommand()
        {
            Command.AddOption(InfoOption);
            return Command;
        }

        private static void ShowWorkloadsInfo()
        {
            IWorkloadInfoHelper workloadListHelper = new WorkloadInfoHelper();
            IEnumerable<WorkloadId> installedList = workloadListHelper.InstalledSdkWorkloadIds;
            InstalledWorkloadsCollection installedWorkloads = workloadListHelper.AddInstalledVsWorkloads(installedList);

            foreach (var workload in installedWorkloads.AsEnumerable())
            {
                Reporter.Output.WriteLine("\n");
                Reporter.Output.WriteLine(LocalizableStrings.WorkloadIdColumn + " : [" + workload.Key + "]");

                Reporter.Output.Write(LocalizableStrings.WorkloadSourceColumn + ":");
                Reporter.Output.WriteLine("\t" + workload.Value);

                var workloadManifest = workloadListHelper.WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                var workloadFeatureBand = new WorkloadManifestInfo(
                    workloadManifest.Id,
                    workloadManifest.Version,
                    Path.GetDirectoryName(workloadManifest.ManifestPath)!).ManifestFeatureBand;

                Reporter.Output.Write(LocalizableStrings.WorkloadManfiestVersionColumn + ":");
                Reporter.Output.WriteLine("\t" + workloadManifest.Version + "/" + workloadFeatureBand);

                Reporter.Output.Write(LocalizableStrings.WorkloadManifestPathColumn + ":");
                Reporter.Output.WriteLine("\t\t" + workloadManifest.ManifestPath);

                Reporter.Output.Write(LocalizableStrings.WorkloadInstallTypeColumn + ":");
                Reporter.Output.WriteLine("\t\t" + WorkloadInstallerFactory.GetWorkloadInstallType(
                    new SdkFeatureBand(workloadFeatureBand), workloadManifest.ManifestPath).ToString()
                );
            }
        }

        private static int ProcessArgs(ParseResult parseResult)
        {
            if (parseResult.HasOption(InfoOption) && parseResult.RootSubCommandResult() == "workload")
            {
                ShowWorkloadsInfo();
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
