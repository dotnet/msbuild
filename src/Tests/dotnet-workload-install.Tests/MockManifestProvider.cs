// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.Sdk.WorkloadManifestReader;

using System;
using System.Collections.Generic;
using System.IO;

namespace ManifestReaderTests
{
    internal class MockManifestProvider : IWorkloadManifestProvider
    {
        readonly (string name, string path, string featureBand)[] _manifests;

        public MockManifestProvider(params string[] manifestPaths)
        {
            _manifests = Array.ConvertAll(manifestPaths, mp =>
            {
                string manifestId = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(mp));
                return (manifestId, mp, (string)null);
            });
            SdkFeatureBand = new SdkFeatureBand("6.0.100");
        }

        public MockManifestProvider(params (string name, string path, string featureBand)[] manifests)
        {
            _manifests = manifests;
            SdkFeatureBand = new SdkFeatureBand("6.0.100");
        }

        public SdkFeatureBand SdkFeatureBand { get; set; }

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
            {
                foreach ((var id, var path, var featureBand) in _manifests)
                {
                    yield return new(
                        id,
                        Path.GetDirectoryName(path),
                        path,
                        featureBand ?? SdkFeatureBand.ToString(),
                        () => File.OpenRead(path),
                        () => WorkloadManifestReader.TryOpenLocalizationCatalogForManifest(path)
                    );
                }
            }

        public string GetSdkFeatureBand() => SdkFeatureBand.ToString();
    }
}
