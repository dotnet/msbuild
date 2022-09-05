// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Workloads.Workload;
using Microsoft.NET.Sdk.Localization;
using FXVersion = Microsoft.DotNet.MSBuildSdkResolver.FXVersion;
#if USE_SYSTEM_TEXT_JSON
using System.Text.Json.Serialization;
#endif

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    /// <remarks>
    /// This very specifically exposes only the functionality needed right now by the MSBuild workload resolver
    /// and by the template engine. More general APIs will be added later.
    /// </remarks>
    public class WorkloadResolver : IWorkloadResolver
    {
        private readonly Dictionary<string, WorkloadManifest> _manifests = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<WorkloadId, (WorkloadDefinition workload, WorkloadManifest manifest)> _workloads = new();
        private readonly Dictionary<WorkloadPackId, (WorkloadPack pack, WorkloadManifest manifest)> _packs = new();
        private IWorkloadManifestProvider? _manifestProvider;
        private string[] _currentRuntimeIdentifiers;
        private readonly (string path, bool installable)[] _dotnetRootPaths;

        private Func<string, bool>? _fileExistOverride;
        private Func<string, bool>? _directoryExistOverride;

        public static WorkloadResolver Create(IWorkloadManifestProvider manifestProvider, string dotnetRootPath, string sdkVersion, string? userProfileDir)
        {
            string runtimeIdentifierChainPath = Path.Combine(dotnetRootPath, "sdk", sdkVersion, "NETCoreSdkRuntimeIdentifierChain.txt");
            string[] currentRuntimeIdentifiers = File.Exists(runtimeIdentifierChainPath) ?
                File.ReadAllLines(runtimeIdentifierChainPath).Where(l => !string.IsNullOrEmpty(l)).ToArray() :
                new string[] { };

            (string path, bool installable)[] workloadRootPaths;
            if (userProfileDir != null && WorkloadFileBasedInstall.IsUserLocal(dotnetRootPath, sdkVersion) && Directory.Exists(userProfileDir))
            {
                workloadRootPaths = new[] { (userProfileDir, true), (dotnetRootPath, true) };
            }
            else
            {
                workloadRootPaths = new[] { (dotnetRootPath, true) };
            }

            var packRootEnvironmentVariable = Environment.GetEnvironmentVariable(EnvironmentVariableNames.WORKLOAD_PACK_ROOTS);
            if (!string.IsNullOrEmpty(packRootEnvironmentVariable))
            {
                workloadRootPaths = packRootEnvironmentVariable.Split(Path.PathSeparator).Select(path => (path, false)).Concat(workloadRootPaths).ToArray();
            }

            return new WorkloadResolver(manifestProvider, workloadRootPaths, currentRuntimeIdentifiers);
        }

        public static WorkloadResolver CreateForTests(IWorkloadManifestProvider manifestProvider, string dotNetRoot, bool userLocal = false, string? userProfileDir = null, string[]? currentRuntimeIdentifiers = null)
        {
            if (userLocal && userProfileDir is null)
            {
                throw new ArgumentNullException(nameof(userProfileDir));
            }
            (string path, bool installable)[] dotNetRootPaths = userLocal
                                                                ? new[] { (userProfileDir!, true), (dotNetRoot, true) }
                                                                : new[] { (dotNetRoot, true) };
            return CreateForTests(manifestProvider, dotNetRootPaths, currentRuntimeIdentifiers);
        }

        public static WorkloadResolver CreateForTests(IWorkloadManifestProvider manifestProvider, (string path, bool installable)[] dotNetRootPaths, string[]? currentRuntimeIdentifiers = null)
        {
            if (currentRuntimeIdentifiers == null)
            {
                currentRuntimeIdentifiers = new[] { "win-x64", "win", "any", "base" };
            }
            return new WorkloadResolver(manifestProvider, dotNetRootPaths, currentRuntimeIdentifiers);
        }

        /// <summary>
        /// Creates a resolver by composing all the manifests from the provider.
        /// </summary>
        private WorkloadResolver(IWorkloadManifestProvider manifestProvider, (string path, bool installable)[] dotnetRootPaths, string[] currentRuntimeIdentifiers)
            : this(dotnetRootPaths, currentRuntimeIdentifiers)
        {
            _manifestProvider = manifestProvider;

            LoadManifestsFromProvider(manifestProvider);
            ComposeWorkloadManifests();
        }

        /// <summary>
        /// Creates a resolver with no manifests.
        /// </summary>A
        private WorkloadResolver((string path, bool installable)[] dotnetRootPaths, string[] currentRuntimeIdentifiers)
        {
            _dotnetRootPaths = dotnetRootPaths;
            _currentRuntimeIdentifiers = currentRuntimeIdentifiers;
        }

        public void RefreshWorkloadManifests()
        {
            if (_manifestProvider == null)
            {
                throw new InvalidOperationException("Resolver was created without provider and cannot be refreshed");
            }
            _manifests.Clear();
            LoadManifestsFromProvider(_manifestProvider);
            ComposeWorkloadManifests();
        }

        private void LoadManifestsFromProvider(IWorkloadManifestProvider manifestProvider)
        {
            foreach (var readableManifest in manifestProvider.GetManifests())
            {
                using (Stream manifestStream = readableManifest.OpenManifestStream())
                using (Stream? localizationStream = readableManifest.OpenLocalizationStream())
                {
                    var manifest = WorkloadManifestReader.ReadWorkloadManifest(readableManifest.ManifestId, manifestStream, localizationStream, readableManifest.ManifestPath);
                    if (!_manifests.TryAdd(readableManifest.ManifestId, manifest))
                    {
                        var existingManifest = _manifests[readableManifest.ManifestId];
                        throw new WorkloadManifestCompositionException(Strings.DuplicateManifestID, manifestProvider.GetType().FullName, readableManifest.ManifestId, readableManifest.ManifestPath, existingManifest.ManifestPath);
                    }
                }
            }
        }

        private void ComposeWorkloadManifests()
        {
            _workloads.Clear();
            _packs.Clear();

            Dictionary<WorkloadId, (WorkloadRedirect redirect, WorkloadManifest manifest)>? redirects = null;

            foreach (var manifest in _manifests.Values)
            {
                if (manifest.DependsOnManifests != null)
                {
                    foreach (var dependency in manifest.DependsOnManifests)
                    {
                        if (_manifests.TryGetValue(dependency.Key, out var resolvedDependency))
                        {
                            if (FXVersion.Compare(dependency.Value, resolvedDependency.ParsedVersion) > 0)
                            {
                                throw new WorkloadManifestCompositionException(Strings.ManifestDependencyVersionTooLow, dependency.Key, resolvedDependency.Version, dependency.Value, manifest.Id, manifest.ManifestPath);
                            }
                        }
                        else
                        {
                            throw new WorkloadManifestCompositionException(Strings.ManifestDependencyMissing, dependency.Key, manifest.Id, manifest.ManifestPath);
                    }
                    }
                }

                foreach (var workload in manifest.Workloads)
                {
                    if (workload.Value is WorkloadRedirect redirect)
                    {
                        (redirects ??= new()).Add(redirect.Id, (redirect, manifest));
                    }
                    else
                    {
                        if (!_workloads.TryAdd(workload.Key, ((WorkloadDefinition)workload.Value, manifest)))
                        {
                            WorkloadManifest conflictingManifest = _workloads[workload.Key].manifest;
                            throw new WorkloadManifestCompositionException(Strings.ConflictingWorkloadDefinition, workload.Key, manifest.Id, manifest.ManifestPath, conflictingManifest.Id, conflictingManifest.ManifestPath);
                        }
                    }
                }

                foreach (var pack in manifest.Packs)
                {
                    if (!_packs.TryAdd(pack.Key, (pack.Value, manifest)))
                    {
                        WorkloadManifest conflictingManifest = _packs[pack.Key].manifest;
                        throw new WorkloadManifestCompositionException(Strings.ConflictingWorkloadPack, pack.Key, manifest.Id, manifest.ManifestPath, conflictingManifest.Id, conflictingManifest.ManifestPath);
                    }
                }
            }

            // resolve redirects upfront so they are transparent to the rest of the code
            // the _workloads dictionary maps redirected ids directly to the replacement
            if (redirects != null)
            {
                // handle multi-levels redirects via multiple resolve passes, bottom-up i.e. iteratively try
                // to resolve unresolved redirects to resolved workloads/redirects until we stop making progress
                var unresolvedRedirects = new HashSet<WorkloadId>(redirects.Keys);
                while (unresolvedRedirects.RemoveWhere(redirectId =>
                {
                    (var redirect, var manifest) = redirects[redirectId];

                    if (_workloads.TryGetValue(redirect.ReplaceWith, out var replacement))
                    {
                        if (!_workloads.TryAdd(redirect.Id, replacement))
                        {
                            WorkloadManifest conflictingManifest = _workloads[redirect.Id].manifest;
                            throw new WorkloadManifestCompositionException(Strings.ConflictingWorkloadDefinition, redirect.Id, manifest.Id, manifest.ManifestPath, conflictingManifest.Id, conflictingManifest.ManifestPath);
                        }
                        return true;
                    }
                    return false;
                }) > 0) { };

                if (unresolvedRedirects.Count > 0)
                {
                    // if one or more of them doesn't resolve into another redirect, it's an actual unresolved redirect
                    var unresolved = unresolvedRedirects.Select(ur => redirects[ur]).Where(ur => !redirects.ContainsKey(ur.redirect.ReplaceWith)).FirstOrDefault();
                    if (unresolved is (WorkloadRedirect redirect, WorkloadManifest manifest))
                    {
                        throw new WorkloadManifestCompositionException(Strings.UnresolvedWorkloadRedirect, redirect.ReplaceWith, redirect.Id, manifest.Id, manifest.ManifestPath);
                    }
                    else
                    {
                        var cyclic = redirects[unresolvedRedirects.First()];
                        throw new WorkloadManifestCompositionException(Strings.CyclicWorkloadRedirect, cyclic.redirect.Id, cyclic.manifest.Id, cyclic.manifest.ManifestPath);
                    }
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
            foreach ((var pack, _) in _packs.Values)
            {
                if (pack.Kind != kind)
                {
                    continue;
                }

                if (ResolvePackPath(pack, out WorkloadPackId resolvedPackageId, out bool isInstalled) is string aliasedPath && isInstalled)
                {
                    yield return CreatePackInfo(pack, aliasedPath, resolvedPackageId);
                }
            }
        }

        internal void ReplaceFilesystemChecksForTest(Func<string, bool> fileExists, Func<string, bool> directoryExists)
        {
            _fileExistOverride = fileExists;
            _directoryExistOverride = directoryExists;
        }

        private PackInfo CreatePackInfo(WorkloadPack pack, string aliasedPath, WorkloadPackId resolvedPackageId) => new PackInfo(
                pack.Id,
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
            => ResolvePackPath(pack, out _, out isInstalled);

        private string? ResolvePackPath(
            WorkloadPack pack,
            out WorkloadPackId resolvedId,
            out bool isInstalled)
        {
            if (ResolveId(pack) is WorkloadPackId resolved)
            {
                resolvedId = resolved;
                return GetPackPath(resolved, pack.Version, pack.Kind, out isInstalled);
            }

            resolvedId = default;
            isInstalled = false;
            return null;

            string GetPackPath(WorkloadPackId resolvedPackageId, string packageVersion, WorkloadPackKind kind, out bool isInstalled)
            {
                isInstalled = false;
                string? firstInstallablePackPath = null;
                string? installedPackPath = null;
                foreach (var rootPath in _dotnetRootPaths)
                {
                    string packPath;
                    bool isFile;
                    switch (kind)
                    {
                        case WorkloadPackKind.Framework:
                        case WorkloadPackKind.Sdk:
                            packPath = Path.Combine(rootPath.path, "packs", resolvedPackageId.ToString(), packageVersion);
                            isFile = false;
                            break;
                        case WorkloadPackKind.Template:
                            packPath = Path.Combine(rootPath.path, "template-packs", resolvedPackageId.GetNuGetCanonicalId() + "." + packageVersion.ToLowerInvariant() + ".nupkg");
                            isFile = true;
                            break;
                        case WorkloadPackKind.Library:
                            packPath = Path.Combine(rootPath.path, "library-packs", resolvedPackageId.GetNuGetCanonicalId() + "." + packageVersion.ToLowerInvariant() + ".nupkg");
                            isFile = true;
                            break;
                        case WorkloadPackKind.Tool:
                            packPath = Path.Combine(rootPath.path, "tool-packs", resolvedPackageId.ToString(), packageVersion);
                            isFile = false;
                            break;
                        default:
                            throw new ArgumentException($"The package kind '{kind}' is not known", nameof(kind));
                    }

                    if (rootPath.installable && firstInstallablePackPath is null)
                    {
                        firstInstallablePackPath = packPath;
                    }

                    //can we do a more robust check than directory.exists?
                    isInstalled = isFile ?
                        _fileExistOverride?.Invoke(packPath) ?? File.Exists(packPath) :
                        _directoryExistOverride?.Invoke(packPath) ?? Directory.Exists(packPath); ;

                    if (isInstalled)
                    {
                        installedPackPath = packPath;
                        break;
                    }
                }
                return installedPackPath ?? firstInstallablePackPath ?? "";
            }
        }

        /// <summary>
        /// Gets the IDs of all the packs that are installed
        /// </summary>
        private HashSet<WorkloadPackId> GetInstalledPacks()
        {
            var installedPacks = new HashSet<WorkloadPackId>();
            foreach ((WorkloadPackId id, (WorkloadPack pack, WorkloadManifest _)) in _packs)
            {
                ResolvePackPath(pack, out bool isInstalled);
                if (isInstalled)
                {
                    installedPacks.Add(id);
                }
            }
            return installedPacks;
        }

        public IEnumerable<WorkloadPackId> GetPacksInWorkload(WorkloadId workloadId)
        {
            if (string.IsNullOrEmpty(workloadId))
            {
                throw new ArgumentException($"'{nameof(workloadId)}' cannot be null or empty", nameof(workloadId));
            }

            if (!_workloads.TryGetValue(workloadId, out var value))
            {
                throw new Exception($"Workload not found: {workloadId}. Known workloads: {string.Join(" ", _workloads.Select(workload => workload.Key.ToString()))}");
            }
            var workload = value.workload;

            if (workload.Extends?.Count > 0)
            {
                return GetPacksInWorkload(workload, value.manifest).Select(p => p.packId);
            }

#nullable disable
            return workload.Packs ?? Enumerable.Empty<WorkloadPackId>();
#nullable restore
        }

        public IEnumerable<WorkloadInfo> GetExtendedWorkloads(IEnumerable<WorkloadId> workloadIds)
        {
            return EnumerateWorkloadWithExtends(new WorkloadId("root"), workloadIds, null)
                .Select(t => new WorkloadInfo(t.workload.Id, t.workload.Description));
        }

        private IEnumerable<(WorkloadDefinition workload, WorkloadManifest workloadManifest)> EnumerateWorkloadWithExtends(WorkloadDefinition workload, WorkloadManifest manifest)
        {
            IEnumerable<(WorkloadDefinition workload, WorkloadManifest workloadManifest)> result =
                workload.Extends == null
                    ? Enumerable.Empty<(WorkloadDefinition workload, WorkloadManifest workloadManifest)>()
                    : EnumerateWorkloadWithExtends(workload.Id, workload.Extends, manifest);

            return result.Prepend((workload, manifest));
        }

        private IEnumerable<(WorkloadDefinition workload, WorkloadManifest workloadManifest)> EnumerateWorkloadWithExtends(WorkloadId workloadId, IEnumerable<WorkloadId> extends, WorkloadManifest? manifest)
        {
            HashSet<WorkloadId>? dedup = null;

            IEnumerable<(WorkloadDefinition workload, WorkloadManifest workloadManifest)> EnumerateWorkloadWithExtendsRec(WorkloadId workloadId, IEnumerable<WorkloadId> extends, WorkloadManifest? manifest)
            {
                dedup ??= new HashSet<WorkloadId> { workloadId };

                foreach (var baseWorkloadId in extends)
                {
                    if (!dedup.Add(baseWorkloadId))
                    {
                        continue;
                    }

                    if (_workloads.TryGetValue(baseWorkloadId) is not (WorkloadDefinition baseWorkload, WorkloadManifest baseWorkloadManifest))
                    {
                        throw new WorkloadManifestCompositionException(Strings.MissingBaseWorkload, baseWorkloadId, workloadId, manifest?.Id, manifest?.ManifestPath);
                    }

                    // the workload's ID may not match the value we looked up if it's a redirect
                    if (baseWorkloadId != baseWorkload.Id && !dedup.Add(baseWorkload.Id))
                    {
                        continue;
                    }

                    yield return (baseWorkload, baseWorkloadManifest);

                    if (baseWorkload.Extends == null)
                    {
                        continue;
                    }

                    foreach (var enumeratedbaseWorkload in EnumerateWorkloadWithExtendsRec(baseWorkload.Id, baseWorkload.Extends, baseWorkloadManifest))
                    {
                        yield return enumeratedbaseWorkload;
                    }
                }
            }

            return EnumerateWorkloadWithExtendsRec(workloadId, extends, manifest);
        }

        internal IEnumerable<(WorkloadPackId packId, WorkloadDefinition referencingWorkload, WorkloadManifest workloadDefinedIn)> GetPacksInWorkload(WorkloadDefinition workload, WorkloadManifest manifest)
        {
            foreach((WorkloadDefinition w, WorkloadManifest m) in EnumerateWorkloadWithExtends(workload, manifest))
            {
                if (w.Packs != null && w.Packs.Count > 0)
                {
                    foreach (var p in w.Packs)
                    {
                        yield return (p, w, m);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the version of a workload pack for this resolver's SDK band
        /// </summary>
        /// <remarks>
        /// Used by the MSBuild SDK resolver to look up which versions of the SDK packs to import.
        /// </remarks>
        public PackInfo? TryGetPackInfo(WorkloadPackId packId)
        {
            if (string.IsNullOrEmpty(packId))
            {
                throw new ArgumentException($"'{nameof(packId)}' cannot be null or empty", nameof(packId));
            }

            if (_packs.TryGetValue(packId) is (WorkloadPack pack, _))
            {
                if (ResolvePackPath(pack, out WorkloadPackId resolvedPackageId, out bool isInstalled) is string aliasedPath)
                {
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
        public ISet<WorkloadInfo>? GetWorkloadSuggestionForMissingPacks(IList<WorkloadPackId> packIds, out ISet<WorkloadPackId> unsatisfiablePacks)
        {
            var requestedPacks = new HashSet<WorkloadPackId>(packIds);
            var availableWorkloads = GetAvailableWorkloadDefinitions();

            List<(WorkloadId Id, HashSet<WorkloadPackId> Packs)>? expandedWorkloads = availableWorkloads
                .Select(w => (w.workload.Id, new HashSet<WorkloadPackId>(GetPacksInWorkload(w.workload, w.manifest).Select(p => p.packId))))
                .ToList();

            var unsatisfiable = requestedPacks
                .Where(p => !expandedWorkloads.Any(w => w.Packs.Contains(p)))
                .ToHashSet();

            unsatisfiablePacks = unsatisfiable;

            requestedPacks.ExceptWith(unsatisfiable);
            if (requestedPacks.Count == 0)
            {
                return null;
            }

            expandedWorkloads = expandedWorkloads
                .Where(w => w.Packs.Any(p => requestedPacks.Contains(p)))
                .ToList();

            var finder = new WorkloadSuggestionFinder(GetInstalledPacks(), requestedPacks, expandedWorkloads);

            return finder.GetBestSuggestion()
                .Workloads
                .Select(s => new WorkloadInfo(s, _workloads[s].workload.Description))
                .ToHashSet();
        }

        /// <summary>
        /// Returns the list of workloads available (installed or not) on the current platform, defined by the manifests on disk
        /// </summary>
        public IEnumerable<WorkloadInfo> GetAvailableWorkloads()
            => GetAvailableWorkloadDefinitions().Select(w => new WorkloadInfo(w.workload.Id, w.workload.Description));

        private IEnumerable<(WorkloadDefinition workload, WorkloadManifest manifest)> GetAvailableWorkloadDefinitions()
        {
            foreach ((WorkloadId _, (WorkloadDefinition workload, WorkloadManifest manifest)) in _workloads)
            {
                if (!workload.IsAbstract && IsWorkloadPlatformCompatible(workload, manifest) && !IsWorkloadImplicitlyAbstract(workload, manifest))
                {
                    yield return (workload, manifest);
                }
            }
        }

        /// <summary>
        /// Determines which of the installed workloads has updates available in the advertising manifests.
        /// </summary>
        /// <param name="advertisingManifestResolver">A resolver that composes the advertising manifests with the installed manifests that do not have corresponding advertising manifests</param>
        /// <param name="existingWorkloads">The IDs of all of the installed workloads</param>
        /// <returns></returns>
        public IEnumerable<WorkloadId> GetUpdatedWorkloads(WorkloadResolver advertisingManifestResolver, IEnumerable<WorkloadId> installedWorkloads)
        {
            foreach (var workloadId in installedWorkloads)
            {
                if (!_workloads.ContainsKey(workloadId) || !advertisingManifestResolver._workloads.ContainsKey(workloadId))
                {
                    continue;
                }

                var existingWorkload = _workloads[workloadId];
                var existingPacks = GetPacksInWorkload(existingWorkload.workload, existingWorkload.manifest).Select(p => p.packId).ToHashSet();
                var updatedWorkload = advertisingManifestResolver._workloads[workloadId].workload;
                var updatedPacks = advertisingManifestResolver.GetPacksInWorkload(existingWorkload.workload, existingWorkload.manifest).Select(p => p.packId);

                if (!existingPacks.SetEquals(updatedPacks) || existingPacks.Any(p => PackHasChanged(_packs[p].pack, advertisingManifestResolver._packs[p].pack)))
                {
                    yield return workloadId;
                }
            }
        }

        private bool PackHasChanged(WorkloadPack oldPack, WorkloadPack newPack)
        {
            var existingPackResolvedId = ResolveId(oldPack);
            var newPackResolvedId = ResolveId(newPack);
            if (existingPackResolvedId is null && newPackResolvedId is null)
            {
                return false; // pack still aliases to nothing
            }
            else if (existingPackResolvedId is null || newPackResolvedId is null || !existingPackResolvedId.Value.Equals(newPackResolvedId.Value))
            {
                return true; // alias has changed
            }
            if (!string.Equals(oldPack.Version, newPack.Version, StringComparison.OrdinalIgnoreCase))
            {
                return true; // version has changed
            }
            return false;
        }

        /// <summary>
        /// Finds the manifest for a specified workload.
        /// </summary>
        /// <param name="workloadId">The workload Id for which we want the corresponding manifest.</param>
        /// <returns>The manifest for a corresponding workload.</returns>
        /// <remarks>
        /// Will fail if the workloadId provided is invalid.
        /// </remarks>
        /// <exception>KeyNotFoundException</exception>
        /// <exception>ArgumentNullException</exception>
        public WorkloadManifest GetManifestFromWorkload(WorkloadId workloadId)
        {
            return _workloads[workloadId].manifest;
        }

        public WorkloadResolver CreateOverlayResolver(IWorkloadManifestProvider overlayManifestProvider)
        {
            // we specifically don't assign the overlayManifestProvider to the new resolver
            // because it's not possible to refresh an overlay resolver
            var overlayResolver = new WorkloadResolver(_dotnetRootPaths, _currentRuntimeIdentifiers);
            overlayResolver.LoadManifestsFromProvider(overlayManifestProvider);

            // after loading the overlay manifests into the new resolver
            // we add all the manifests from this resolver that are not overlayed
            foreach (var manifest in _manifests)
            {
                overlayResolver._manifests.TryAdd(manifest.Key, manifest.Value);
            }

            overlayResolver.ComposeWorkloadManifests();

            return overlayResolver;
        }

        public string GetSdkFeatureBand()
        {
            return _manifestProvider?.GetSdkFeatureBand() ?? throw new Exception("Cannot get SDK feature band from ManifestProvider");
        }

        public class PackInfo
        {
            public PackInfo(WorkloadPackId id, string version, WorkloadPackKind kind, string path, string resolvedPackageId)
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
#if USE_SYSTEM_TEXT_JSON
            [JsonConverter(typeof(PackIdJsonConverter))]
#endif
            public WorkloadPackId Id { get; }

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
            public WorkloadInfo(WorkloadId id, string? description)
            {
                Id = id;
                Description = description;
            }

            public WorkloadId Id { get; }
            public string? Description { get; }
        }

        public WorkloadInfo GetWorkloadInfo(WorkloadId workloadId)
        {
            if (_workloads.TryGetValue(workloadId) is not (WorkloadDefinition workload, _))
            {
                throw new ArgumentException($"Workload '{workloadId}' not found", nameof(workloadId));
            }
            return new WorkloadInfo(workload.Id, workload.Description);
        }

        public bool IsPlatformIncompatibleWorkload(WorkloadId workloadId)
        {
            if (_workloads.TryGetValue(workloadId) is not (WorkloadDefinition workload, WorkloadManifest manifest))
            {
                //  Not a recognized workload
                return false;
            }
            return !IsWorkloadPlatformCompatible(workload, manifest);
        }

        private bool IsWorkloadPlatformCompatible(WorkloadDefinition workload, WorkloadManifest manifest)
            => EnumerateWorkloadWithExtends(workload, manifest).All(w =>
                w.workload.Platforms == null || w.workload.Platforms.Count == 0 || w.workload.Platforms.Any(platform => _currentRuntimeIdentifiers.Contains(platform)));

        private bool IsWorkloadImplicitlyAbstract(WorkloadDefinition workload, WorkloadManifest manifest) => !GetPacksInWorkload(workload, manifest).Any();

        public string GetManifestVersion(string manifestId) =>
            (_manifests.TryGetValue(manifestId, out WorkloadManifest? value)? value : null)?.Version
            ?? throw new Exception($"Manifest with id {manifestId} does not exist.");

        public IEnumerable<WorkloadManifestInfo> GetInstalledManifests() => _manifests.Select(m => new WorkloadManifestInfo(m.Value.Id, m.Value.Version, Path.GetDirectoryName(m.Value.ManifestPath)!));
    }

    static class DictionaryExtensions
    {
#if !NETCOREAPP
        public static bool TryAdd<TKey,TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull
        {
            if (dictionary.ContainsKey(key))
            {
                return false;
            }
            dictionary.Add(key, value);
            return true;
        }

        public static void Deconstruct<TKey,TValue>(this KeyValuePair<TKey,TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }
#endif

        public static TValue? TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
            where TKey : notnull
            where TValue : struct
        {
            if (dictionary.TryGetValue(key, out TValue value))
            {
                return value;
            }
            return default(TValue?);
        }
    }
}
