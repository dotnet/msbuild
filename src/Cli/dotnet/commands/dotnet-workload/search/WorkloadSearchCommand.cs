// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.IO;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Linq;
using Microsoft.DotNet.Workloads.Workload.Install;
using System.Collections.Generic;

namespace Microsoft.DotNet.Workloads.Workload.Search
{
    internal class WorkloadSearchCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly VerbosityOptions _verbosity;
        private readonly IWorkloadResolver _workloadResolver;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _workloadIdStub;

        public WorkloadSearchCommand(
            ParseResult result,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            string version = null) : base(result)
        {
            _reporter = reporter ?? Reporter.Output;
            _verbosity = result.ValueForOption<VerbosityOptions>(WorkloadSearchCommandParser.VerbosityOption);
            _workloadIdStub = result.ValueForArgument<string>(WorkloadSearchCommandParser.WorkloadIdStubArgument);
            var dotnetPath = Path.GetDirectoryName(Environment.ProcessPath);
            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(result.ValueForOption<string>(WorkloadSearchCommandParser.VersionOption), version, dotnetPath);
            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, _sdkVersion.ToString());
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(workloadManifestProvider, dotnetPath, _sdkVersion.ToString());
        }

        public override int Execute()
        {
            IEnumerable<WorkloadResolver.WorkloadInfo> availableWorkloads = _workloadResolver.GetAvailableWorkloads()
                .OrderBy(workload => workload.Id);

            if (!string.IsNullOrEmpty(_workloadIdStub))
            {
                availableWorkloads = availableWorkloads.Where(workload => workload.Id.ToString().Contains(_workloadIdStub, StringComparison.OrdinalIgnoreCase));
            }

            var table = new PrintableTable<WorkloadResolver.WorkloadInfo>();
            table.AddColumn(LocalizableStrings.WorkloadIdColumnName, workload => workload.Id.ToString());
            table.AddColumn(LocalizableStrings.DescriptionColumnName, workload => workload.Description);

            _reporter.WriteLine();
            table.PrintRows(availableWorkloads, l => _reporter.WriteLine(l));
            _reporter.WriteLine();

            return 0;
        }
    }
}
