using System.Collections.Generic;
using System.IO;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class SdkDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        private readonly string _sdkRootPath;
        private readonly string _sdkVersionBand;

        public SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersionBand)
        {
            _sdkRootPath = sdkRootPath;
            _sdkVersionBand = sdkVersionBand;
        }

        public IEnumerable<Stream> GetManifests()
        {
            var manifestDirectory = Path.Combine(_sdkRootPath, "sdk-manifests", _sdkVersionBand);

            if (Directory.Exists(manifestDirectory))
            {
                foreach (var workloadName in Directory.EnumerateDirectories(manifestDirectory))
                {
                    var workloadManifest = Path.Combine(workloadName, "WorkloadManifest.json");
                    yield return File.OpenRead(workloadManifest);
                }
            }
        }
    }
}
