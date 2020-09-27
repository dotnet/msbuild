// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Collections.Generic;
using System.IO;

namespace ManifestReaderTests
{
    internal class FakeManifestProvider : IWorkloadManifestProvider
        {
            readonly string[] _filePaths;

            public FakeManifestProvider(params string[] filePaths)
            {
                _filePaths = filePaths;
            }

        public IEnumerable<string> GetManifestDirectories() => throw new System.NotImplementedException();

        public IEnumerable<Stream> GetManifests()
            {
                foreach (var filePath in _filePaths)
                {
                    yield return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                }
            }
        }
}
