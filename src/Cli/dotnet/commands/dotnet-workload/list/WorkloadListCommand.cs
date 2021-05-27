// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Text.Json;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Product = Microsoft.DotNet.Cli.Utils.Product;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Linq;

namespace Microsoft.DotNet.Workloads.Workload.List
{
    internal class WorkloadListCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly VerbosityOptions _verbosity;
        private readonly bool _machineReadableOption;
        private readonly IWorkloadInstallationRecordRepository _workloadRecordRepo;
        private readonly SdkFeatureBand _sdkFeatureBand;

        public static readonly string MockUpdateDirectory = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),
            "DEV_mockworkloads", "update");

        public WorkloadListCommand(
            ParseResult result,
            IReporter reporter = null,
            IWorkloadInstallationRecordRepository workloadRecordRepo = null,
            string version = null) : base(result)
        {
            _reporter = reporter ?? Reporter.Output;
            _machineReadableOption = result.ValueForOption<bool>(WorkloadListCommandParser.MachineReadableOption);
            _verbosity = result.ValueForOption<VerbosityOptions>(WorkloadListCommandParser.VerbosityOption);

            var sdkVersion = new ReleaseVersion(version ?? Product.Version);
            var dotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, sdkVersion.ToString());
            var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetPath, sdkVersion.ToString());
            _sdkFeatureBand = new SdkFeatureBand(sdkVersion);
            _workloadRecordRepo = workloadRecordRepo ??
                                  WorkloadInstallerFactory
                                      .GetWorkloadInstaller(_reporter, _sdkFeatureBand, workloadResolver, _verbosity)
                                      .GetWorkloadInstallationRecordRepository();
            _sdkVersion = result.ValueForOption<string>(WorkloadListCommandParser.VersionOption);
        }

        public override int Execute()
        {
            var installedList = _workloadRecordRepo.GetInstalledWorkloads(_sdkFeatureBand);
            if (_machineReadableOption)
            {
                var updateAvailable = MockUpdateAvailable();
                var listOutput = new ListOutput(Installed: installedList.Select(id => id.ToString()).ToArray(),
                    UpdateAvailable: updateAvailable);

                _reporter.WriteLine("==workloadListJsonOutputStart==");
                _reporter.WriteLine(
                    JsonSerializer.Serialize(listOutput,
                        options: new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase}));
                _reporter.WriteLine("==workloadListJsonOutputEnd==");
            }
            else
            {
                var table = new PrintableTable<WorkloadId>();
                table.AddColumn(LocalizableStrings.WorkloadIdColumn, workloadId => workloadId.ToString());

                table.PrintRows(installedList, l => _reporter.WriteLine(l));

                _reporter.WriteLine();
            }

            return 0;
        }

        private UpdateAvailableEntry[] MockUpdateAvailable()
        {
            var updateList = new List<UpdateAvailableEntry>();

            if (!File.Exists(Path.Combine(MockUpdateDirectory,
                "Microsoft.NET.Workload.Android.6.0.100.nupkg")))
            {
                updateList.Add(new UpdateAvailableEntry("6.0.100", "6.0.101",
                    _mockAndroidDescription,
                    "microsoft-android-sdk-full"));
            }

            if (!File.Exists(Path.Combine(MockUpdateDirectory,
                "Microsoft.iOS.Bundle.6.0.100.nupkg")))
            {
                updateList.Add(new UpdateAvailableEntry("6.0.100", "6.0.101",
                    _mockIosDescription,
                    "microsoft-ios-sdk-full"));
            }

            return updateList.ToArray();
        }

        internal record ListOutput(string[] Installed, UpdateAvailableEntry[] UpdateAvailable);

        internal record UpdateAvailableEntry(string ExistingManifestVersion, string AvailableUpdateManifestVersion, string Description, string WorkloadId);

        private readonly string _mockIosDescription =
            $"ios-workload-description: for testing you can delete the content of {MockUpdateDirectory} to revert the mock update";

        private readonly string _mockAndroidDescription =
            $"android-workload-description: for testing you can delete the content of {MockUpdateDirectory} to revert the mock update";

        private readonly string _sdkVersion;
    }
}
