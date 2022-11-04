// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Sdk.WorkloadManifestReader;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ManifestReaderTests
{
    internal class FakeManifestProvider : IWorkloadManifestProvider
    {
        readonly (string manifest, string? localizationCatalog)[] _filePaths;

        public FakeManifestProvider(params string[] filePaths)
        {
            _filePaths = filePaths.Select(p => (p, (string?)null)).ToArray();
        }

        public FakeManifestProvider(params (string manifest, string? localizationCatalog)[] filePaths)
        {
            _filePaths = filePaths;
        }

        public IEnumerable<string> GetManifestDirectories() => throw new NotImplementedException();

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
        {
            foreach (var filePath in _filePaths)
            {
                yield return new(
                    Path.GetFileNameWithoutExtension(filePath.manifest),
                    filePath.manifest,
                    () => new FileStream(filePath.manifest, FileMode.Open, FileAccess.Read),
                    () => filePath.localizationCatalog != null ? new FileStream(filePath.localizationCatalog, FileMode.Open, FileAccess.Read) : null
                );
            }
        }

        public string GetSdkFeatureBand() => throw new NotImplementedException();
    }

    internal class InMemoryFakeManifestProvider : IWorkloadManifestProvider, IEnumerable<(string id, string content)>
    {
        readonly List<(string id, byte[] content)> _manifests = new List<(string, byte[])>();

        public void Add(string id, string content) => _manifests.Add((id, Encoding.UTF8.GetBytes(content)));
        public IEnumerable<string> GetManifestDirectories() => throw new NotImplementedException();

        public IEnumerable<ReadableWorkloadManifest> GetManifests()
            => _manifests.Select(m => new ReadableWorkloadManifest(
                m.id,
                $@"C:\fake\{m.id}\WorkloadManifest.json",
                (Func<Stream>)(() => new MemoryStream(m.content)),
                (Func<Stream?>)(() => null)
            ));

        // these are just so the collection initializer works
        public IEnumerator<(string id, string content)> GetEnumerator() => throw new NotImplementedException();
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
        public string GetSdkFeatureBand() => throw new NotImplementedException();
    }
}
