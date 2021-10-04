// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Product = Microsoft.DotNet.Cli.Utils.Product;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadOptionsExtensions
    {
        internal static ReleaseVersion GetValidatedSdkVersion(string versionOption, string providedVersion, string dotnetPath, string userProfileDir)
        {

            if (string.IsNullOrEmpty(versionOption))
            {
                return new ReleaseVersion(providedVersion ?? Product.Version);
            }
            else
            {
                var manifests = new SdkDirectoryWorkloadManifestProvider(dotnetPath, versionOption, userProfileDir).GetManifests();
                if (!manifests.Any())
                {
                    throw new GracefulException(string.Format(LocalizableStrings.NoManifestsExistForFeatureBand, versionOption), isUserError: false);
                }
                try
                {
                    foreach ((string manifestId, string informationalPath, Func<Stream> openManifestStream, Func<Stream> openLocalizationStream) in manifests)
                    {
                        using (var manifestStream = openManifestStream())
                        using (var localizationStream = openLocalizationStream())
                        {
                            var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId, manifestStream, localizationStream, informationalPath);
                        }
                    }
                }
                catch
                {
                    throw new GracefulException(string.Format(LocalizableStrings.IncompatibleManifests, versionOption), isUserError: false);
                }

                return new ReleaseVersion(versionOption);
            }
        }
    }
}
