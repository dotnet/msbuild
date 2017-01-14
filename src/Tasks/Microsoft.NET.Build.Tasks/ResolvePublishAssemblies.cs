// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Resolves the assemblies to be published for a .NET app.
    /// </summary>
    public class ResolvePublishAssemblies : TaskBase
    {
        private readonly List<ITaskItem> _assembliesToPublish = new List<ITaskItem>();

        [Required]
        public string ProjectPath { get; set; }

        [Required]
        public string AssetsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string PlatformLibraryName { get; set; }

        public ITaskItem[] PrivateAssetsPackageReferences { get; set; }

        public bool PreserveCacheLayout { get; set; }

        public string FilterProjectAssetsFile { get; set; }

        /// <summary>
        /// All the assemblies to publish.
        /// </summary>
        [Output]
        public ITaskItem[] AssembliesToPublish
        {
            get { return _assembliesToPublish.ToArray(); }
        }

        protected override void ExecuteCore()
        {
            var lockFileCache = new LockFileCache(BuildEngine4);
            LockFile lockFile = lockFileCache.GetLockFile(AssetsFilePath);
            IEnumerable<string> privateAssetsPackageIds = PackageReferenceConverter.GetPackageIds(PrivateAssetsPackageReferences);
            IPackageResolver packageResolver = NuGetPackageResolver.CreateResolver(lockFile, ProjectPath);

            LockFile filterLockFile = null;
            if (!string.IsNullOrEmpty(FilterProjectAssetsFile))
            {
                filterLockFile = lockFileCache.GetLockFile(FilterProjectAssetsFile);

            }
            ProjectContext projectContext = lockFile.CreateProjectContext(
                NuGetUtils.ParseFrameworkName(TargetFramework),
                RuntimeIdentifier,
                PlatformLibraryName,
                filterLockFile
                );

            IEnumerable<ResolvedFile> resolvedAssemblies =
                new PublishAssembliesResolver(packageResolver)
                    .WithPrivateAssets(privateAssetsPackageIds)
                    .WithPreserveCacheLayout(PreserveCacheLayout)
                    .Resolve(projectContext);

            foreach (ResolvedFile resolvedAssembly in resolvedAssemblies)
            {
                TaskItem item = new TaskItem(resolvedAssembly.SourcePath);
                item.SetMetadata("DestinationSubPath", resolvedAssembly.DestinationSubPath);
                item.SetMetadata("AssetType", resolvedAssembly.Asset.ToString().ToLower());
                _assembliesToPublish.Add(item);
            }
        }
    }
}
