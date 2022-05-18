// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal class WorkloadListCommand : WorkloadCommandBase
    {
        private readonly bool _includePreviews;
        private readonly bool _machineReadableOption;
        private readonly IReporter _reporter;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly IWorkloadListHelper _workloadListHelper;

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
        ) : base(result)
        {
            _reporter = reporter ?? Reporter.Output;

            _workloadListHelper = new WorkloadListHelper(
                result,
                VerifySignatures,
                _reporter,
                workloadRecordRepo,
                currentSdkVersion,
                dotnetDir,
                userProfileDir,
                workloadResolver
            );

            _machineReadableOption = result.GetValueForOption(WorkloadListCommandParser.MachineReadableOption);

            _includePreviews = result.GetValueForOption(WorkloadListCommandParser.IncludePreviewsOption);
            tempDirPath ??=
                (string.IsNullOrWhiteSpace(
                    result.GetValueForOption(WorkloadListCommandParser.TempDirOption))
                    ? Path.GetTempPath()
                    : result.GetValueForOption(WorkloadListCommandParser.TempDirOption));
            string userProfileDir1 = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            DirectoryPath tempPackagesDir =
                new(Path.Combine(userProfileDir1, "sdk-advertising-temp"));
            NullLogger nullLogger = new NullLogger();
            nugetPackageDownloader ??= new NuGetPackageDownloader(tempPackagesDir, null,
                new FirstPartyNuGetPackageSigningVerifier(tempPackagesDir, nullLogger),
                verboseLogger: nullLogger,
                restoreActionConfig: _parseResult.ToRestoreActionConfig());

            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter,
                _workloadListHelper.WorkloadResolver, nugetPackageDownloader, userProfileDir1, tempDirPath, _workloadListHelper.WorkloadRecordRepo);
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

                _reporter.WriteLine("==workloadListJsonOutputStart==");
                _reporter.WriteLine(
                    JsonSerializer.Serialize(listOutput,
                        new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase}));
                _reporter.WriteLine("==workloadListJsonOutputEnd==");
            }
            else
            {
                InstalledWorkloadsCollection installedWorkloads = _workloadListHelper.AddInstalledVsWorkloads(installedList);


                _reporter.WriteLine();

                PrintableTable<KeyValuePair<string, string>> table = new();
                table.AddColumn(LocalizableStrings.WorkloadIdColumn, workload => workload.Key);
                table.AddColumn(LocalizableStrings.WorkloadSourceColumn, workload => workload.Value);

                table.PrintRows(installedWorkloads.AsEnumerable(), l => _reporter.WriteLine(l));

                _reporter.WriteLine();
                _reporter.WriteLine(LocalizableStrings.WorkloadListFooter);
                _reporter.WriteLine();

                var updatableWorkloads = _workloadManifestUpdater.GetUpdatableWorkloadsToAdvertise(installedList).Select(workloadId => workloadId.ToString());
                if (updatableWorkloads.Any())
                {
                    _reporter.WriteLine(string.Format(LocalizableStrings.WorkloadUpdatesAvailable, string.Join(" ", updatableWorkloads)));
                    _reporter.WriteLine();
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
                        //  TODO: Potentially show existing and new feature bands
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
