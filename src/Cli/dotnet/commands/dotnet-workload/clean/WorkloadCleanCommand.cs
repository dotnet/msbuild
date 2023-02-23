// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

            _dotnetPath = dotnetDir ?? Path.GetDirectoryName(Environment.ProcessPath);
            if (_dotnetPath == null)
            {
                throw new GracefulException(String.Format(LocalizableStrings.InvalidWorkloadProcessPath, Environment.ProcessPath ?? "null"));
            }

            _userProfileDir = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;

            _sdkVersion = WorkloadOptionsExtensions.GetValidatedSdkVersion(parseResult.GetValue(WorkloadUninstallCommandParser.VersionOption), version, _dotnetPath, userProfileDir, true);
            var sdkFeatureBand = new SdkFeatureBand(_sdkVersion);

            var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(_dotnetPath, _sdkVersion.ToString(), _userProfileDir);
            _workloadResolver = workloadResolver ?? WorkloadResolver.Create(workloadManifestProvider, _dotnetPath, _sdkVersion.ToString(), _userProfileDir);
            _workloadInstaller = WorkloadInstallerFactory.GetWorkloadInstaller(Reporter, sdkFeatureBand, _workloadResolver, Verbosity, _userProfileDir, VerifySignatures, PackageDownloader, _dotnetPath);
            
        }

        public override int Execute()
        {
            ExecuteGarbageCollection();
            return 0;
        }

        private void ExecuteGarbageCollection()
        {
            if (_cleanAll)
            {
                //_workloadInstaller.GarbageCollectInstalledWorkloadPacks(cleanAllPacks: true);
            }
            else
            {
                _workloadInstaller.GarbageCollectInstalledWorkloadPacks();
            }

            DisplayUninstallableVSWorkloads();
        }

        /// <summary>
        /// Print VS Workloads with the same machine arch which can't be uninstalled through the SDK CLI to increase user awareness that they must uninstall via VS.
        /// </summary>
        private void DisplayUninstallableVSWorkloads()
        {
#if !DOT_NET_BUILD_FROM_SOURCE
            if (OperatingSystem.IsWindows())
            {
                // All VS Workloads should have a corresponding MSI based SDK. This means we can pull all of the VS SDK feature bands using MSI/VS related registry keys.
                var installedSDKVersionsWithPotentialVSRecords = MsiInstallerBase.GetInstalledSdkVersions();
                HashSet<string> vsWorkloadUninstallWarnings = new();

                foreach (string sdkVersion in installedSDKVersionsWithPotentialVSRecords)
                {
                    try
                    {
#pragma warning disable CS8604 // We error in the constructor if the dotnet path is null.
                        // The below environment expansion should be architecture agnostic for the C:\Program Files path, but only works on windows 7+. https://learn.microsoft.com/en-us/windows/win32/winprog64/wow64-implementation-details?redirectedfrom=MSDN
                        // In addition, we don't need to worry about when dotnet will be dotnet/x64 (x64 on processor arch of arm64) because MSI install thru VS will only support installs on processor arch, not chip/emulation arch.
                        string defaultDotnetWinPath = Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramW6432%"), "dotnet");

                        // We don't know if the dotnet installation for the other bands is in a different directory than the current dotnet; check the default directory if it isn't.
                        var bandedDotnetPath = Path.Exists(Path.Combine(_dotnetPath, "sdk", sdkVersion)) ? _dotnetPath : defaultDotnetWinPath;

                        var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(bandedDotnetPath, sdkVersion, _userProfileDir);
                        var bandedResolver = WorkloadResolver.Create(workloadManifestProvider, bandedDotnetPath, sdkVersion.ToString(), _userProfileDir);
#pragma warning restore CS8604

                        InstalledWorkloadsCollection vsWorkloads = new();
                        VisualStudioWorkloads.GetInstalledWorkloads(bandedResolver, vsWorkloads, _cleanAll ? null : new SdkFeatureBand(sdkVersion));
                        foreach (var vsWorkload in vsWorkloads.AsEnumerable())
                        {
                            vsWorkloadUninstallWarnings.Add(string.Format(LocalizableStrings.VSWorkloadNotRemoved, $"{vsWorkload.Key} ({vsWorkload.Value})"));
                        }
                    }
                    catch (Exception ex)
                    {
                        // Limitation: We don't know the dotnetPath of the other feature bands when making the manifestProvider and resolvers.
                        // It can theoretically be customized, but that is not currently supported for workloads with VS.
                        Reporter.WriteLine(AnsiColorExtensions.Yellow(string.Format(LocalizableStrings.CannotAnalyzeVSWorkloadBand, sdkVersion, ex)));
                    }
                }

                foreach(string warning in vsWorkloadUninstallWarnings)
                {
                    Reporter.WriteLine(AnsiColorExtensions.Yellow(warning));
                }
            }
#endif
        }
    }
}
