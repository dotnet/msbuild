// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallerFactory
    {
        public static IInstaller GetWorkloadInstaller(
            IReporter reporter,
            SdkFeatureBand sdkFeatureBand,
            IWorkloadResolver workloadResolver,
            VerbosityOptions verbosity,
            string userProfileDir,
            bool verifySignatures,
            INuGetPackageDownloader nugetPackageDownloader = null,
            string dotnetDir = null,
            string tempDirPath = null,
            PackageSourceLocation packageSourceLocation = null,
            RestoreActionConfig restoreActionConfig = null,
            bool elevationRequired = true)
        {
            dotnetDir = string.IsNullOrWhiteSpace(dotnetDir) ? Path.GetDirectoryName(Environment.ProcessPath) : dotnetDir;
            var installType = WorkloadInstallType.GetWorkloadInstallType(sdkFeatureBand, dotnetDir);

            if (installType == InstallType.Msi)
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new InvalidOperationException(LocalizableStrings.OSDoesNotSupportMsi);
                }

                return NetSdkMsiInstallerClient.Create(verifySignatures, sdkFeatureBand, workloadResolver,
                    nugetPackageDownloader, verbosity, packageSourceLocation, reporter, tempDirPath);
            }

            if (elevationRequired && !WorkloadFileBasedInstall.IsUserLocal(dotnetDir, sdkFeatureBand.ToString()) && !CanWriteToDotnetRoot(dotnetDir))
            {
                throw new GracefulException(LocalizableStrings.InadequatePermissions, isUserError: false);
            }

            userProfileDir ??= CliFolderPathCalculator.DotnetUserProfileFolderPath;

            return new FileBasedInstaller(
                reporter,
                sdkFeatureBand,
                workloadResolver,
                userProfileDir,
                nugetPackageDownloader,
                dotnetDir: dotnetDir,
                tempDirPath: tempDirPath,
                verbosity: verbosity,
                packageSourceLocation: packageSourceLocation,
                restoreActionConfig: restoreActionConfig);
        }

        private static bool CanWriteToDotnetRoot(string dotnetDir = null)
        {
            dotnetDir ??= Path.GetDirectoryName(Environment.ProcessPath);
            try
            {
                var testPath = Path.Combine(dotnetDir, "metadata", Path.GetRandomFileName());
                if (Directory.Exists(Path.GetDirectoryName(testPath)))
                {
                    using FileStream fs = File.Create(testPath, 1, FileOptions.DeleteOnClose);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(testPath));
                }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
