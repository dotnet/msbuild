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
    internal class WorkloadListCommand : CommandBase
    {
        private readonly SdkFeatureBand _currentSdkFeatureBand;
        private readonly string _dotnetPath;
        private readonly bool _includePreviews;
        private readonly bool _machineReadableOption;
        private readonly INuGetPackageDownloader _nugetPackageDownloader;
        private readonly IReporter _reporter;
        private readonly string _targetSdkVersion;
        private readonly string _tempDirPath;
        private readonly string _userHome;
        private readonly VerbosityOptions _verbosity;
        private readonly SdkDirectoryWorkloadManifestProvider _workloadManifestProvider;
        private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
        private readonly IWorkloadInstallationRecordRepository _workloadRecordRepo;

        public WorkloadListCommand(
            ParseResult result,
            IReporter reporter = null,
            IWorkloadInstallationRecordRepository workloadRecordRepo = null,
            string currentSdkVersion = null,
            string dotnetDir = null,
            string userHome = null,
            string tempDirPath = null,
            INuGetPackageDownloader nugetPackageDownloader = null,
            IWorkloadManifestUpdater workloadManifestUpdater = null
        ) : base(result)
        {
            _reporter = reporter ?? Reporter.Output;
            _machineReadableOption = result.ValueForOption<bool>(WorkloadListCommandParser.MachineReadableOption);
            _verbosity = result.ValueForOption<VerbosityOptions>(WorkloadListCommandParser.VerbosityOption);

            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            ReleaseVersion currentSdkReleaseVersion = new(currentSdkVersion ?? Product.Version);
            _currentSdkFeatureBand = new SdkFeatureBand(currentSdkReleaseVersion);
            _workloadRecordRepo = workloadRecordRepo ??
                                  new NetSdkManagedInstallationRecordRepository(_dotnetPath);
            _includePreviews = result.ValueForOption<bool>(WorkloadListCommandParser.IncludePreviewsOption);
            _tempDirPath = tempDirPath ??
                           (string.IsNullOrWhiteSpace(
                               result.ValueForOption<string>(WorkloadListCommandParser.TempDirOption))
                               ? Path.GetTempPath()
                               : result.ValueForOption<string>(WorkloadListCommandParser.TempDirOption));
            _targetSdkVersion = result.ValueForOption<string>(WorkloadListCommandParser.VersionOption);
            _workloadManifestProvider =
                new SdkDirectoryWorkloadManifestProvider(_dotnetPath,
                    string.IsNullOrWhiteSpace(_targetSdkVersion)
                        ? currentSdkReleaseVersion.ToString()
                        : _targetSdkVersion);
            _userHome = userHome ?? CliFolderPathCalculator.DotnetHomePath;
            DirectoryPath tempPackagesDir =
                new(Path.Combine(_userHome, ".dotnet", "sdk-advertising-temp"));
            NullLogger nullLogger = new NullLogger();
            _nugetPackageDownloader = nugetPackageDownloader ??
                                      new NuGetPackageDownloader(tempPackagesDir, null,
                                          new FirstPartyNuGetPackageSigningVerifier(tempPackagesDir, nullLogger),
                                          verboseLogger: nullLogger,
                                          restoreActionConfig: _parseResult.ToRestoreActionConfig());
            var workloadResolver = WorkloadResolver.Create(_workloadManifestProvider, _dotnetPath, currentSdkReleaseVersion.ToString());
            _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(_reporter,
                _workloadManifestProvider, workloadResolver, _nugetPackageDownloader, _userHome, _tempDirPath);
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
                PrintableTable<WorkloadId> table = new();
                table.AddColumn(LocalizableStrings.WorkloadIdColumn, workloadId => workloadId.ToString());

                table.PrintRows(installedList, l => _reporter.WriteLine(l));

                _reporter.WriteLine();
            }

            return 0;
        }

        internal UpdateAvailableEntry[] GetUpdateAvailable(IEnumerable<WorkloadId> installedList)
        {
            HashSet<WorkloadId> installedWorkloads = installedList.ToHashSet();
            _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews).Wait();
            IEnumerable<(ManifestId manifestId, ManifestVersion existingVersion, ManifestVersion newVersion,
                Dictionary<WorkloadId, WorkloadDefinition> Workloads)> manifestsToUpdate =
                _workloadManifestUpdater.CalculateManifestUpdates();

            List<UpdateAvailableEntry> updateList = new();
            foreach ((ManifestId _, ManifestVersion existingVersion, ManifestVersion newVersion,
                Dictionary<WorkloadId, WorkloadDefinition> workloads) in manifestsToUpdate)
            {
                foreach ((WorkloadId WorkloadId, WorkloadDefinition workloadDefinition) in
                    workloads)
                {
                    if (installedWorkloads.Contains(new WorkloadId(WorkloadId.ToString())))
                    {
                        updateList.Add(new UpdateAvailableEntry(existingVersion.ToString(),
                            newVersion.ToString(),
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
