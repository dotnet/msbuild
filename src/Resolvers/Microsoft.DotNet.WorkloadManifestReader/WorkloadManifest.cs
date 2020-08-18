using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class WorkloadManifest
    {
        public Dictionary<string, List<string>> Workloads { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, (string version, string kind)> SdkPackDetail { get; set; } = new Dictionary<string, (string version, string kind)>(StringComparer.OrdinalIgnoreCase);

        public static WorkloadManifest LoadFromFolder(string manifestFolder)
        {
            var manifest = new WorkloadManifest();

            string manifestPath = Path.Combine(manifestFolder, "WorkloadManifest.xml");
            if (File.Exists(manifestPath))
            {
                var manifestXml = XDocument.Load(manifestPath);

                if (manifestXml.Root?.Element("Workloads")?.Elements("Workload") != null)
                {
                    foreach (var workload in manifestXml.Root.Element("Workloads").Elements("Workload"))
                    {
                        string workloadName = workload?.Attribute("Name").Value;
                        var workloadPacks = workload.Elements("RequiredPack").Select(rp => rp.Attribute("Name").Value)
                            .ToList();

                        manifest.Workloads[workloadName] = workloadPacks;
                    }
                }

                if (manifestXml.Root?.Element("WorkloadPacks")?.Elements("Pack") != null)
                {
                    foreach (var pack in manifestXml.Root.Element("WorkloadPacks").Elements("Pack"))
                    {
                        string packName = pack.Attribute("Name").Value;
                        string packVersion = pack.Attribute("Version").Value;
                        string kind = pack.Attribute("Kind").Value;
                        manifest.SdkPackDetail[packName] = (packVersion, kind);
                    }
                }
            }

            return manifest;
        }

        public static WorkloadManifest Merge(IEnumerable<WorkloadManifest> manifests)
        {
            if (!manifests.Any())
            {
                return new WorkloadManifest();
            }
            else if (manifests.Count() == 1)
            {
                return manifests.Single();
            }
            else
            {
                var mergedManifest = new WorkloadManifest();
                foreach (var manifest in manifests)
                {
                    foreach (var workload in manifest.Workloads)
                    {
                        mergedManifest.Workloads.Add(workload.Key, workload.Value);
                    }
                    foreach (var sdkPackVersion in manifest.SdkPackDetail)
                    {
                        mergedManifest.SdkPackDetail.Add(sdkPackVersion.Key, sdkPackVersion.Value);
                    }
                }
                return mergedManifest;
            }
        }
    }
}
