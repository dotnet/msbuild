// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Resolves the assets from the package dependencies that should be copied to output/publish directories.
    /// </summary>
    public class ResolveCopyLocalAssets : TaskBase
    {
        private readonly List<ITaskItem> _resolvedAssets = new();

        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        public ITaskItem[] RuntimeFrameworks { get; set; }

        public ITaskItem[] ExcludedPackageReferences { get; set; }

        public bool PreserveStoreLayout { get; set; }

        public ITaskItem[] RuntimeStorePackages { get; set; }

        public bool IsSelfContained { get; set; }

        public bool ResolveRuntimeTargets { get; set; }

        [Output]
        public ITaskItem[] ResolvedAssets => _resolvedAssets.ToArray();

        protected override void ExecuteCore()
        {
            var lockFileCache = new LockFileCache(this);
            LockFile lockFile = lockFileCache.GetLockFile(AssetsFilePath);
            HashSet<PackageIdentity> packagestoBeFiltered = null;

            if (RuntimeStorePackages != null && RuntimeStorePackages.Length > 0)
            {
                packagestoBeFiltered = new HashSet<PackageIdentity>();
                foreach (var package in RuntimeStorePackages)
                {
                    packagestoBeFiltered.Add(ItemUtilities.GetPackageIdentity(package));
                }
            }

            ProjectContext projectContext = lockFile.CreateProjectContext(
                TargetFramework,
                RuntimeIdentifier,
                PlatformLibraryName,
                RuntimeFrameworks,
                IsSelfContained);

            projectContext.PackagesToBeFiltered = packagestoBeFiltered;

            var assetsFileResolver =
                new AssetsFileResolver(NuGetPackageResolver.CreateResolver(lockFile))
                    .WithExcludedPackages(PackageReferenceConverter.GetPackageIds(ExcludedPackageReferences))
                    .WithPreserveStoreLayout(PreserveStoreLayout);

            foreach (var resolvedFile in assetsFileResolver.Resolve(projectContext, resolveRuntimeTargets: ResolveRuntimeTargets))
            {
                TaskItem item = new(resolvedFile.SourcePath);

                item.SetMetadata(MetadataKeys.DestinationSubPath, resolvedFile.DestinationSubPath);
                item.SetMetadata(MetadataKeys.DestinationSubDirectory, resolvedFile.DestinationSubDirectory);
                item.SetMetadata(MetadataKeys.AssetType, resolvedFile.Asset.ToString().ToLowerInvariant());
                item.SetMetadata(MetadataKeys.NuGetPackageId, resolvedFile.PackageName);
                item.SetMetadata(MetadataKeys.NuGetPackageVersion, resolvedFile.PackageVersion.ToLowerInvariant());

                if (resolvedFile.Asset == AssetType.Resources)
                {
                    //  For resources, the DestinationSubDirectory is set to the locale.  Set the Culture
                    //  metadata on the generated item to this value so that the satellite assemblies can
                    //  be filtered by culture.
                    item.SetMetadata(MetadataKeys.Culture, resolvedFile.DestinationSubDirectory.TrimEnd(Path.DirectorySeparatorChar));
                }

                _resolvedAssets.Add(item);
            }
        }
    }
}
