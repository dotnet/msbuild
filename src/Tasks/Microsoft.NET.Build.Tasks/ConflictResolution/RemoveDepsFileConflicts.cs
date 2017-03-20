// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    public partial class RemoveDepsFileConflicts : Task
    {
        Dictionary<string, HashSet<string>> compileConflicts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> runtimeConflicts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        [Required]
        public string DepsFilePath { get; set; }

        [Required]
        public ITaskItem[] Conflicts { get; set; }

        public override bool Execute()
        {
            LoadConflicts();

            DependencyContext sourceDeps;
            using (var sourceStream = File.Open(DepsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                sourceDeps = new DependencyContextJsonReader().Read(sourceStream);
            }

            DependencyContext trimmedDeps = TrimConflicts(sourceDeps);

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(DepsFilePath))
            {
                writer.Write(trimmedDeps, fileStream);
            }

            return !Log.HasLoggedErrors;
        }

        private void LoadConflicts()
        {
            foreach (var conflict in Conflicts)
            {
                string packageId, packageSubPath;
                NuGetUtilities.GetPackageParts(conflict.ItemSpec, out packageId, out packageSubPath);

                if (String.IsNullOrEmpty(packageId) || String.IsNullOrEmpty(packageSubPath))
                {
                    continue;
                }

                var itemType = conflict.GetMetadata(nameof(ConflictItemType));
                var conflictPackages = (itemType == nameof(ConflictItemType.Reference)) ? compileConflicts : runtimeConflicts;

                HashSet<string> conflictFiles;
                if (!conflictPackages.TryGetValue(packageId, out conflictFiles))
                {
                    conflictPackages[packageId] = conflictFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                conflictFiles.Add(packageSubPath);
            }
        }

        private DependencyContext TrimConflicts(DependencyContext sourceDeps)
        {
            return new DependencyContext(sourceDeps.Target,
                                         sourceDeps.CompilationOptions,
                                         TrimCompilationLibraries(sourceDeps.CompileLibraries),
                                         TrimRuntimeLibraries(sourceDeps.RuntimeLibraries),
                                         sourceDeps.RuntimeGraph);
        }

        private IEnumerable<RuntimeLibrary> TrimRuntimeLibraries(IReadOnlyList<RuntimeLibrary> runtimeLibraries)
        {
            foreach(var runtimeLibrary in runtimeLibraries)
            {
                HashSet<string> conflictFiles;
                if (runtimeConflicts.TryGetValue(runtimeLibrary.Name, out conflictFiles))
                {
                    yield return new RuntimeLibrary(runtimeLibrary.Type,
                                              runtimeLibrary.Name,
                                              runtimeLibrary.Version,
                                              runtimeLibrary.Hash,
                                              TrimAssetGroups(runtimeLibrary.RuntimeAssemblyGroups, conflictFiles).ToArray(),
                                              TrimAssetGroups(runtimeLibrary.NativeLibraryGroups, conflictFiles).ToArray(),
                                              TrimResourceAssemblies(runtimeLibrary.ResourceAssemblies, conflictFiles),
                                              runtimeLibrary.Dependencies,
                                              runtimeLibrary.Serviceable);
                }
                else
                {
                    yield return runtimeLibrary;
                }
            }
        }

        private IEnumerable<RuntimeAssetGroup> TrimAssetGroups(IEnumerable<RuntimeAssetGroup> assetGroups, ISet<string> conflicts)
        {
            foreach (var assetGroup in assetGroups)
            {
                yield return new RuntimeAssetGroup(assetGroup.Runtime, TrimAssemblies(assetGroup.AssetPaths, conflicts));
            }
        }

        private IEnumerable<ResourceAssembly> TrimResourceAssemblies(IEnumerable<ResourceAssembly> resourceAssemblies, ISet<string> conflicts)
        {
            foreach(var resourceAssembly in resourceAssemblies)
            {
                if (!conflicts.Contains(resourceAssembly.Path))
                {
                    yield return resourceAssembly;
                }
            }
        }

        private IEnumerable<CompilationLibrary> TrimCompilationLibraries(IReadOnlyList<CompilationLibrary> compileLibraries)
        {
            foreach (var compileLibrary in compileLibraries)
            {
                HashSet<string> conflictFiles;
                if (compileConflicts.TryGetValue(compileLibrary.Name, out conflictFiles))
                {
                    yield return new CompilationLibrary(compileLibrary.Type,
                                              compileLibrary.Name,
                                              compileLibrary.Version,
                                              compileLibrary.Hash,
                                              TrimAssemblies(compileLibrary.Assemblies, conflictFiles),
                                              compileLibrary.Dependencies,
                                              compileLibrary.Serviceable);
                }
                else
                {
                    yield return compileLibrary;
                }
            }
        }

        private IEnumerable<string> TrimAssemblies(IEnumerable<string> assemblies, ISet<string> conflicts)
        {
            foreach(var assembly in assemblies)
            {
                if (!conflicts.Contains(assembly))
                {
                    yield return assembly;
                }
            }
        }
    }
}
