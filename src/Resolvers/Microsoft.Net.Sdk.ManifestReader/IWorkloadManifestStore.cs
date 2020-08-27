using System.Collections.Generic;
using System.IO;

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{

    public interface IWorkloadManifestStore
    {
        IEnumerable<StreamReader> GetManifests();
    }
}
