// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class TempDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        private readonly string _manifestsPath;
        private readonly string _sdkVersionBand;

        public TempDirectoryWorkloadManifestProvider(string manifestsPath, string sdkFeatureBand)
        {
            _manifestsPath = manifestsPath;
            _sdkVersionBand = sdkFeatureBand;
        }

        public IEnumerable<ReadableWorkloadManifest>
            GetManifests()
        {
            foreach (var workloadManifestDirectory in GetManifestDirectories())
            {
                string? workloadManifestPath = Path.Combine(workloadManifestDirectory, "WorkloadManifest.json");
                var manifestId = Path.GetFileName(workloadManifestDirectory);

                int index = manifestId.IndexOf(".Manifest", StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    manifestId = manifestId.Substring(0, index);
                }

                yield return new(
                    manifestId,
                    workloadManifestPath,
                    () => File.OpenRead(workloadManifestPath),
                    () => WorkloadManifestReader.TryOpenLocalizationCatalogForManifest(workloadManifestPath)
                );
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

        public string GetSdkFeatureBand() => _sdkVersionBand;
    }
}
