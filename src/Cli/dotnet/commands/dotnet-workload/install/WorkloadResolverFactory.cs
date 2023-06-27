// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal static class WorkloadResolverFactory
    {
        public class CreationParameters
        {
            public string DotnetPath { get; set; }
            public string UserProfileDir { get; set; }
            public string GlobalJsonStartDir { get; set; }
            public string SdkVersionFromOption { get; set; }
            public string VersionForTesting { get; set; }
            public bool CheckIfFeatureBandManifestExists { get; set; }
            public IWorkloadResolver WorkloadResolverForTesting { get; set; }

            public bool UseInstalledSdkVersionForResolver { get; set; }
        }

        public class CreationResult
        {
            public string DotnetPath { get; set; }
            public string UserProfileDir { get; set; }
            public ReleaseVersion SdkVersion { get; set; }
            public ReleaseVersion InstalledSdkVersion { get; set; }
            public IWorkloadResolver WorkloadResolver { get; set; }

        }

        public static CreationResult Create(CreationParameters parameters)
        {
            var result = new CreationResult();

            result.InstalledSdkVersion = new ReleaseVersion(parameters.VersionForTesting ?? Product.Version);

            bool manifestsNeedValidation;
            if (string.IsNullOrEmpty(parameters.SdkVersionFromOption))
            {
                result.SdkVersion = result.InstalledSdkVersion;
                manifestsNeedValidation = false;
            }
            else
            {
                result.SdkVersion = new ReleaseVersion(parameters.SdkVersionFromOption);
                manifestsNeedValidation = true;
            }

            result.DotnetPath = parameters.DotnetPath ?? Path.GetDirectoryName(Environment.ProcessPath);
            result.UserProfileDir = parameters.UserProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;
            string globalJsonStartDir = parameters.GlobalJsonStartDir ?? Environment.CurrentDirectory;

            string globalJsonPath = SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(globalJsonStartDir);

            var sdkWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(result.DotnetPath, result.SdkVersion.ToString(), result.UserProfileDir, globalJsonPath);

            if (manifestsNeedValidation)
            {
                var manifests = sdkWorkloadManifestProvider.GetManifests();
                if (parameters.CheckIfFeatureBandManifestExists && !manifests.Any())
                {
                    throw new GracefulException(string.Format(LocalizableStrings.NoManifestsExistForFeatureBand, result.SdkVersion.ToString()), isUserError: false);
                }
                try
                {
                    foreach (var readableManifest in manifests)
                    {
                        using (var manifestStream = readableManifest.OpenManifestStream())
                        using (var localizationStream = readableManifest.OpenLocalizationStream())
                        {
                            var manifest = WorkloadManifestReader.ReadWorkloadManifest(readableManifest.ManifestId, manifestStream, localizationStream, readableManifest.ManifestPath);
                        }
                    }
                }
                catch
                {
                    throw new GracefulException(string.Format(LocalizableStrings.IncompatibleManifests, parameters.SdkVersionFromOption), isUserError: false);
                }
            }

            ReleaseVersion versionForResolver = parameters.UseInstalledSdkVersionForResolver ? result.InstalledSdkVersion : result.SdkVersion;
            if (parameters.UseInstalledSdkVersionForResolver && !result.InstalledSdkVersion.Equals(result.SdkVersion))
            {
                //  Create new manifest provider using installed SDK version instead of the target SDK version
                sdkWorkloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(result.DotnetPath, result.InstalledSdkVersion.ToString(), result.UserProfileDir, globalJsonPath);
            }

            result.WorkloadResolver = parameters.WorkloadResolverForTesting ?? WorkloadResolver.Create(sdkWorkloadManifestProvider, result.DotnetPath, result.SdkVersion.ToString(), result.UserProfileDir);

            return result;
        }
    }
}
