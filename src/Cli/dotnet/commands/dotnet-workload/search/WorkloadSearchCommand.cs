// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Linq;
using Microsoft.DotNet.Workloads.Workload.Install;
using System.Collections.Generic;

namespace Microsoft.DotNet.Workloads.Workload.Search
{
    internal class WorkloadSearchCommand : WorkloadCommandBase
    {
        private readonly IWorkloadResolver _workloadResolver;
        private readonly ReleaseVersion _sdkVersion;
        private readonly string _workloadIdStub;

        public WorkloadSearchCommand(
            ParseResult result,
            IReporter reporter = null,
            IWorkloadResolver workloadResolver = null,
            string version = null,
            string userProfileDir = null) : base(result, CommonOptions.HiddenVerbosityOption, reporter)
        {
            _workloadIdStub = result.GetValueForArgument(WorkloadSearchCommandParser.WorkloadIdStubArgument);

            var creationParameters = new WorkloadResolverFactory.CreationParameters()
            {
                DotnetPath = null,
                UserProfileDir = userProfileDir,
                GlobalJsonStartDir = null,
                SdkVersionFromOption = result.GetValueForOption(WorkloadSearchCommandParser.VersionOption),
                VersionForTesting = version,
                CheckIfFeatureBandManifestExists = true,
                WorkloadResolverForTesting = workloadResolver,
                UseInstalledSdkVersionForResolver = true
            };

            var creationResult = WorkloadResolverFactory.Create(creationParameters);

            _sdkVersion = creationResult.SdkVersion;
            _workloadResolver = creationResult.WorkloadResolver;
        }

        public override int Execute()
        {
            IEnumerable<WorkloadResolver.WorkloadInfo> availableWorkloads = _workloadResolver.GetAvailableWorkloads()
                .OrderBy(workload => workload.Id);

            if (!string.IsNullOrEmpty(_workloadIdStub))
            {
                availableWorkloads = availableWorkloads
                    .Where(workload => workload.Id.ToString().Contains(_workloadIdStub, StringComparison.OrdinalIgnoreCase) || (workload.Description?.Contains(_workloadIdStub, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var table = new PrintableTable<WorkloadResolver.WorkloadInfo>();
            table.AddColumn(LocalizableStrings.WorkloadIdColumnName, workload => workload.Id.ToString());
            table.AddColumn(LocalizableStrings.DescriptionColumnName, workload => workload.Description);

            Reporter.WriteLine();
            table.PrintRows(availableWorkloads, l => Reporter.WriteLine(l));
            Reporter.WriteLine();

            return 0;
        }
    }
}
