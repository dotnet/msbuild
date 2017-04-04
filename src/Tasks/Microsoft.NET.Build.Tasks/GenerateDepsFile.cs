// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using NuGet.ProjectModel;
using System;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).deps.json file.
    /// </summary>
    public class GenerateDepsFile : TaskBase
    {
        [Required]
        public string ProjectPath { get; set; }

        [Required]
        public string AssetsFilePath { get; set; }

        [Required]
        public string DepsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string AssemblyExtension { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        [Required]
        public ITaskItem[] AssemblySatelliteAssemblies { get; set; }

        [Required]
        public ITaskItem[] ReferencePaths { get; set; }

        [Required]
        public ITaskItem[] ReferenceSatellitePaths { get; set; }

        [Required]
        public ITaskItem[] Conflicts { get; set; }

        public ITaskItem CompilerOptions { get; set; }

        public ITaskItem[] PrivateAssetsPackageReferences { get; set; }

        public bool IsSelfContained { get; set; }

        List<ITaskItem> _filesWritten = new List<ITaskItem>();

        [Output]
        public ITaskItem[] FilesWritten
        {
            get { return _filesWritten.ToArray(); }
        }

        Dictionary<string, HashSet<string>> compileConflicts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> runtimeConflicts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        protected override void ExecuteCore()
        {
            LoadConflicts();

            LockFile lockFile = new LockFileCache(BuildEngine4).GetLockFile(AssetsFilePath);
            CompilationOptions compilationOptions = CompilationOptionsConverter.ConvertFrom(CompilerOptions);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                ProjectPath,
                AssemblyName,
                AssemblyExtension,
                AssemblyVersion,
                AssemblySatelliteAssemblies);

            IEnumerable<ReferenceInfo> frameworkReferences =
                ReferenceInfo.CreateFrameworkReferenceInfos(ReferencePaths);

            IEnumerable<ReferenceInfo> directReferences =
                ReferenceInfo.CreateDirectReferenceInfos(ReferencePaths, ReferenceSatellitePaths);

            Dictionary<string, SingleProjectInfo> referenceProjects = SingleProjectInfo.CreateProjectReferenceInfos(
                ReferencePaths,
                ReferenceSatellitePaths);

            IEnumerable<string> privateAssets = PackageReferenceConverter.GetPackageIds(PrivateAssetsPackageReferences);

            ProjectContext projectContext = lockFile.CreateProjectContext(
                NuGetUtils.ParseFrameworkName(TargetFramework),
                RuntimeIdentifier,
                PlatformLibraryName,
                IsSelfContained);

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, projectContext)
                .WithFrameworkReferences(frameworkReferences)
                .WithDirectReferences(directReferences)
                .WithReferenceProjectInfos(referenceProjects)
                .WithPrivateAssets(privateAssets)
                .WithCompilationOptions(compilationOptions)
                .WithReferenceAssembliesPath(FrameworkReferenceResolver.GetDefaultReferenceAssembliesPath())
                .Build();

            if (compileConflicts.Any() || runtimeConflicts.Any())
            {
                dependencyContext = TrimConflicts(dependencyContext);
            }

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(DepsFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
            _filesWritten.Add(new TaskItem(DepsFilePath));

        }

        private void LoadConflicts()
        {
            foreach (var conflict in Conflicts)
            {
                string packageId, packageSubPath;
                ConflictResolution.NuGetUtilities.GetPackageParts(conflict.ItemSpec, out packageId, out packageSubPath);

                if (String.IsNullOrEmpty(packageId) || String.IsNullOrEmpty(packageSubPath))
                {
                    continue;
                }

                var itemType = conflict.GetMetadata(nameof(ConflictResolution.ConflictItemType));
                var conflictPackages = (itemType == nameof(ConflictResolution.ConflictItemType.Reference)) ? compileConflicts : runtimeConflicts;

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
            foreach (var runtimeLibrary in runtimeLibraries)
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
            foreach (var resourceAssembly in resourceAssemblies)
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
            foreach (var assembly in assemblies)
            {
                if (!conflicts.Contains(assembly))
                {
                    yield return assembly;
                }
            }
        }
    }
}
