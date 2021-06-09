// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.NET.Sdk.Localization;
using FXVersion = Microsoft.DotNet.MSBuildSdkResolver.FXVersion;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <remarks>
    /// This very specifically exposes only the functionality needed right now by the MSBuild workload resolver
    /// and by the template engine. More general APIs will be added later.
    /// </remarks>
    public class WorkloadResolver : IWorkloadResolver
    {
        private readonly Dictionary<WorkloadId, WorkloadDefinition> _workloads = new Dictionary<WorkloadId, WorkloadDefinition>();
        private readonly Dictionary<WorkloadPackId, WorkloadPack> _packs = new Dictionary<WorkloadPackId, WorkloadPack>();
        private readonly IWorkloadManifestProvider _manifestProvider;
        private string[] _currentRuntimeIdentifiers;
        private readonly string [] _dotnetRootPaths;

        private Func<string, bool>? _fileExistOverride;
        private Func<string, bool>? _directoryExistOverride;

        public static WorkloadResolver Create(IWorkloadManifestProvider manifestProvider, string dotnetRootPath, string sdkVersion)
        {
            string runtimeIdentifierChainPath = Path.Combine(dotnetRootPath, "sdk", sdkVersion, "NETCoreSdkRuntimeIdentifierChain.txt");
            string[] currentRuntimeIdentifiers = File.Exists(runtimeIdentifierChainPath) ?
                File.ReadAllLines(runtimeIdentifierChainPath).Where(l => !string.IsNullOrEmpty(l)).ToArray() :
                new string[] { };

            var packRootEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNETSDK_WORKLOAD_PACK_ROOTS");

            string[] dotnetRootPaths;
            if (!string.IsNullOrEmpty(packRootEnvironmentVariable))
            {
                dotnetRootPaths = packRootEnvironmentVariable.Split(Path.PathSeparator).Append(dotnetRootPath).ToArray();
            }
            else
            {
                dotnetRootPaths = new[] { dotnetRootPath };
            }

            return new WorkloadResolver(manifestProvider, dotnetRootPaths, currentRuntimeIdentifiers);
        }

        public static WorkloadResolver CreateForTests(IWorkloadManifestProvider manifestProvider, string[] dotNetRootPaths, string[]? currentRuntimeIdentifiers = null)
        {
            if (currentRuntimeIdentifiers == null)
            {
                currentRuntimeIdentifiers = new[] { "win-x64", "win", "any", "base" };
            }
            return new WorkloadResolver(manifestProvider, dotNetRootPaths, currentRuntimeIdentifiers);
        }

        public WorkloadResolver CreateTempDirResolver(IWorkloadManifestProvider manifestProvider, string dotnetRootPath, string sdkVersion)
        {
            var packRootEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNETSDK_WORKLOAD_PACK_ROOTS");
            string[] dotnetRootPaths;
            if (!string.IsNullOrEmpty(packRootEnvironmentVariable))
            {
                dotnetRootPaths = packRootEnvironmentVariable.Split(Path.PathSeparator).Append(dotnetRootPath).ToArray();
            }
            else
            {
                dotnetRootPaths = new[] { dotnetRootPath };
            }

            return new WorkloadResolver(manifestProvider, dotnetRootPaths, _currentRuntimeIdentifiers);
        }

        private WorkloadResolver(IWorkloadManifestProvider manifestProvider, string [] dotnetRootPaths, string [] currentRuntimeIdentifiers)
        {
            _dotnetRootPaths = dotnetRootPaths;
            _currentRuntimeIdentifiers = currentRuntimeIdentifiers;
            _manifestProvider = manifestProvider;

            RefreshWorkloadManifests();
        }

        public void RefreshWorkloadManifests()
        {
            _workloads.Clear();
            _packs.Clear();

            var manifests = new Dictionary<string,WorkloadManifest>(StringComparer.OrdinalIgnoreCase);

            foreach ((string manifestId, Stream manifestStream) in _manifestProvider.GetManifests())

            {
                using (manifestStream)
                {
                    var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId, manifestStream);
                    if (manifests.ContainsKey(manifestId))
                    {
                        throw new Exception($"Duplicate workload manifest {manifestId}");
                    }
                    manifests.Add(manifestId, manifest);
                }
            }

            foreach (var manifest in manifests.Values)
            {
                if (manifest.DependsOnManifests != null)
                {
                    foreach (var dependency in manifest.DependsOnManifests)
                    {
                        if (manifests.TryGetValue(dependency.Key, out var resolvedDependency))
                        {
                            if (FXVersion.Compare(dependency.Value, resolvedDependency.ParsedVersion) > 0)
                            {
                                throw new Exception($"Inconsistency in workload manifest '{manifest.Id}': requires '{dependency.Key}' version at least {dependency.Value} but found {resolvedDependency.Version}");
                            }
                        }
                        else
                        {
                            throw new Exception($"Inconsistency in workload manifest '{manifest.Id}': missing dependency '{dependency.Key}'");
                        }
                    }
                }

                HashSet<WorkloadRedirect>? redirects = null;
                foreach (var workload in manifest.Workloads)
                {
                    if (workload.Value is WorkloadRedirect redirect)
                    {
                        (redirects ?? (redirects = new HashSet<WorkloadRedirect>())).Add(redirect);
                    }
                    else
                    {
                        _workloads.Add(workload.Key, (WorkloadDefinition)workload.Value);
                    }
                }

                // resolve redirects upfront so they are transparent to the rest of the code
                // the _workloads dictionary maps redirected ids directly to the replacement
                if (redirects != null)
                {
                    // handle multi-levels redirects via multiple resolve passes, bottom-up
                    while (redirects.RemoveWhere(redirect =>
                    {
                        if (_workloads.TryGetValue(redirect.ReplaceWith, out var replacement))
                        {
                            _workloads.Add(redirect.Id, replacement);
                            return true;
                        }
                        return false;
                    }) > 0) { };

                    if (redirects.Count > 0)
                    {
                        throw new WorkloadManifestCompositionException(Strings.UnresolvedWorkloadRedirects, string.Join("\", \"", redirects.Select(r => r.Id.ToString())));
                    }
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
            foreach (var pack in _packs.Values)
            {
                if (pack.Kind != kind)
                {
                    continue;
                }

                if (ResolveId(pack) is WorkloadPackId resolvedPackageId)
                {
                    var aliasedPath = GetPackPath(_dotnetRootPaths, resolvedPackageId, pack.Version, pack.Kind, out bool isInstalled);
                    if (isInstalled)
                    {
                        yield return CreatePackInfo(pack, aliasedPath, resolvedPackageId);
                    }
                }
            }
        }

        internal void ReplaceFilesystemChecksForTest(Func<string, bool> fileExists, Func<string, bool> directoryExists)
        {
            _fileExistOverride = fileExists;
            _directoryExistOverride = directoryExists;
        }

        private PackInfo CreatePackInfo(WorkloadPack pack, string aliasedPath, WorkloadPackId resolvedPackageId) => new PackInfo(
                pack.Id.ToString(),
                pack.Version,
                pack.Kind,
                aliasedPath,
                resolvedPackageId.ToString()
            );

        /// <summary>
        /// Resolve the package ID for the host platform.
        /// </summary>
        /// <param name="pack">The workload pack</param>
        /// <returns>The path to the pack, or null if the pack is not available on the host platform.</returns>
        private WorkloadPackId? ResolveId(WorkloadPack pack)
        {
            if (!pack.IsAlias)
            {
                return pack.Id;
            }

            if (pack.TryGetAliasForRuntimeIdentifiers(_currentRuntimeIdentifiers) is WorkloadPackId aliasedId)
            {
                return aliasedId;
            }

            return null;
        }

        /// <summary>
        /// Resolve the pack path for the host platform.
        /// </summary>
        /// <param name="pack">The workload pack</param>
        /// <param name="isInstalled">Whether the pack is installed</param>
        /// <returns>The path to the pack, or null if the pack is not available on the host platform.</returns>
        private string? ResolvePackPath(WorkloadPack pack, out bool isInstalled)
        {
            if (ResolveId(pack) is WorkloadPackId resolvedId)
            {
                return GetPackPath(_dotnetRootPaths, resolvedId, pack.Version, pack.Kind, out isInstalled);
            }

            isInstalled = false;
            return null;
        }

        private string GetPackPath(string [] dotnetRootPaths, WorkloadPackId packageId, string packageVersion, WorkloadPackKind kind, out bool isInstalled)
        {
            isInstalled = false;
            string packPath = "";
            bool isFile;
            foreach (var rootPath in dotnetRootPaths)
            {
                switch (kind)
                {
                    case WorkloadPackKind.Framework:
                    case WorkloadPackKind.Sdk:
                        packPath = Path.Combine(rootPath, "packs", packageId.ToString(), packageVersion);
                        isFile = false;
                        break;
                    case WorkloadPackKind.Template:
                        packPath = Path.Combine(rootPath, "template-packs", packageId.GetNuGetCanonicalId() + "." + packageVersion.ToLowerInvariant() + ".nupkg");
                        isFile = true;
                        break;
                    case WorkloadPackKind.Library:
                        packPath = Path.Combine(rootPath, "library-packs", packageId.GetNuGetCanonicalId() + "." + packageVersion.ToLowerInvariant() + ".nupkg");
                        isFile = true;
                        break;
                    case WorkloadPackKind.Tool:
                        packPath = Path.Combine(rootPath, "tool-packs", packageId.ToString(), packageVersion);
                        isFile = false;
                        break;
                    default:
                        throw new ArgumentException($"The package kind '{kind}' is not known", nameof(kind));
                }

                //can we do a more robust check than directory.exists?
                isInstalled = isFile ?
                    _fileExistOverride?.Invoke(packPath) ?? File.Exists(packPath) :
                    _directoryExistOverride?.Invoke(packPath) ?? Directory.Exists(packPath); ;

                if (isInstalled)
                {
                    break;
                }
            }
            return packPath;
        }

        /// <summary>
        /// Gets the IDs of all the packs that are installed
        /// </summary>
        private HashSet<WorkloadPackId> GetInstalledPacks()
        {
            var installedPacks = new HashSet<WorkloadPackId>();
            foreach (var pack in _packs)
            {
                ResolvePackPath(pack.Value, out bool isInstalled);
                if (isInstalled)
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

            var id = new WorkloadId(workloadId);

            if (!_workloads.TryGetValue(id, out var workload))
            {
                throw new Exception($"Workload not found: {id}. Known workloads: {string.Join(" ", _workloads.Select(workload => workload.Key.ToString()))}");
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
            var dedup = new HashSet<WorkloadId>();

            IEnumerable<WorkloadPackId> ExpandPacks (WorkloadId workloadId)
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
                if (ResolveId(pack) is WorkloadPackId resolvedPackageId)
                {
                    var aliasedPath = GetPackPath(_dotnetRootPaths, resolvedPackageId, pack.Version, pack.Kind, out bool exists);
                    return CreatePackInfo(pack, aliasedPath, resolvedPackageId);
                }
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

        /// <summary>
        /// Returns the list of workloads defined by the manifests on disk
        /// </summary>
        public IEnumerable<WorkloadDefinition> GetAvaliableWorkloads()
        {
            return _workloads.Values;
        }

        public class PackInfo
        {
            public PackInfo(string id, string version, WorkloadPackKind kind, string path, string resolvedPackageId)
            {
                Id = id;
                Version = version;
                Kind = kind;
                Path = path;
                ResolvedPackageId = resolvedPackageId;
            }

            /// <summary>
            /// The workload pack ID. The NuGet package ID <see cref="ResolvedPackageId"/> may differ from this.
            /// </summary>
            public string Id { get; }

            public string Version { get; }

            public WorkloadPackKind Kind { get; }

            public string ResolvedPackageId { get; }

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

        public WorkloadInfo GetWorkloadInfo(WorkloadId WorkloadId)
        {
            if (!_workloads.TryGetValue(WorkloadId, out var workload))
            {
                throw new Exception("Workload not found");
            }

            return new WorkloadInfo(workload.Id.ToString(), workload.Description);
        }

        public bool IsWorkloadPlatformCompatible(WorkloadId workloadId)
        {
            var workloadDef = GetAvaliableWorkloads().FirstOrDefault(workload => workload.Id.ToString().Equals(workloadId.ToString()));
            if (workloadDef == null)
            {
                throw new Exception("Workload not found");
            }
            if (workloadDef.Platforms == null)
            {
                return true;
            }
            return workloadDef.Platforms.Any(supportedPlatform => _currentRuntimeIdentifiers.Contains(supportedPlatform));
        }

        public string GetManifestVersion(string manifestId)
        {
            (_, Stream manifestStream) = _manifestProvider.GetManifests().FirstOrDefault(manifest => manifest.manifestId.Contains(manifestId));

            if (manifestStream == null)
            {
                throw new Exception($"Manifest with id {manifestId} does not exist.");
            }

            using (manifestStream)
            {
                var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId, manifestStream);
                return manifest.Version;
            }
        }

        public IDictionary<string, string> GetInstalledManifests()
        {
            var manifests = new Dictionary<string, string>();
            foreach ((string manifestId, Stream manifestStream) in _manifestProvider.GetManifests())
            {
                using (manifestStream)
                {
                    var manifest = WorkloadManifestReader.ReadWorkloadManifest(manifestId, manifestStream);
                    manifests.Add(manifestId, manifest.Version);
                }
            }
            return manifests;
        }
    }
}
