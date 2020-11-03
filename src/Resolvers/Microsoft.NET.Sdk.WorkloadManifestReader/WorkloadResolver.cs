// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <remarks>
    /// This very specifically exposes only the functionality needed right now by the MSBuild workload resolver
    /// and by the template engine. More general APIs will be added later.
    /// </remarks>
    public class WorkloadResolver : IWorkloadResolver
    {
        private readonly Dictionary<WorkloadDefinitionId, WorkloadDefinition> _workloads = new Dictionary<WorkloadDefinitionId, WorkloadDefinition>();
        private readonly Dictionary<WorkloadPackId, WorkloadPack> _packs = new Dictionary<WorkloadPackId, WorkloadPack>();
        private string[] _currentRuntimeIdentifiers;
        private readonly string _dotnetRootPath;

        private Func<string, bool>? _fileExistOverride;
        private Func<string, bool>? _directoryExistOverride;

        public static WorkloadResolver Create(IWorkloadManifestProvider manifestProvider, string dotnetRootPath, string sdkVersion)
        {
            string runtimeIdentifierChainPath = Path.Combine(dotnetRootPath, "sdk", sdkVersion, "NETCoreSdkRuntimeIdentifierChain.txt");
            string[] currentRuntimeIdentifiers = File.Exists(runtimeIdentifierChainPath) ?
                File.ReadAllLines(runtimeIdentifierChainPath).Where(l => !string.IsNullOrEmpty(l)).ToArray() :
                new string[] { };

            return new WorkloadResolver(manifestProvider, dotnetRootPath, currentRuntimeIdentifiers);
        }

        public static WorkloadResolver CreateForTests(IWorkloadManifestProvider manifestProvider, string dotnetRootPath, string[] currentRuntimeIdentifiers)
        {
            return new WorkloadResolver(manifestProvider, dotnetRootPath, currentRuntimeIdentifiers);
        }

        private WorkloadResolver(IWorkloadManifestProvider manifestProvider, string dotnetRootPath, string [] currentRuntimeIdentifiers)
        {
            this._dotnetRootPath = dotnetRootPath;

            _currentRuntimeIdentifiers = currentRuntimeIdentifiers;

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
                    _workloads.Add(workload.Key, workload.Value);
                }
                foreach (var pack in manifest.Packs)
                {
                    _packs.Add(pack.Key, pack.Value);
                }
            }
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
            foreach (var pack in _packs)
            {
                if (pack.Value.Kind != kind)
                {
                    continue;
                }

                var aliasedPath = GetAliasedPackPath(pack.Value);
                if (PackExists(aliasedPath, pack.Value.Kind))
                {
                    yield return CreatePackInfo(pack.Value, aliasedPath);
                }
            }
        }

        internal void ReplaceFilesystemChecksForTest(Func<string, bool> fileExists, Func<string, bool> directoryExists)
        {
            _fileExistOverride = fileExists;
            _directoryExistOverride = directoryExists;
        }

        private PackInfo CreatePackInfo(WorkloadPack pack, string aliasedPath) => new PackInfo(
                pack.Id.ToString(),
                pack.Version,
                pack.Kind,
                aliasedPath
            );

        private bool PackExists (string packPath, WorkloadPackKind packKind)
        {
            switch (packKind)
            {
                case WorkloadPackKind.Framework:
                case WorkloadPackKind.Sdk:
                case WorkloadPackKind.Tool:
                    //can we do a more robust check than directory.exists?
                    return _directoryExistOverride?.Invoke(packPath) ?? Directory.Exists(packPath);
                case WorkloadPackKind.Library:
                case WorkloadPackKind.Template:
                    return _fileExistOverride?.Invoke(packPath) ?? File.Exists(packPath);
                default:
                    throw new ArgumentException($"The package kind '{packKind}' is not known", nameof(packKind));
            }
        }

        private string GetAliasedPackPath(WorkloadPack pack)
        {
            var aliasedId = pack.TryGetAliasForRuntimeIdentifiers(_currentRuntimeIdentifiers) ?? pack.Id;
            return GetPackPath(_dotnetRootPath, aliasedId, pack.Version, pack.Kind);
        }

        private static string GetPackPath (string dotnetRootPath, WorkloadPackId packageId, string packageVersion, WorkloadPackKind kind)
        {
            switch (kind)
            {
                case WorkloadPackKind.Framework:
                case WorkloadPackKind.Sdk:
                    return Path.Combine(dotnetRootPath, "packs", packageId.ToString(), packageVersion);
                case WorkloadPackKind.Template:
                    return Path.Combine(dotnetRootPath, "template-packs", packageId.GetNuGetCanonicalId() + "." + packageVersion.ToLowerInvariant() + ".nupkg");
                case WorkloadPackKind.Library:
                    return Path.Combine(dotnetRootPath, "library-packs", packageId.GetNuGetCanonicalId() + "." + packageVersion.ToLowerInvariant() + ".nupkg");
                case WorkloadPackKind.Tool:
                    return Path.Combine(dotnetRootPath, "tool-packs", packageId.ToString(), packageVersion);
                default:
                    throw new ArgumentException($"The package kind '{kind}' is not known", nameof(kind));
            }
        }

        /// <summary>
        /// Gets the IDs of all the packs that are installed
        /// </summary>
        private HashSet<WorkloadPackId> GetInstalledPacks()
        {
            var installedPacks = new HashSet<WorkloadPackId>();
            foreach (var pack in _packs)
            {
                var packPath = GetAliasedPackPath(pack.Value);

                if (PackExists(packPath, pack.Value.Kind))
                {
                    installedPacks.Add(pack.Key);
                }
            }
            return installedPacks;
        }

        public IEnumerable<string> GetPacksInWorkload(string workloadId)
        {
            if (string.IsNullOrEmpty(workloadId))
            {
                throw new ArgumentException($"'{nameof(workloadId)}' cannot be null or empty", nameof(workloadId));
            }

            var id = new WorkloadDefinitionId(workloadId);

            if (!_workloads.TryGetValue(id, out var workload))
            {
                throw new Exception("Workload not found");
            }

            if (workload.Extends?.Count > 0)
            {
                return GetPacksInWorkload(workload).Select (p => p.ToString());
            }

#nullable disable
            return workload.Packs.Select(p => p.ToString()) ?? Enumerable.Empty<string>();
#nullable restore
        }

        internal IEnumerable<WorkloadPackId> GetPacksInWorkload(WorkloadDefinition workload)
        {
            var dedup = new HashSet<WorkloadDefinitionId>();

            IEnumerable<WorkloadPackId> ExpandPacks (WorkloadDefinitionId workloadId)
            {
                if (!_workloads.TryGetValue (workloadId, out var workloadInfo))
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
        public PackInfo? TryGetPackInfo(string packId)
        {
            if (string.IsNullOrWhiteSpace(packId))
            {
                throw new ArgumentException($"'{nameof(packId)}' cannot be null or whitespace", nameof(packId));
            }

            if (_packs.TryGetValue(new WorkloadPackId (packId), out var pack))
            {
                return CreatePackInfo(pack, GetAliasedPackPath(pack));
            }

            return null;
        }

        /// <summary>
        /// Recommends a set of workloads should be installed on top of the existing installed workloads to provide the specified missing packs
        /// </summary>
        /// <remarks>
        /// Used by the MSBuild workload resolver to emit actionable errors
        /// </remarks>
        public ISet<WorkloadInfo> GetWorkloadSuggestionForMissingPacks(IList<string> packIds)
        {
            var requestedPacks = new HashSet<WorkloadPackId>(packIds.Select(p => new WorkloadPackId(p)));
            var expandedWorkloads = _workloads.Select(w => (w.Key, new HashSet<WorkloadPackId>(GetPacksInWorkload(w.Value))));
            var finder = new WorkloadSuggestionFinder(GetInstalledPacks(), requestedPacks, expandedWorkloads);

            return new HashSet<WorkloadInfo>
            (
                finder.GetBestSuggestion().Workloads.Select(s => new WorkloadInfo(s.ToString(), _workloads[s].Description))
            );
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
            public WorkloadInfo(string id, string? description)
            {
                Id = id;
                Description = description;
            }

            public string Id { get; }
            public string? Description { get; }
        }
    }
}
