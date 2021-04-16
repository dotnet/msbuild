// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Collections.Generic;
using System.IO;

namespace ManifestReaderTests
{
    internal class MockManifestProvider : IWorkloadManifestProvider
    {
        readonly string[] _filePaths;

        public MockManifestProvider(params string[] filePaths)
        {
            _filePaths = filePaths;
        }

        public IEnumerable<string> GetManifestDirectories() => throw new System.NotImplementedException();

        public IEnumerable<(string manifestId, Stream manifestStream)> GetManifests()
            {
                foreach (var filePath in _filePaths)
                {
                    yield return (filePath, new FileStream(filePath, FileMode.Open, FileAccess.Read));
                }
            }
        }
}
