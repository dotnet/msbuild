using Microsoft.Net.Sdk.WorkloadManifestReader;

using System.Collections.Generic;
using System.IO;

namespace ManifestReaderTests
{
    class FakeManifestProvider : IWorkloadManifestProvider
        {
            readonly string[] filePaths;

            public FakeManifestProvider(params string[] filePaths)
            {
                this.filePaths = filePaths;
            }

            public IEnumerable<Stream> GetManifests()
            {
                foreach (var filePath in filePaths)
                {
                    yield return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                }
            }
        }
}
