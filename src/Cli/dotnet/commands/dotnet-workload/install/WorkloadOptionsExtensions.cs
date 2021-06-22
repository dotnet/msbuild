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
        internal static ReleaseVersion GetValidatedSdkVersion(string versionOption, string providedVersion, string dotnetPath)
        {

            if (string.IsNullOrEmpty(versionOption))
            {
                return new ReleaseVersion(providedVersion ?? Product.Version);
            }
            else
            {
                var manifests = new SdkDirectoryWorkloadManifestProvider(dotnetPath, versionOption).GetManifests();
                if (!manifests.Any())
                {
                    throw new GracefulException(string.Format(LocalizableStrings.NoManifestsExistForFeatureBand, versionOption));
                }
                try
                {
                    foreach ((string manifestId, string informationalPath, Func<Stream> openManifestStream) in manifests)
                    {
                        using (var manifestStream = openManifestStream())
                        {
                            var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId, manifestStream);
                        }
                    }
                }
                catch
                {
                    throw new GracefulException(string.Format(LocalizableStrings.IncompatibleManifests, versionOption));
                }

                return new ReleaseVersion(versionOption);
            }
        }
    }
}
