// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
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
        public bool IncludeMainProject { get; set; }

        [Required]
        public ITaskItem[] ReferencePaths { get; set; }

        [Required]
        public ITaskItem[] ReferenceDependencyPaths { get; set; }

        [Required]
        public ITaskItem[] ReferenceSatellitePaths { get; set; }

        [Required]
        public ITaskItem[] ReferenceAssemblies { get; set; }

        [Required]
        public ITaskItem[] FilesToSkip { get; set; }

        public ITaskItem CompilerOptions { get; set; }

        public ITaskItem[] ExcludeFromPublishPackageReferences { get; set; }

        public ITaskItem[] RuntimeStorePackages { get; set; }

        public bool IsSelfContained { get; set; }

        List<ITaskItem> _filesWritten = new List<ITaskItem>();

        [Output]
        public ITaskItem[] FilesWritten
        {
            get { return _filesWritten.ToArray(); }
        }

        private Dictionary<string, HashSet<string>> compileFilesToSkip = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, HashSet<string>> runtimeFilesToSkip = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<PackageIdentity, string> GetFilteredPackages()
        {
            Dictionary<PackageIdentity, string> filteredPackages = null;

            if (RuntimeStorePackages != null && RuntimeStorePackages.Length > 0)
            {
                filteredPackages = new Dictionary<PackageIdentity, string>();
                foreach (var package in RuntimeStorePackages)
                {
                    filteredPackages.Add(
                        ItemUtilities.GetPackageIdentity(package),
                        package.GetMetadata(MetadataKeys.RuntimeStoreManifestNames));
                }
            }

            return filteredPackages;
        }

        protected override void ExecuteCore()
        {
            LoadFilesToSkip();

            LockFile lockFile = new LockFileCache(this).GetLockFile(AssetsFilePath);
            CompilationOptions compilationOptions = CompilationOptionsConverter.ConvertFrom(CompilerOptions);

            SingleProjectInfo mainProject = SingleProjectInfo.Create(
                    ProjectPath,
                    AssemblyName,
                    AssemblyExtension,
                    AssemblyVersion,
                    AssemblySatelliteAssemblies);

            IEnumerable<ReferenceInfo> referenceAssemblyInfos =
                ReferenceInfo.CreateReferenceInfos(ReferenceAssemblies);

            IEnumerable<ReferenceInfo> directReferences =
                ReferenceInfo.CreateDirectReferenceInfos(ReferencePaths, ReferenceSatellitePaths);

            IEnumerable<ReferenceInfo> dependencyReferences =
                ReferenceInfo.CreateDependencyReferenceInfos(ReferenceDependencyPaths, ReferenceSatellitePaths);

            Dictionary<string, SingleProjectInfo> referenceProjects = SingleProjectInfo.CreateProjectReferenceInfos(
                ReferencePaths,
                ReferenceDependencyPaths,
                ReferenceSatellitePaths);

            IEnumerable<string> excludeFromPublishAssets = PackageReferenceConverter.GetPackageIds(ExcludeFromPublishPackageReferences);

            ProjectContext projectContext = lockFile.CreateProjectContext(
                NuGetUtils.ParseFrameworkName(TargetFramework),
                RuntimeIdentifier,
                PlatformLibraryName,
                IsSelfContained);

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, projectContext)
                .WithMainProjectInDepsFile(IncludeMainProject)
                .WithReferenceAssemblies(referenceAssemblyInfos)
                .WithDirectReferences(directReferences)
                .WithDependencyReferences(dependencyReferences)
                .WithReferenceProjectInfos(referenceProjects)
                .WithExcludeFromPublishAssets(excludeFromPublishAssets)
                .WithCompilationOptions(compilationOptions)
                .WithReferenceAssembliesPath(FrameworkReferenceResolver.GetDefaultReferenceAssembliesPath())
                .WithPackagesThatWhereFiltered(GetFilteredPackages())
                .Build();

            if (compileFilesToSkip.Any() || runtimeFilesToSkip.Any())
            {
                dependencyContext = TrimFilesToSkip(dependencyContext);
            }

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(DepsFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
            _filesWritten.Add(new TaskItem(DepsFilePath));

        }

        private void LoadFilesToSkip()
        {
            foreach (var fileToSkip in FilesToSkip)
            {
                string packageId, packageSubPath;
                NuGetUtils.GetPackageParts(fileToSkip.ItemSpec, out packageId, out packageSubPath);

                if (String.IsNullOrEmpty(packageId) || String.IsNullOrEmpty(packageSubPath))
                {
                    continue;
                }

                var itemType = fileToSkip.GetMetadata(nameof(ConflictResolution.ConflictItemType));
                var packagesWithFilesToSkip = (itemType == nameof(ConflictResolution.ConflictItemType.Reference)) ? compileFilesToSkip : runtimeFilesToSkip;

                HashSet<string> filesToSkipForPackage;
                if (!packagesWithFilesToSkip.TryGetValue(packageId, out filesToSkipForPackage))
                {
                    packagesWithFilesToSkip[packageId] = filesToSkipForPackage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                filesToSkipForPackage.Add(packageSubPath);
            }
        }

        private DependencyContext TrimFilesToSkip(DependencyContext sourceDeps)
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
                HashSet<string> filesToSkip;
                if (runtimeFilesToSkip.TryGetValue(runtimeLibrary.Name, out filesToSkip))
                {
                    yield return new RuntimeLibrary(runtimeLibrary.Type,
                                              runtimeLibrary.Name,
                                              runtimeLibrary.Version,
                                              runtimeLibrary.Hash,
                                              TrimAssetGroups(runtimeLibrary.RuntimeAssemblyGroups, filesToSkip).ToArray(),
                                              TrimAssetGroups(runtimeLibrary.NativeLibraryGroups, filesToSkip).ToArray(),
                                              TrimResourceAssemblies(runtimeLibrary.ResourceAssemblies, filesToSkip),
                                              runtimeLibrary.Dependencies,
                                              runtimeLibrary.Serviceable,
                                              runtimeLibrary.Path,
                                              runtimeLibrary.HashPath,
                                              runtimeLibrary.RuntimeStoreManifestName);
                }
                else
                {
                    yield return runtimeLibrary;
                }
            }
        }

        private IEnumerable<RuntimeAssetGroup> TrimAssetGroups(IEnumerable<RuntimeAssetGroup> assetGroups, ISet<string> filesToTrim)
        {
            foreach (var assetGroup in assetGroups)
            {
                yield return new RuntimeAssetGroup(assetGroup.Runtime, TrimAssemblies(assetGroup.AssetPaths, filesToTrim));
            }
        }

        private IEnumerable<ResourceAssembly> TrimResourceAssemblies(IEnumerable<ResourceAssembly> resourceAssemblies, ISet<string> filesToTrim)
        {
            foreach (var resourceAssembly in resourceAssemblies)
            {
                if (!filesToTrim.Contains(resourceAssembly.Path))
                {
                    yield return resourceAssembly;
                }
            }
        }

        private IEnumerable<CompilationLibrary> TrimCompilationLibraries(IReadOnlyList<CompilationLibrary> compileLibraries)
        {
            foreach (var compileLibrary in compileLibraries)
            {
                HashSet<string> filesToSkip;
                if (compileFilesToSkip.TryGetValue(compileLibrary.Name, out filesToSkip))
                {
                    yield return new CompilationLibrary(compileLibrary.Type,
                                              compileLibrary.Name,
                                              compileLibrary.Version,
                                              compileLibrary.Hash,
                                              TrimAssemblies(compileLibrary.Assemblies, filesToSkip),
                                              compileLibrary.Dependencies,
                                              compileLibrary.Serviceable,
                                              compileLibrary.Path,
                                              compileLibrary.HashPath);
                }
                else
                {
                    yield return compileLibrary;
                }
            }
        }

        private IEnumerable<string> TrimAssemblies(IEnumerable<string> assemblies, ISet<string> filesToTrim)
        {
            foreach (var assembly in assemblies)
            {
                if (!filesToTrim.Contains(assembly))
                {
                    yield return assembly;
                }
            }
        }
    }
}
