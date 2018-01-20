// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    public class ResolvePackageAssets : TaskBase
    {
        [Required]
        public string ProjectAssetsFile { get; set; }

        [Required]
        public string ProjectPath { get; set; }

        public string ProjectLanguage { get; set; }

        public bool DisableTransitiveProjectReferences { get; set; }

        public bool DisableFrameworkAssemblies { get; set; }

        [Required]
        public string TargetFrameworkMoniker { get; set; }

        public string RuntimeIdentifier { get; set; }

        public bool MarkPackageReferencesAsExternallyResolved { get; set; }

        /// <summary>
        /// Check that there is at least one package dependency in the RID graph that is not in the RID-agnostic graph.
        /// Used as a heuristic to detect invalid RIDs.
        /// </summary>
        public bool EnsureRuntimePackageDependencies { get; set; }

        [Output]
        public ITaskItem[] Analyzers { get; private set; }

        [Output]
        public ITaskItem[] ContentFilesToPreprocess { get; private set; }

        [Output]
        public ITaskItem[] CompileTimeAssemblies { get; private set; }

        [Output]
        public ITaskItem[] FrameworkAssemblies { get; private set; }

        [Output]
        public ITaskItem[] NativeLibraries { get; private set; }

        [Output]
        public ITaskItem[] ResourceAssemblies { get; private set; }

        [Output]
        public ITaskItem[] RuntimeAssemblies { get; private set; }

        [Output]
        public ITaskItem[] RuntimeTargets { get; private set; }

        [Output]
        public ITaskItem[] TransitiveProjectReferences { get; private set; }

        private NuGetPackageResolver _packageResolver;

        protected override void ExecuteCore()
        {
            var lockFile = new LockFileCache(BuildEngine4).GetLockFile(ProjectAssetsFile);
            _packageResolver = NuGetPackageResolver.CreateResolver(lockFile, ProjectPath);

            var targetFramework = NuGetUtils.ParseFrameworkName(TargetFrameworkMoniker);
            var compileTimeTarget = lockFile.GetTargetAndThrowIfNotFound(targetFramework, runtime: null);
            var runtimeTarget = lockFile.GetTargetAndThrowIfNotFound(targetFramework, RuntimeIdentifier);

            CheckRuntimePackageDependencies(compileTimeTarget, runtimeTarget);

            Analyzers = RaiseAnalyzers(
                lockFile,
                runtimeTarget);

            CompileTimeAssemblies = RaisePackageAssets(
                compileTimeTarget,
                package => package.CompileTimeAssemblies,
                setup: (asset, item) =>
                {
                    item.SetMetadata(MetadataKeys.Private, "false");
                    item.SetMetadata(MetadataKeys.HintPath, item.ItemSpec);

                    if (MarkPackageReferencesAsExternallyResolved)
                    {
                        item.SetMetadata(MetadataKeys.ExternallyResolved, "true");
                    }
                });

            if (!DisableFrameworkAssemblies)
            {
                FrameworkAssemblies = RaiseFrameworkAssemblies(
                    compileTimeTarget,
                    setup: (asset, item) =>
                     {
                         item.SetMetadata(MetadataKeys.Pack, "false");
                         item.SetMetadata(MetadataKeys.Private, "false");
                         item.SetMetadata(MetadataKeys.NuGetIsFrameworkReference, "true");
                     });
            }

            ContentFilesToPreprocess = RaisePackageAssets(
                runtimeTarget,
                p => p.ContentFiles,
                filter: asset => !string.IsNullOrEmpty(asset.PPOutputPath),
                setup: (asset, item) => 
                {
                    item.SetMetadata(MetadataKeys.BuildAction, asset.BuildAction.ToString());
                    item.SetMetadata(MetadataKeys.CopyToOutput, asset.CopyToOutput.ToString());
                    item.SetMetadata(MetadataKeys.PPOutputPath, asset.PPOutputPath);

                    if (!string.IsNullOrEmpty(MetadataKeys.OutputPath))
                    {
                        item.SetMetadata(MetadataKeys.OutputPath, asset.OutputPath);
                    }

                    if (!string.IsNullOrEmpty(asset.CodeLanguage))
                    {
                        item.SetMetadata(MetadataKeys.CodeLanguage, asset.CodeLanguage);
                    }
                });

            NativeLibraries = RaisePackageAssets(
                runtimeTarget,
                package => package.NativeLibraries);

            ResourceAssemblies = RaisePackageAssets(
                runtimeTarget,
                package => package.ResourceAssemblies,
                setup: (asset, item) =>
                {
                    string locale = asset.Properties["locale"];
                    item.SetMetadata(MetadataKeys.Culture, locale);
                    item.SetMetadata(MetadataKeys.DestinationSubDirectory, locale + Path.DirectorySeparatorChar);
                });

            RuntimeAssemblies = RaisePackageAssets(
                runtimeTarget,
                package => package.RuntimeAssemblies);

            RuntimeTargets = RaisePackageAssets(
                runtimeTarget,
                package => package.RuntimeTargets,
                setup: (asset, item) =>
                {
                    string directory = Path.GetDirectoryName(asset.Path);
                    item.SetMetadata(MetadataKeys.DestinationSubDirectory, directory + Path.DirectorySeparatorChar);
                });

            if (!DisableTransitiveProjectReferences)
            {
                TransitiveProjectReferences = RaiseTransitiveProjectReferences(lockFile, runtimeTarget);
            }
        }

        private ITaskItem[] RaisePackageAssets<T>(
            LockFileTarget target, 
            Func<LockFileTargetLibrary, IList<T>> getAssets,
            Func<T, bool> filter = null,
            Action<T, ITaskItem> setup = null) 
            where T : LockFileItem
        {
            var items = new List<ITaskItem>();

            foreach (var library in target.Libraries)
            {
                if (library.IsPackage())
                {
                    foreach (T asset in getAssets(library))
                    {
                        if (!asset.IsPlaceholderFile() && (filter == null || filter(asset)))
                        {
                            var item = CreatePackageAssetItem(library, ResolvePackageAssetPath(library, asset.Path));
                            setup?.Invoke(asset, item);
                            items.Add(item);
                        }
                    }
                }
            }

            return items.ToArray();
        }

        private static ITaskItem[] RaiseFrameworkAssemblies(LockFileTarget target, Action<string, ITaskItem> setup)
        {
            var items = new List<ITaskItem>();

            foreach (var library in target.Libraries)
            {
                if (library.IsPackage())
                {
                    foreach (string frameworkAssembly in library.FrameworkAssemblies)
                    {
                        var item = CreatePackageAssetItem(library, frameworkAssembly);
                        items.Add(item);
                        setup?.Invoke(frameworkAssembly, item);
                    }
                }
            }

            return items.ToArray();
        }

        private ITaskItem[] RaiseTransitiveProjectReferences(LockFile lockFile, LockFileTarget target)
        {
            var items = new List<ITaskItem>();

            Dictionary<string, string> projectReferencePaths = null;
            HashSet<string> directProjectDependencies = null;

            foreach (var library in target.Libraries)
            {
                if (library.IsTransitiveProjectReference(lockFile, ref directProjectDependencies))
                {
                    if (projectReferencePaths == null)
                    {
                        projectReferencePaths = GetProjectReferencePaths(lockFile);
                    }

                    if (!directProjectDependencies.Contains(library.Name))
                    {
                        items.Add(new TaskItem(projectReferencePaths[library.Name]));
                    }
                }
            }

            return items.ToArray();
        }

        private ITaskItem[] RaiseAnalyzers(LockFile lockFile, LockFileTarget target)
        {
            var items = new List<ITaskItem>();
            Dictionary<string, LockFileTargetLibrary> targetLibraries = null;

            foreach (var library in lockFile.Libraries)
            {
                if (library.IsPackage())
                {
                    foreach (var file in library.Files)
                    {
                        if (NuGetUtils.IsApplicableAnalyzer(file, ProjectLanguage))
                        {
                            if (targetLibraries == null)
                            {
                                targetLibraries = target.Libraries.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
                            }

                            if (targetLibraries.TryGetValue(library.Name, out var targetLibrary))
                            {
                                items.Add(CreatePackageAssetItem(targetLibrary, ResolvePackageAssetPath(targetLibrary, file)));
                            }
                        }
                    }
                }
            }

            return items.ToArray();
        }

        private static ITaskItem CreatePackageAssetItem(LockFileTargetLibrary package, string itemSpec)
        {
            var item = new TaskItem(itemSpec);
            item.SetMetadata(MetadataKeys.NuGetSourceType, "Package");
            item.SetMetadata(MetadataKeys.NuGetPackageId, package.Name);
            item.SetMetadata(MetadataKeys.NuGetPackageVersion, package.Version.ToNormalizedString());
            return item;
        }

        private string ResolvePackageAssetPath(LockFileTargetLibrary package, string relativePath)
        {
            string packagePath = _packageResolver.GetPackageDirectory(package.Name, package.Version);
            return Path.Combine(packagePath, NormalizeRelativePath(relativePath));
        }

        private static Dictionary<string, string> GetProjectReferencePaths(LockFile lockFile)
        {
            Dictionary<string, string> paths = new Dictionary<string, string>();

            foreach (var library in lockFile.Libraries)
            {
                if (library.IsProject())
                {
                    paths[library.Name] = NormalizeRelativePath(library.MSBuildProject);
                }
            }

            return paths;
        }

        private void CheckRuntimePackageDependencies(LockFileTarget compileTimeTarget, LockFileTarget runtimeTarget)
        {
            if (EnsureRuntimePackageDependencies && !string.IsNullOrEmpty(RuntimeIdentifier))
            {
                if (compileTimeTarget.Libraries.Count >= runtimeTarget.Libraries.Count)
                {
                    throw new BuildErrorException(Strings.UnsupportedRuntimeIdentifier, RuntimeIdentifier);
                }
            }
        }

        private static string NormalizeRelativePath(string relativePath)
            => relativePath.Replace('/', Path.DirectorySeparatorChar);
    }
}
