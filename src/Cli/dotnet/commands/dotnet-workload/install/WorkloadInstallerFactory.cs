// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Configurer;

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
            var installType = GetWorkloadInstallType(sdkFeatureBand, dotnetDir);

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

            return new FileBasedInstaller(reporter,
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

        /// <summary>
        /// Determines the <see cref="InstallType"/> associated with a specific SDK version.
        /// </summary>
        /// <param name="sdkFeatureBand">The SDK version to check.</param>
        /// <returns>The <see cref="InstallType"/> associated with the SDK.</returns>
        public static InstallType GetWorkloadInstallType(SdkFeatureBand sdkFeatureBand, string dotnetDir)
        {
            string installerTypePath = Path.Combine(dotnetDir, "metadata",
                "workloads", $"{sdkFeatureBand.ToStringWithoutPrerelease()}", "installertype");

            if (File.Exists(Path.Combine(installerTypePath, "msi")))
            {
                return InstallType.Msi;
            }

            return InstallType.FileBased;
        }

        private static bool CanWriteToDotnetRoot(string dotnetDir = null)
        {
            dotnetDir = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            try
            {
                var testPath = Path.Combine(dotnetDir, "metadata", Path.GetRandomFileName());
                if (Directory.Exists(Path.GetDirectoryName(testPath)))
                {
                    using (FileStream fs = File.Create(testPath, 1, FileOptions.DeleteOnClose)) { }
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
