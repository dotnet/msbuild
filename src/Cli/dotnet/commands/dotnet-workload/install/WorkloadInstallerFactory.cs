// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallerFactory
    {
        public static IInstaller GetWorkloadInstaller(
            IReporter reporter,
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver, 
            VerbosityOptions verbosity,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null, 
            PackageSourceLocation packageSourceLocation = null)
        {
            return new NetSdkManagedInstaller(reporter, sdkFeatureBand, workloadResolver, nugetPackageDownloader, verbosity: verbosity, dotnetDir: dotnetDir, packageSourceLocation: packageSourceLocation);
        }
    }
}
