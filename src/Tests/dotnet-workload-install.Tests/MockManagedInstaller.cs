// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ManifestReaderTests;
using Microsoft.DotNet.Cli.NuGetPackageInstaller;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockManagedInstaller : NetSdkManagedInstaller
    {
        private readonly string _manifestPath;

        public MockManagedInstaller(IReporter reporter, INuGetPackageInstaller nugetPackageInstaller, string dotnetDir, string manifestPath) :
            base(reporter, nugetPackageInstaller, dotnetDir)
        {
            _manifestPath = manifestPath;
        }

        protected override WorkloadResolver GetWorkloadResolver(string featureBand)
        {
            return WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { _dotnetDir });
        }
    }
}
