using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

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
        readonly string[] platformIds;
        readonly string dotNetRootPath;

        public WorkloadResolver(IWorkloadManifestProvider manifestProvider, string dotNetRootPath)
        {
            this.dotNetRootPath = dotNetRootPath;

            // eventually we may want a series of falbacks here, as rids have in general
            // but for now, keep it simple
            var platformId = GetHostPlatformId();
            if (platformId != null)
            {
                platformIds = new[] { platformId, "*" };
            }
            else
            {
                platformIds = new[] { "*" };
            }

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
                    workloads.Add(workload.Key, workload.Value);
                }
                foreach (var pack in manifest.Packs)
                {
                    packs.Add(pack.Key, pack.Value);
                }
            }
        }


        // rather that forcing all consumers to depend on and parse the RID catalog, or doing that here, for now just bake in a small
        // subset of dev host platform rids for now for the workloads that are likely to need this functionality soonest
        string? GetHostPlatformId()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.OSArchitecture switch
                {
                    Architecture.X64 => "osx-x64",
                    Architecture.Arm64 => "osx-arm64",
                    _ => null
                };
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64)
                {
                    return "windows-x64";
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the installed workload packs of a particular kind
        /// </summary>
        /// <remarks>
        /// Used by MSBuild resolver to scan SDK packs for AutoImport.props files to be imported.
        /// Used by template engine to find templates to be added to hive.
        /// </remarks>
        public IEnumerable<PackInfo> GetInstalledWorkloadPacksOfKind(WorkloadPackKind kind)
        {
            foreach (var pack in packs)
            {
                if (pack.Value.Kind != kind)
                {
                    continue;
                }

                var aliasedId = pack.Value.TryGetAliasForPlatformIds(platformIds) ?? pack.Value.Id;
                var packPath = GetPackPath(dotNetRootPath, aliasedId, pack.Value.Version, pack.Value.Kind);

                if (PackExists(packPath, kind))
                {
                    yield return new PackInfo(
                        pack.Value.Id,
                        pack.Value.Version,
                        pack.Value.Kind,
                        packPath
                    );
                }
            }
        }

        Func<string, bool>? fileExistOverride;
        Func<string, bool>? directoryExistOverride;
        internal void ReplaceFilesystemChecksForTest (Func<string,bool> fileExists, Func<string, bool> directoryExists)
        {
            fileExistOverride = fileExists;
            directoryExistOverride = directoryExists;
        }

        bool PackExists (string packPath, WorkloadPackKind kind)
        {
            switch (kind)
            {
                case WorkloadPackKind.Framework:
                case WorkloadPackKind.Sdk:
                case WorkloadPackKind.Tool:
                    //can we do a more robust check than directory.exists?
                    return directoryExistOverride?.Invoke(packPath) ?? Directory.Exists(packPath);
                case WorkloadPackKind.Library:
                case WorkloadPackKind.Template:
                    return fileExistOverride?.Invoke(packPath) ?? File.Exists(packPath);
                default:
                    throw new ArgumentException($"The package kind '{kind}' is not known", nameof(kind));
            }
        }

        static string GetPackPath (string dotNetRootPath, string packageId, string packageVersion, WorkloadPackKind kind)
        {
            switch (kind)
            {
                case WorkloadPackKind.Framework:
                case WorkloadPackKind.Sdk:
                    return Path.Combine(dotNetRootPath, "packs", packageId, packageVersion);
                case WorkloadPackKind.Template:
                    return Path.Combine(dotNetRootPath, "template-packs", packageId.ToLowerInvariant() + "." + packageVersion.ToLowerInvariant() + ".nupkg");
                case WorkloadPackKind.Library:
                    return Path.Combine(dotNetRootPath, "library-packs", packageId.ToLowerInvariant() + "." + packageVersion.ToLowerInvariant() + ".nupkg");
                case WorkloadPackKind.Tool:
                    return Path.Combine(dotNetRootPath, "tool-packs", packageId, packageVersion);
                default:
                    throw new ArgumentException($"The package kind '{kind}' is not known", nameof(kind));
            }
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
            if (packs.TryGetValue(packId, out var packInfo))
            {
                return packInfo.Version;
            }

            return null;
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
