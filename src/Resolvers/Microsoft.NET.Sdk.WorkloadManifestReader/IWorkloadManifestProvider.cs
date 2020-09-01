using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{
    public interface IWorkloadManifestProvider
    {
        IEnumerable<Stream> GetManifests();
    }
}
