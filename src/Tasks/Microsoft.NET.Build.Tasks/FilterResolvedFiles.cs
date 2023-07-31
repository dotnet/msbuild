// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.ProjectModel;
using NuGet.Packaging.Core;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Filters out the assemblies from the list based on a given package closure.
    /// </summary>
    public class FilterResolvedFiles : TaskBase
    {
        private readonly List<ITaskItem> _assembliesToPublish = new List<ITaskItem>();
        private readonly List<ITaskItem> _packagesResolved = new List<ITaskItem>();

        public string AssetsFilePath { get; set; }

        [Required]
        public ITaskItem[] ResolvedFiles { get; set; }

        [Required]
        public ITaskItem[] PackagesToPrune { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        public bool IsSelfContained { get; set; }

        /// <summary>
        /// All the assemblies to publish.
        /// </summary>
        [Output]
        public ITaskItem[] AssembliesToPublish
        {
            get; private set;
        }
        [Output]
        public ITaskItem[] PublishedPackages
        {
            get; private set;
        }

        protected override void ExecuteCore()
        {
            var lockFileCache = new LockFileCache(this);
            LockFile lockFile = lockFileCache.GetLockFile(AssetsFilePath);

            ProjectContext projectContext = lockFile.CreateProjectContext(
                TargetFramework,
                RuntimeIdentifier,
                PlatformLibraryName,
                runtimeFrameworks: null,
                IsSelfContained);

            var packageClosure =  new HashSet<PackageIdentity>();

            foreach ( var packageItem in PackagesToPrune)
            {
                var pkgName = packageItem.ItemSpec;
                if (!string.IsNullOrEmpty(pkgName))
                {
                    packageClosure.UnionWith(projectContext.GetTransitiveList(pkgName, ignoreIfNotFound: true));
                }
            }

            var packagesToPublish = new HashSet<PackageIdentity>();
            foreach (var resolvedFile in ResolvedFiles)
            {
                var resolvedPkg = ItemUtilities.GetPackageIdentity(resolvedFile);

                if (resolvedPkg != null && !packageClosure.Contains(resolvedPkg))
                {
                    _assembliesToPublish.Add(resolvedFile);
                    packagesToPublish.Add(resolvedPkg);

                }
            }
            AssembliesToPublish = _assembliesToPublish.ToArray();

            foreach (var resolvedPkg in packagesToPublish)
            {
                TaskItem item = new TaskItem(resolvedPkg.Id);
                item.SetMetadata("Version", resolvedPkg.Version.ToString());
                _packagesResolved.Add(item);
            }
            PublishedPackages = _packagesResolved.ToArray();
        }
    }
}
