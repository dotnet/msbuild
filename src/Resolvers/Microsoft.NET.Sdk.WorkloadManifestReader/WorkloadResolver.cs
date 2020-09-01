using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{
    /// <remarks>
    /// This very specifically exposes only the functionality needed right now by the MSBuild workload resolver
    /// and by the template engine. More general APIs will be added later.
    /// </remarks>
    public class WorkloadResolver : IWorkloadResolver
    {
        readonly Dictionary<string, WorkloadDefinition> workloads = new Dictionary<string, WorkloadDefinition>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, WorkloadPack> packs = new Dictionary<string, WorkloadPack>(StringComparer.OrdinalIgnoreCase);

        public static WorkloadResolver Create(IWorkloadManifestProvider manifestProvider)
        {
            var resolver = new WorkloadResolver();
            var manifests = new List<WorkloadManifest>();

            foreach (var manifestStream in manifestProvider.GetManifests())
            {
                using (manifestStream)
                {
                    var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestStream);
                    manifests.Add(manifest);
                }
            }

            foreach (var manifest in manifests)
            {
                foreach (var workload in manifest.Workloads)
                {
                    resolver.workloads.Add(workload.Key, workload.Value);
                }
                foreach (var pack in manifest.Packs)
                {
                    resolver.packs.Add(pack.Key, pack.Value);
                }
            }

            return resolver;
        }

        /// <summary>
        /// Gets the installed workload packs of a particular kind
        /// </summary>
        /// <remarks>
        /// Used by MSBuild resolver to scan SDK packs for AutoImport.props files to be imported.
        /// Used by template engine to find templatees to be added to hive.
        /// </remarks>
        public IEnumerable<PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind)
        {
            foreach (var pack in packs)
            {
                //TODO resolve aliases
                if (pack.Value.Kind != kind)
                {
                    continue;
                }

                //var packPath = "";
                /*
                if (!File.Exists(packPath))
                {
                    continue;
                }

                yield return new PackInfo(
                    pack.Value.Id,
                    pack.Value.Version,
                    pack.Value.Kind,
                    packPath
                );
                */
            }

            throw new NotImplementedException();
        }

        public IEnumerable<string> GetPacksInWorkload(string workloadId)
        {
            if (!workloads.TryGetValue(workloadId, out var workload))
            {
                throw new Exception("Workload not found");
            }

            if (workload.Extends?.Count > 0)
            {
                return ExpandWorkload(workload);
            }

            return workload.Packs ?? Enumerable.Empty<string>();
        }

        IEnumerable<string> ExpandWorkload (WorkloadDefinition workload)
        {
            var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> ExpandPacks (string workloadId)
            {
                if (!workloads.TryGetValue (workloadId, out var workloadInfo))
                {
                    // inconsistent manifest
                    throw new Exception("Workload not found");
                }

                if (workloadInfo.Packs != null && workloadInfo.Packs.Count > 0)
                {
                    foreach (var p in workloadInfo.Packs)
                    {
                        yield return p;
                    }
                }

                if (workloadInfo.Extends != null && workloadInfo.Extends.Count > 0)
                {
                    foreach (var e in workloadInfo.Extends)
                    {
                        if (dedup.Add(e))
                        {
                            foreach (var ep in ExpandPacks(e))
                            {
                                yield return ep;
                            }
                        }
                    }
                }
            }

            return ExpandPacks(workload.Id);
        }

        /// <summary>
        /// Gets the version of a workload pack for this resolver's SDK band
        /// </summary>
        /// <remarks>
        /// Used by the MSBuild SDK resolver to look up which versions of the SDK packs to import.
        /// </remarks>
        public string? TryGetPackVersion(string packId)
        {
            if (!packs.TryGetValue(packId, out var packInfo))
            {
                return null;
            }

            if (packInfo.IsAlias)
            {
                //packInfo.AliasTo.TryGetValue ()
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Recommends a set of workloads should be installed on top of the existing installed workloads to provide the specified missing packs
        /// </summary>
        /// <remarks>
        /// Used by the MSBuild workload resolver to emit actionable errors
        /// </remarks>
        public IList<WorkloadInfo> GetWorkloadSuggestionForMissingPacks(IList<string> packId)
        {
            throw new NotImplementedException();
        }

        public class PackInfo
        {
            public PackInfo(string id, string version, WorkloadPackKind kind, string path)
            {
                Id = id;
                Version = version;
                Kind = kind;
                Path = path;
            }

            public string Id { get; }

            public string Version { get; }

            public WorkloadPackKind Kind { get; }

            /// <summary>
            /// Path to the pack. If it's a template or library pack, <see cref="IsStillPacked"/> will be <code>true</code> and this will be a path to the <code>nupkg</code>,
            /// else <see cref="IsStillPacked"/> will be <code>false</code> and this will be a path to the directory into which it has been unpacked.
            /// </summary>
            public string Path { get; }

            /// <summary>
            /// Whether the pack pointed to by the path is still in a packed form.
            /// </summary>
            public bool IsStillPacked => Kind switch
            {
                WorkloadPackKind.Library => false,
                WorkloadPackKind.Template => false,
                _ => true
            };
        }

        public class WorkloadInfo
        {
            public WorkloadInfo(string id, string description)
            {
                Id = id;
                Description = description;
            }

            public string Id { get; }
            public string Description { get; }
        }
    }
}
