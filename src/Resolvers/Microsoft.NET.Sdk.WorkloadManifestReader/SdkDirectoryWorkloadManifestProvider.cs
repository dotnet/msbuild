using System.Collections.Generic;
using System.IO;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class SdkDirectoryWorkloadManifestProvider : IWorkloadManifestProvider
    {
        readonly string sdkRootPath;
        readonly string sdkVersionBand;

        public SdkDirectoryWorkloadManifestProvider(string sdkRootPath, string sdkVersionBand)
        {
            this.sdkRootPath = sdkRootPath;
            this.sdkVersionBand = sdkVersionBand;
        }

        public IEnumerable<Stream> GetManifests()
        {
            var manifestDirectory = Path.Combine(sdkRootPath, "sdk-manifests", sdkVersionBand);

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
