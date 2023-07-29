// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Common;

namespace Microsoft.DotNet.Workloads.Workload
{
    internal static class WorkloadIntegrityChecker
    {
        public static void RunFirstUseCheck(IReporter reporter)
        {
            var creationParameters = new WorkloadResolverFactory.CreationParameters()
            {
                CheckIfFeatureBandManifestExists = true,
                UseInstalledSdkVersionForResolver = true
            };

            var creationResult = WorkloadResolverFactory.Create(creationParameters);
            var sdkFeatureBand = new SdkFeatureBand(creationResult.SdkVersion);
            var verifySignatures = WorkloadCommandBase.ShouldVerifySignatures();
            var tempPackagesDirectory = new DirectoryPath(PathUtilities.CreateTempSubdirectory());
            var packageDownloader = new NuGetPackageDownloader(
                tempPackagesDirectory,
                verboseLogger: new NullLogger(),
                verifySignatures: verifySignatures);

            var installer = WorkloadInstallerFactory.GetWorkloadInstaller(
                reporter,
                sdkFeatureBand,
                creationResult.WorkloadResolver,
                VerbosityOptions.normal,
                creationResult.UserProfileDir,
                verifySignatures,
                packageDownloader,
                creationResult.DotnetPath);
            var repository = installer.GetWorkloadInstallationRecordRepository();
            var installedWorkloads = repository.GetInstalledWorkloads(sdkFeatureBand);

            if (installedWorkloads.Any())
            {
                reporter.WriteLine(LocalizableStrings.WorkloadIntegrityCheck);
                CliTransaction.RunNew(context => installer.InstallWorkloads(installedWorkloads, sdkFeatureBand, context));
                reporter.WriteLine("----------------");
            }
        }
    }
}
