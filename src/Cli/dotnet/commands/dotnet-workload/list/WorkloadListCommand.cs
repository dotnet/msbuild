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
                WorkloadInstallerFactory.GetWorkloadInstaller(_reporter, _sdkFeatureBand, workloadResolver, _verbosity).GetWorkloadInstallationRecordRepository();
        }

        public override int Execute()
        {
            var installedList = _workloadRecordRepo.GetInstalledWorkloads(_sdkFeatureBand);
            if (_machineReadableOption)
            {
                var outputJson = new Dictionary<string, string[]>()
                {
                    ["installed"] = installedList.Select(id => id.ToString()).ToArray()
                };

                _reporter.WriteLine("==workloadListJsonOutputStart==");
                _reporter.WriteLine(
                    JsonSerializer.Serialize(outputJson));
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
    }
}
