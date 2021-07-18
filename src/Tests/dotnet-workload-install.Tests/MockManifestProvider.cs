// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Sdk.WorkloadManifestReader;

using System;
using System.Collections.Generic;
using System.IO;

namespace ManifestReaderTests
{
    internal class MockManifestProvider : IWorkloadManifestProvider
    {
        readonly (string name, string path)[] _manifests;

        public MockManifestProvider(params string[] manifestPaths)
        {
            _manifests = Array.ConvertAll(manifestPaths, mp =>
            {
                string manifestId = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(mp));
                return (manifestId, mp);
            });
        }

        public MockManifestProvider(params (string name, string path)[] manifests)
        {
            _manifests = manifests;
        }

        public IEnumerable<string> GetManifestDirectories()
        {
            foreach ((_, var filePath) in _manifests)
            {
                yield return Path.GetDirectoryName(filePath);
            }
        }

        public IEnumerable<(string manifestId, string informationalPath, Func<Stream> openManifestStream)> GetManifests()
            {
                foreach ((var id, var path) in _manifests)
                {
                    yield return (id, path, () => File.OpenRead(path));
                }
            }

        public string GetSdkFeatureBand() => "6.0.100";
    }
}
