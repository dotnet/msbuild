// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Cli.Commands;
using CommonStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;
using IReporter = Microsoft.DotNet.Cli.Utils.IReporter;

namespace Microsoft.DotNet.Cli
{
    internal static class WorkloadCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-workload";

        private static readonly Command Command = ConstructCommand();

        public static readonly Option<bool> InfoOption = new Option<bool>("--info", CommonStrings.WorkloadInfoDescription);

        public static Command GetCommand()
        {
            Command.AddOption(InfoOption);
            return Command;
        }

        internal static void ShowWorkloadsInfo(ParseResult parseResult = null, IWorkloadInfoHelper workloadInfoHelper = null, IReporter reporter = null, string dotnetDir = null)
        {
            if(workloadInfoHelper != null)
            {
                workloadInfoHelper ??= new WorkloadInfoHelper(parseResult != null ? parseResult.HasOption(SharedOptions.InteractiveOption) : false);
            }
            else
            {
                workloadInfoHelper ??= new WorkloadInfoHelper(false);
            }
            IEnumerable<WorkloadId> installedList = workloadInfoHelper.InstalledSdkWorkloadIds;
            InstalledWorkloadsCollection installedWorkloads = workloadInfoHelper.AddInstalledVsWorkloads(installedList);
            reporter ??= Cli.Utils.Reporter.Output;
            string dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);

            if (!installedList.Any())
            {
                reporter.WriteLine(CommonStrings.NoWorkloadsInstalledInfoWarning);
                return;
            }


            foreach (var workload in installedWorkloads.AsEnumerable())
            {
                var workloadManifest = workloadInfoHelper.WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                var workloadFeatureBand = new WorkloadManifestInfo(
                    workloadManifest.Id,
                    workloadManifest.Version,
                    Path.GetDirectoryName(workloadManifest.ManifestPath)!).ManifestFeatureBand;

                const int align = 10;
                const string separator = "   ";

                reporter.WriteLine($" {'[' + workload.Key + ']'}");

                reporter.Write($"{separator}{CommonStrings.WorkloadSourceColumn}:");
                reporter.WriteLine($" {workload.Value,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadManfiestVersionColumn}:");
                reporter.WriteLine($"    {workloadManifest.Version + '/' + workloadFeatureBand,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadManifestPathColumn}:");
                reporter.WriteLine($"       {workloadManifest.ManifestPath,align}");

                reporter.Write($"{separator}{CommonStrings.WorkloadInstallTypeColumn}:");
                reporter.WriteLine($"       {WorkloadInstallerFactory.GetWorkloadInstallType(new SdkFeatureBand(workloadFeatureBand), dotnetPath),align}"
                );
            }
        }

        private static int ProcessArgs(ParseResult parseResult)
        {
            if (parseResult.HasOption(InfoOption) && parseResult.RootSubCommandResult() == "workload")
            {
                ShowWorkloadsInfo(parseResult);
                Cli.Utils.Reporter.Output.WriteLine("");
                return 0;
            }
            return parseResult.HandleMissingCommand();
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("workload", DocsLink, CommonStrings.CommandDescription);

            command.AddCommand(WorkloadInstallCommandParser.GetCommand());
            command.AddCommand(WorkloadUpdateCommandParser.GetCommand());
            command.AddCommand(WorkloadListCommandParser.GetCommand());
            command.AddCommand(WorkloadSearchCommandParser.GetCommand());
            command.AddCommand(WorkloadUninstallCommandParser.GetCommand());
            command.AddCommand(WorkloadRepairCommandParser.GetCommand());
            command.AddCommand(WorkloadRestoreCommandParser.GetCommand());
            command.AddCommand(WorkloadCleanCommandParser.GetCommand());
            command.AddCommand(WorkloadElevateCommandParser.GetCommand());

            command.SetHandler((parseResult) => ProcessArgs(parseResult));

            return command;
        }
    }
}
