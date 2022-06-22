// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal partial class NetSdkMsiInstallerClient
    {
        class WorkloadPackGroupJson
        {
            public string GroupPackageId { get; set; }
            public string GroupPackageVersion { get; set; }

            public List<WorkloadPackJson> Packs { get; set; } = new List<WorkloadPackJson>();
        }

        class WorkloadPackJson
        {
            public string PackId { get; set; }

            public string PackVersion { get; set; }
        }

        Dictionary<(string packId, string packVersion), List<WorkloadPackGroupJson>> GetWorkloadPackGroups()
        {
            Dictionary<(string packId, string packVersion), List<WorkloadPackGroupJson>> ret = new();

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_DISABLE_PACK_GROUPS)))
            {
                //  If workload pack groups are disabled via environment variable, then just return an empty Dictionary
                return ret;
            }

            var manifests = _workloadResolver.GetInstalledManifests();
            foreach (var manifest in manifests)
            {
                var packGroupFile = Path.Combine(manifest.ManifestDirectory, "WorkloadPackGroups.json");
                if (File.Exists(packGroupFile))
                {
                    var packGroups = JsonSerializer.Deserialize<IList<WorkloadPackGroupJson>>(File.ReadAllText(packGroupFile));
                    foreach (var packGroup in packGroups)
                    {
                        foreach (var packJson in packGroup.Packs)
                        {
                            var pack = (packId: packJson.PackId, packVersion: packJson.PackVersion);
                            if (!ret.TryGetValue(pack, out var groupsWithPack))
                            {
                                groupsWithPack = new();
                                ret[pack] = groupsWithPack;
                            }
                            groupsWithPack.Add(packGroup);
                        }
                    }
                }
            }

            return ret;
        }
        List<WorkloadDownload> GetMsisForWorkloads(IEnumerable<WorkloadId> workloads)
        {
            var packs = workloads
                .SelectMany(workloadId => _workloadResolver.GetPacksInWorkload(workloadId))
                .Distinct()
                .Select(packId => _workloadResolver.TryGetPackInfo(packId))
                .Where(pack => pack != null);

            return GetMsisForPacks(packs);
        }


        List<WorkloadDownload> GetMsisForPacks(IEnumerable<PackInfo> packInfos)
        {
            List<WorkloadDownload> msisToInstall = new();
            HashSet<(string packId, string packVersion)> packsProcessed = new();

            var groupsForPacks = GetWorkloadPackGroups();

            foreach (var pack in packInfos)
            {
                if (packsProcessed.Contains((pack.Id, pack.Version)))
                {
                    continue;
                }

                if (groupsForPacks.TryGetValue((pack.Id, pack.Version), out var groups))
                {
                    var group = groups.First();
                    msisToInstall.Add(new WorkloadDownload(group.GroupPackageId, $"{group.GroupPackageId}.Msi.{HostArchitecture}", group.GroupPackageVersion));
                    foreach (var packFromGroup in group.Packs)
                    {
                        packsProcessed.Add((packFromGroup.PackId, packFromGroup.PackVersion));
                    }
                }
                else
                {
                    msisToInstall.Add(GetWorkloadDownloadForPack(pack));
                    packsProcessed.Add((pack.Id, pack.Version));
                }
            }

            return msisToInstall;
        }

        WorkloadDownload GetWorkloadDownloadForPack(PackInfo packInfo)
        {
            return new WorkloadDownload(packInfo.ResolvedPackageId, GetMsiPackageId(packInfo), packInfo.Version);
        }
    }
}
