// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;
using EnvironmentProvider = Microsoft.DotNet.NativeWrapper.EnvironmentProvider;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly bool _skipManifestUpdate;
        private IReadOnlyCollection<string> _workloadIds;
        private WorkloadInstallManager _workloadInstallManager;

        public WorkloadInstallCommand(
            ParseResult parseResult,
            IReporter reporter = null)
            : base(parseResult)
        {
            _reporter = reporter ?? Reporter.Output;
            _skipManifestUpdate = parseResult.ValueForOption<bool>(WorkloadInstallCommandParser.SkipManifestUpdateOption);
            _workloadIds = parseResult.ValueForArgument<IReadOnlyCollection<string>>(WorkloadInstallCommandParser.WorkloadIdArgument);

            var dotnetPath = EnvironmentProvider.GetDotnetExeDirectory();
            var sdkVersion = new ReleaseVersion(Product.Version);
            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetPath, sdkVersion.ToString());
            var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetPath, sdkVersion.ToString());
            var workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(_reporter);
            _workloadInstallManager = new WorkloadInstallManager(_reporter, workloadInstaller, workloadResolver);
        }

        public override int Execute()
        {
            _workloadInstallManager.InstallWorkloads(_workloadIds, _skipManifestUpdate);
            return 0;
        }
    }
}
