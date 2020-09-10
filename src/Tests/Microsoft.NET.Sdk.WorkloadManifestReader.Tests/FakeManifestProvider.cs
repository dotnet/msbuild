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

            public IEnumerable<Stream> GetManifests()
            {
                foreach (var filePath in _filePaths)
                {
                    yield return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                }
            }
        }
}
