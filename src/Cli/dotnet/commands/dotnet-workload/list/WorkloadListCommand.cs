// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal class WorkloadListCommand : WorkloadCommandBase
    {
        private readonly SdkFeatureBand _currentSdkFeatureBand;
        private readonly string _dotnetPath;
        private readonly bool _includePreviews;
        private readonly bool _machineReadableOption;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IReporter _reporter;
        private readonly string _targetSdkVersion;
        private readonly string _tempDirPath;
        private readonly string _userProfileDir;
        private readonly VerbosityOptions _verbosity;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly IWorkloadInstallationRecordRepository _workloadRecordRepo;
        private readonly IWorkloadResolver _workloadResolver;

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
            _machineReadableOption = result.GetValueForOption(WorkloadListCommandParser.MachineReadableOption);
            _verbosity = result.GetValueForOption(WorkloadListCommandParser.VerbosityOption);

            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            ReleaseVersion currentSdkReleaseVersion = new(currentSdkVersion ?? Product.Version);
            _currentSdkFeatureBand = new SdkFeatureBand(currentSdkReleaseVersion);
            
            _includePreviews = result.GetValueForOption(WorkloadListCommandParser.IncludePreviewsOption);
            _tempDirPath = tempDirPath ??
                           (string.IsNullOrWhiteSpace(
                               result.GetValueForOption(WorkloadListCommandParser.TempDirOption))
                               ? Path.GetTempPath()
                               : result.GetValueForOption(WorkloadListCommandParser.TempDirOption));
            _targetSdkVersion = result.GetValueForOption(WorkloadListCommandParser.VersionOption);
            _userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            var workloadManifestProvider =
                new SdkDirectoryWorkloadManifestProvider(_dotnetPath,
                    string.IsNullOrWhiteSpace(_targetSdkVersion)
                        ? currentSdkReleaseVersion.ToString()
                        : _targetSdkVersion,
                    _userProfileDir);
            DirectoryPath tempPackagesDir =
                new(Path.Combine(_userProfileDir, "sdk-advertising-temp"));
            NullLogger nullLogger = new NullLogger();
            _nugetPackageDownloader = nugetPackageDownloader ??
                                      new NuGetPackageDownloader(tempPackagesDir, null,
                                          new FirstPartyNuGetPackageSigningVerifier(tempPackagesDir, nullLogger),
                                          verboseLogger: nullLogger,
                                          restoreActionConfig: _parseResult.ToRestoreActionConfig());
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(workloadManifestProvider, _dotnetPath, currentSdkReleaseVersion.ToString(), _userProfileDir);

            _workloadRecordRepo = workloadRecordRepo ??
                WorkloadInstallerFactory.GetWorkloadInstaller(reporter, _currentSdkFeatureBand, _workloadResolver, _verbosity, _userProfileDir,
                VerifySignatures,
                elevationRequired: false).GetWorkloadInstallationRecordRepository();

            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter,
                _workloadResolver, _nugetPackageDownloader, _userProfileDir, _tempDirPath, _workloadRecordRepo);
        }

        public override int Execute()
        {
            IEnumerable<WorkloadId> installedList = _workloadRecordRepo.GetInstalledWorkloads(_currentSdkFeatureBand);
            if (_machineReadableOption)
            {
                if (!string.IsNullOrWhiteSpace(_targetSdkVersion))
                {
                    if (new SdkFeatureBand(_targetSdkVersion).CompareTo(_currentSdkFeatureBand) < 0)
                    {
                        throw new ArgumentException(
                            $"Version band of {_targetSdkVersion} --- {new SdkFeatureBand(_targetSdkVersion)} should not be smaller than current version band {_currentSdkFeatureBand}");
                    }
                }

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
                InstalledWorkloadsCollection installedWorkloads = new(installedList, $"SDK {_currentSdkFeatureBand}");

                if (OperatingSystem.IsWindows())
                {
                    VisualStudioWorkloads.GetInstalledWorkloads(_workloadResolver, _currentSdkFeatureBand, installedWorkloads);
                }
                
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
