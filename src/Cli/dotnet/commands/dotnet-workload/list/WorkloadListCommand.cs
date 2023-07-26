// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using InformationStrings = Microsoft.DotNet.Workloads.Workload.LocalizableStrings;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal class WorkloadListCommand : WorkloadCommandBase
    {
        private readonly bool _includePreviews;
        private readonly bool _machineReadableOption;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly IWorkloadInfoHelper _workloadListHelper;

        public WorkloadListCommand(
            ParseResult result,
            IReporter reporter = null,
            IWorkloadInstallationRecordRepository workloadRecordRepo = null,
            string currentSdkVersion = null,
            string dotnetDir = null,
            string userProfileDir = null,
            string tempDirPath = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null,
            IWorkloadResolver workloadResolver = null
        ) : base(result, CommonOptions.HiddenVerbosityOption, reporter, tempDirPath, nugetPackageDownloader)
        {
            _workloadListHelper = new WorkloadInfoHelper(
                Verbosity,
                result?.GetValueForOption(WorkloadListCommandParser.VersionOption) ?? null,
                VerifySignatures,
                Reporter,
                workloadRecordRepo,
                currentSdkVersion,
                dotnetDir,
                userProfileDir,
                workloadResolver
            );

            _machineReadableOption = result.GetValueForOption(WorkloadListCommandParser.MachineReadableOption);

            _includePreviews = result.GetValueForOption(WorkloadListCommandParser.IncludePreviewsOption);
            string userProfileDir1 = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;

            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(Reporter,
                _workloadListHelper.WorkloadResolver, PackageDownloader, userProfileDir1, TempDirectoryPath, _workloadListHelper.WorkloadRecordRepo, _workloadListHelper.Installer);
        }

        public override int Execute()
        {
            IEnumerable<WorkloadId> installedList = _workloadListHelper.InstalledSdkWorkloadIds;

            if (_machineReadableOption)
            {
                _workloadListHelper.CheckTargetSdkVersionIsValid();

                UpdateAvailableEntry[] updateAvailable = GetUpdateAvailable(installedList);
                ListOutput listOutput = new(installedList.Select(id => id.ToString()).ToArray(),
                    updateAvailable);

                Reporter.WriteLine("==workloadListJsonOutputStart==");
                Reporter.WriteLine(
                    JsonSerializer.Serialize(listOutput,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                Reporter.WriteLine("==workloadListJsonOutputEnd==");
            }
            else
            {
                var manifestInfoDict = _workloadListHelper.WorkloadResolver.GetInstalledManifests().ToDictionary(info => info.Id, StringComparer.OrdinalIgnoreCase);

                InstalledWorkloadsCollection installedWorkloads = _workloadListHelper.AddInstalledVsWorkloads(installedList);
                Reporter.WriteLine();
                PrintableTable<KeyValuePair<string, string>> table = new();
                table.AddColumn(InformationStrings.WorkloadIdColumn, workload => workload.Key);
                table.AddColumn(InformationStrings.WorkloadManfiestVersionColumn, workload =>
                {
                    var m = _workloadListHelper.WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                    var manifestInfo = manifestInfoDict[m.Id];
                    return m.Version + "/" + manifestInfo.ManifestFeatureBand;
                });
                table.AddColumn(InformationStrings.WorkloadSourceColumn, workload => workload.Value);

                table.PrintRows(installedWorkloads.AsEnumerable(), l => Reporter.WriteLine(l));

                Reporter.WriteLine();
                Reporter.WriteLine(LocalizableStrings.WorkloadListFooter);
                Reporter.WriteLine();

                var updatableWorkloads = _workloadManifestUpdater.GetUpdatableWorkloadsToAdvertise(installedList).Select(workloadId => workloadId.ToString());
                if (updatableWorkloads.Any())
                {
                    Reporter.WriteLine(string.Format(LocalizableStrings.WorkloadUpdatesAvailable, string.Join(" ", updatableWorkloads)));
                    Reporter.WriteLine();
                }
            }

            return 0;
        }

        internal UpdateAvailableEntry[] GetUpdateAvailable(IEnumerable<WorkloadId> installedList)
        {
            HashSet<WorkloadId> installedWorkloads = installedList.ToHashSet();
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews).Wait();
            var manifestsToUpdate =
                _workloadManifestUpdater.CalculateManifestUpdates();

            List<UpdateAvailableEntry> updateList = new();
            foreach ((ManifestVersionUpdate manifestUpdate, Dictionary<WorkloadId, WorkloadDefinition> workloads) in manifestsToUpdate)
            {
                foreach ((WorkloadId WorkloadId, WorkloadDefinition workloadDefinition) in
                    workloads)
                {
                    if (installedWorkloads.Contains(new WorkloadId(WorkloadId.ToString())))
                    {
                        updateList.Add(new UpdateAvailableEntry(manifestUpdate.ExistingVersion.ToString(),
                            manifestUpdate.NewVersion.ToString(),
                            workloadDefinition.Description, WorkloadId.ToString()));
                    }
                }
            }

            return updateList.ToArray();
        }

        internal record ListOutput(string[] Installed, UpdateAvailableEntry[] UpdateAvailable);

        internal record UpdateAvailableEntry(string ExistingManifestVersion, string AvailableUpdateManifestVersion,
            string Description, string WorkloadId);
    }
}
