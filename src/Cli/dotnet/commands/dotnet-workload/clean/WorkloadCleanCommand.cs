// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.DotNet.Workloads.Workload.Install;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;

#nullable enable

namespace Microsoft.DotNet.Workloads.Workload.Clean
{
    internal class WorkloadCleanCommand : WorkloadCommandBase
    {
        private readonly bool _cleanAll;

        private string? _dotnetPath;
        private string _userProfileDir;

        private readonly ReleaseVersion _sdkVersion;
        private readonly IInstaller _workloadInstaller;
        private readonly IWorkloadResolver _workloadResolver;

        public WorkloadCleanCommand(
            ParseResult parseResult,
            IReporter? reporter = null,
            IWorkloadResolver? workloadResolver = null,
            string? dotnetDir = null,
            string? version = null,
            string? userProfileDir = null
            ) : base(parseResult, reporter: reporter)
        {
            _cleanAll = parseResult.GetValue(WorkloadCleanCommandParser.CleanAllOption);

            var creationParameters = new WorkloadResolverFactory.CreationParameters()
            {
                DotnetPath = dotnetDir,
                UserProfileDir = userProfileDir,
                GlobalJsonStartDir = null,
                SdkVersionFromOption = parseResult.GetValue(WorkloadUninstallCommandParser.VersionOption),
                VersionForTesting = version,
                CheckIfFeatureBandManifestExists = true,
                WorkloadResolverForTesting = workloadResolver,
                UseInstalledSdkVersionForResolver = true
            };

            var creationResult = WorkloadResolverFactory.Create(creationParameters);

            _dotnetPath = creationResult.DotnetPath;
            _userProfileDir = creationResult.UserProfileDir;
            _workloadResolver = creationResult.WorkloadResolver;
            _sdkVersion = creationResult.SdkVersion;

            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);
            _workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand, creationResult.WorkloadResolver, Verbosity, creationResult.UserProfileDir, VerifySignatures, PackageDownloader, creationResult.DotnetPath);
        }

        public override int Execute()
        {
            ExecuteGarbageCollection();
            return 0;
        }

        private void ExecuteGarbageCollection()
        {
            _workloadInstaller.GarbageCollectInstalledWorkloadPacks(cleanAllPacks: _cleanAll);
            DisplayUninstallableVSWorkloads();
        }

        /// <summary>
        /// Print VS Workloads with the same machine arch which can't be uninstalled through the SDK CLI to increase user awareness that they must uninstall via VS.
        /// </summary>
        private void DisplayUninstallableVSWorkloads()
        {
#if !DOT_NET_BUILD_FROM_SOURCE
            // We don't want to print MSI related content in a file-based installation.
            if (!(_workloadInstaller.GetType() == typeof(NetSdkMsiInstallerClient)))
            {
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                // All VS Workloads should have a corresponding MSI based SDK. This means we can pull all of the VS SDK feature bands using MSI/VS related registry keys.
                var installedSDKVersionsWithPotentialVSRecords = MsiInstallerBase.GetInstalledSdkVersions();
                HashSet<string> vsWorkloadUninstallWarnings = new();

                string defaultDotnetWinPath = MsiInstallerBase.GetDotNetHome();
                foreach (string sdkVersion in installedSDKVersionsWithPotentialVSRecords)
                {
                    try
                    {
#pragma warning disable CS8604 // We error in the constructor if the dotnet path is null.

                        // We don't know if the dotnet installation for the other bands is in a different directory than the current dotnet; check the default directory if it isn't.
                        var bandedDotnetPath = Path.Exists(Path.Combine(_dotnetPath, "sdk", sdkVersion)) ? _dotnetPath : defaultDotnetWinPath;

                        if (!Path.Exists(bandedDotnetPath))
                        {
                            Reporter.WriteLine(AnsiExtensions.Yellow(string.Format(LocalizableStrings.CannotAnalyzeVSWorkloadBand, sdkVersion, _dotnetPath, defaultDotnetWinPath)));
                            continue;
                        }

                        var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(bandedDotnetPath, sdkVersion, _userProfileDir, SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(Environment.CurrentDirectory));
                        var bandedResolver = WorkloadResolver.Create(workloadManifestProvider, bandedDotnetPath, sdkVersion.ToString(), _userProfileDir);
#pragma warning restore CS8604

                        InstalledWorkloadsCollection vsWorkloads = new();
                        VisualStudioWorkloads.GetInstalledWorkloads(bandedResolver, vsWorkloads, _cleanAll ? null : new SdkFeatureBand(sdkVersion));
                        foreach (var vsWorkload in vsWorkloads.AsEnumerable())
                        {
                            vsWorkloadUninstallWarnings.Add(string.Format(LocalizableStrings.VSWorkloadNotRemoved, $"{vsWorkload.Key}", $"{vsWorkload.Value}"));
                        }
                    }
                    catch (WorkloadManifestException ex)
                    {
                        // Limitation: We don't know the dotnetPath of the other feature bands when making the manifestProvider and resolvers.
                        // This can cause the manifest resolver to fail as it may look for manifests in an invalid path.
                        // It can theoretically be customized, but that is not currently supported for workloads with VS.
                        Reporter.WriteLine(AnsiExtensions.Yellow(string.Format(LocalizableStrings.CannotAnalyzeVSWorkloadBand, sdkVersion, _dotnetPath, defaultDotnetWinPath)));
                        Cli.Utils.Reporter.Verbose.WriteLine(ex.Message);
                    }
                }

                foreach (string warning in vsWorkloadUninstallWarnings)
                {
                    Reporter.WriteLine(warning.Yellow());
                }
            }
#endif
        }
    }
}
