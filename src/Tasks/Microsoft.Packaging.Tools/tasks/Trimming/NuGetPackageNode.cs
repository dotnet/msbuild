// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Metedata and information for a package listed in the lock file.
    /// </summary>
    internal sealed class NuGetPackageNode : IIsIncluded
    {
        private const string NuGetPackageDependencies = "dependencies";
        private List<NuGetPackageNode> _dependencies = new List<NuGetPackageNode>();
        private IEnumerable<string> _dependencyIds;

        public NuGetPackageNode(string id, string version, IEnumerable<string> dependencyIds)
        {
            Id = id;
            IsRuntimePackage = id.StartsWith("runtime.");
            Version = version;
            _dependencyIds = dependencyIds;
        }

        public string Id { get; }
        private bool IsRuntimePackage { get; }
        public bool IsIncluded { get; set; }
        public string Version { get; }
        public bool IsProject { get; }
        public bool IsMetaPackage { get { return Files.Count == 0; } }
        public IEnumerable<NuGetPackageNode> Dependencies { get { return _dependencies; } }
        public IList<FileNode> Files { get; } = new List<FileNode>();

        public void PopulateDependencies(Dictionary<string, NuGetPackageNode> allPackages, ILog log)
        {
            foreach(var dependencyId in _dependencyIds)
            {
                NuGetPackageNode dependencyNode;
                if (!allPackages.TryGetValue(dependencyId, out dependencyNode))
                {
                    // package declared a dependency but NuGet was missing the dependent package
                    // in the lock file.  This indicates a broken restore, but don't fail trimming
                    log.LogMessage(LogImportance.Low, $"Could not locate dependency {dependencyId} of package {Id}.");
                }
                else
                {
                    _dependencies.Add(dependencyNode);

                    // Runtime packages may be brought in by a file-based dependency,
                    // but runtime packages may be missing the dependencies needed since those are 
                    // often declared by the idenity package since it is in the compile graph
                    // and capable of bringing in other runtime-split packages.

                    // Map back up to the identity package so that we can root it and its dependencies.
                    // This creates an artificial cycle, but our graph walk doesn't care about cycles.
                    if (dependencyNode.IsRuntimePackage)
                    {
                        dependencyNode._dependencies.Add(this);
                    }
                }
            }

            _dependencyIds = null;
        }

    }
}
