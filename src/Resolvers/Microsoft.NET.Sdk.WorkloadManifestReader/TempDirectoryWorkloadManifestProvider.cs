// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class TempDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        private readonly string _sdkVersionBand;
        private readonly string _manifestsPath;

        public TempDirectoryWorkloadManifestProvider(string manifestsPath, string sdkVersion)
        {
            _manifestsPath = manifestsPath;
            _sdkVersionBand = sdkVersion;
        }

        public IEnumerable<(string manifestId, string? informationalPath, Func<Stream> openManifestStream)> GetManifests()
        {
            foreach (var workloadManifestDirectory in GetManifestDirectories())
            {
                var workloadManifest = Path.Combine(workloadManifestDirectory, "WorkloadManifest.json");
                var id = Path.GetFileName(workloadManifestDirectory);
                yield return (id, workloadManifest, () => File.OpenRead(workloadManifest));
            }
        }

        public IEnumerable<string> GetManifestDirectories()
        {
            if (Directory.Exists(_manifestsPath))
            {
                foreach (var workloadManifestDirectory in Directory.EnumerateDirectories(_manifestsPath))
                {
                    yield return workloadManifestDirectory;
                }
            }
        }

        public string GetSdkFeatureBand()
        {
            return _sdkVersionBand;
        }
    }
}
