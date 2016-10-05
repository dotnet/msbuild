// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NETCore.Build.Tasks
{
    /// <summary>
    /// Resolves the assemblies to be published for a .NET app.
    /// </summary>
    public class ResolvePublishAssemblies : Task
    {
        private readonly List<ITaskItem> _assembliesToPublish = new List<ITaskItem>();

        [Required]
        public string ProjectPath { get; set; }

        [Required]
        public string LockFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        public ITaskItem[] PrivateAssetsPackageReferences { get; set; }

        /// <summary>
        /// All the assemblies to publish.
        /// </summary>
        [Output]
        public ITaskItem[] AssembliesToPublish
        {
            get { return _assembliesToPublish.ToArray(); }
        }

        public override bool Execute()
        {
            LockFile lockFile = new LockFileCache(BuildEngine4).GetLockFile(LockFilePath);
            NuGetFramework framework = TargetFramework == null ? null : NuGetFramework.Parse(TargetFramework);
            NuGetPathContext nugetPathContext = NuGetPathContext.Create(Path.GetDirectoryName(ProjectPath));
            IEnumerable<string> privateAssetsPackageIds = PackageReferenceConverter.GetPackageIds(PrivateAssetsPackageReferences);

            IEnumerable<ResolvedFile> resolvedAssemblies = 
                new PublishAssembliesResolver(lockFile, new NuGetPackageResolver(nugetPathContext))
                    .WithPrivateAssets(privateAssetsPackageIds)
                    .Resolve(framework, RuntimeIdentifier);

            foreach (ResolvedFile resolvedAssembly in resolvedAssemblies)
            {
                TaskItem item = new TaskItem(resolvedAssembly.SourcePath);
                item.SetMetadata("DestinationSubPath", resolvedAssembly.DestinationSubPath);

                _assembliesToPublish.Add(item);
            }

            return true;
        }
    }
}
