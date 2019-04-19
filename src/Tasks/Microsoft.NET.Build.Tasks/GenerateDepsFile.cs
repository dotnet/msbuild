// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public ITaskItem[] RuntimeFrameworks { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string AssemblyExtension { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        public ITaskItem[] AssemblySatelliteAssemblies { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public bool IncludeMainProject { get; set; }


        public ITaskItem[] ReferencePaths { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ReferenceDependencyPaths { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ReferenceSatellitePaths { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] ReferenceAssemblies { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public ITaskItem[] FilesToSkip { get; set; }

        public ITaskItem[] RuntimePackAssets { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem CompilerOptions { get; set; }

        public ITaskItem[] ExcludeFromPublishPackageReferences { get; set; }

        public ITaskItem[] RuntimeStorePackages { get; set; }

        [Required]
        public ITaskItem[] CompileReferences { get; set; }

        //  NativeCopyLocalItems, ResourceCopyLocalItems, RuntimeCopyLocalItems
        [Required]
        public ITaskItem[] ResolvedNuGetFiles { get; set; }

        [Required]
        public ITaskItem[] ResolvedRuntimeTargetsFiles { get; set; }

        public bool IsSelfContained { get; set; }

        public bool IncludeRuntimeFileVersions { get; set; }

        public string DepsFileGenerationMode { get; set; } = "new";

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

        private void WriteDepsFileOld(string depsFilePath)
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

            IEnumerable<RuntimePackAssetInfo> runtimePackAssets = 
                RuntimePackAssets.Select(item => RuntimePackAssetInfo.FromItem(item));

            ProjectContext projectContext = lockFile.CreateProjectContext(
                NuGetUtils.ParseFrameworkName(TargetFramework),
                RuntimeIdentifier,
                PlatformLibraryName,
                RuntimeFrameworks,
                IsSelfContained);

            DependencyContext dependencyContext = new DependencyContextBuilder(mainProject, projectContext, IncludeRuntimeFileVersions)
                .WithMainProjectInDepsFile(IncludeMainProject)
                .WithReferenceAssemblies(referenceAssemblyInfos)
                .WithDirectReferences(directReferences)
                .WithDependencyReferences(dependencyReferences)
                .WithReferenceProjectInfos(referenceProjects)
                .WithExcludeFromPublishAssets(excludeFromPublishAssets)
                .WithRuntimePackAssets(runtimePackAssets)
                .WithCompilationOptions(compilationOptions)
                .WithReferenceAssembliesPath(FrameworkReferenceResolver.GetDefaultReferenceAssembliesPath())
                .WithPackagesThatWhereFiltered(GetFilteredPackages())
                .Build();

            if (compileFilesToSkip.Any() || runtimeFilesToSkip.Any())
            {
                dependencyContext = TrimFilesToSkip(dependencyContext);
            }

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(depsFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
            _filesWritten.Add(new TaskItem(depsFilePath));
        }

        private void WriteDepsFileNew(string depsFilePath)
        {
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

            IEnumerable<RuntimePackAssetInfo> runtimePackAssets =
                RuntimePackAssets.Select(item => RuntimePackAssetInfo.FromItem(item));


            ProjectContext projectContext = lockFile.CreateProjectContext(
                NuGetUtils.ParseFrameworkName(TargetFramework),
                RuntimeIdentifier,
                PlatformLibraryName,
                RuntimeFrameworks,
                IsSelfContained);

            var builder = new DependencyContextBuilder2(mainProject, projectContext, IncludeRuntimeFileVersions);

            builder = builder
                .WithMainProjectInDepsFile(IncludeMainProject)
                .WithReferenceAssemblies(referenceAssemblyInfos)
                .WithDirectReferences(directReferences)
                .WithDependencyReferences(dependencyReferences)
                .WithReferenceProjectInfos(referenceProjects)
                .WithExcludeFromPublishAssets(excludeFromPublishAssets)
                .WithRuntimePackAssets(runtimePackAssets)
                .WithCompilationOptions(compilationOptions)
                .WithReferenceAssembliesPath(FrameworkReferenceResolver.GetDefaultReferenceAssembliesPath())
                .WithPackagesThatWereFiltered(GetFilteredPackages());

            if (CompileReferences.Length > 0)
            {
                builder = builder.WithCompileReferences(ReferenceInfo.CreateReferenceInfos(CompileReferences));
            }

            var resolvedNuGetFiles = ResolvedNuGetFiles.Select(f => new ResolvedFile(f, false))
                                .Concat(ResolvedRuntimeTargetsFiles.Select(f => new ResolvedFile(f, true)));
            builder = builder.WithResolvedNuGetFiles(resolvedNuGetFiles);

            DependencyContext dependencyContext = builder.Build();

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(depsFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
            _filesWritten.Add(new TaskItem(depsFilePath));
        }

        bool _loggedLocalError = false;

        public override bool Execute()
        {
            if (!base.Execute() || _loggedLocalError)
            {
                return false;
            }
            return true;
        }
        protected override void ExecuteCore()
        {
            if (DepsFileGenerationMode.Equals("old", StringComparison.InvariantCultureIgnoreCase))
            {
                WriteDepsFileOld(DepsFilePath);
            }
            else if (DepsFileGenerationMode.Equals("new", StringComparison.InvariantCultureIgnoreCase))
            {
                WriteDepsFileNew(DepsFilePath);
            }
            else
            {
                var newDepsFilePath = Path.ChangeExtension(DepsFilePath, ".new.json");

                WriteDepsFileOld(DepsFilePath);

                WriteDepsFileNew(newDepsFilePath);

                var oldJson = File.ReadAllText(DepsFilePath);
                var newJson = File.ReadAllText(newDepsFilePath);

                if (oldJson != newJson)
                {
                    string message = "Internal error: new deps file generation logic did not produce the same result as the old logic." + Environment.NewLine +
                        "    Please file an issue for this at https://github.com/dotnet/sdk and include the following two files: " + Environment.NewLine +                        
                        "    Deps file from old logic: " + DepsFilePath + Environment.NewLine +
                        "    Deps file from new logic: " + newDepsFilePath + Environment.NewLine +
                        "    You can work around this by setting the DepsFileGenerationMode MSBuild property to 'old'";

                    //  This is a temporary error message that we won't localize or assign an SDK
                    //  error code to.  So use the Task classes Log property instead of our wrapper
                    //  around it (which would force it to have an error code)
                    ((Task) this).Log.LogError(message);

                    _loggedLocalError = true;
                }
                else
                {
                    //  If the files matched, then delete the .new.json file
                    File.Delete(newDepsFilePath);
                }
            }
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
                var trimmedFiles = TrimRuntimeFiles(assetGroup.RuntimeFiles, filesToTrim).ToList();
                if (trimmedFiles.Any())
                {
                    yield return new RuntimeAssetGroup(assetGroup.Runtime, trimmedFiles);
                }
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


        private IEnumerable<RuntimeFile> TrimRuntimeFiles(IEnumerable<RuntimeFile> assemblies, ISet<string> filesToTrim)
        {
            foreach (var assembly in assemblies)
            {
                if (!filesToTrim.Contains(assembly.Path))
                {
                    yield return assembly;
                }
            }
        }
    }
}
