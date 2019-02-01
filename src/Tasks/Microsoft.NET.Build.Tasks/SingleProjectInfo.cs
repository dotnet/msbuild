// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// These are Projects for which there is no csproj path info in RAR (indirect project references)
    /// </summary>
    internal class UnreferencedProjectInfo : SingleProjectInfo
    {
        internal static UnreferencedProjectInfo Default = new UnreferencedProjectInfo();

        private UnreferencedProjectInfo() : base(string.Empty, string.Empty, string.Empty, string.Empty, new List<ReferenceInfo>(), new List<ResourceAssemblyInfo>())
        {
        }
    }

    internal class SingleProjectInfo
    {
        public string ProjectPath { get; }
        public string Name { get; }
        public string Version { get; }
        public string OutputName { get; }

        private List<ReferenceInfo> _dependencyReferences;
        public IEnumerable<ReferenceInfo> DependencyReferences
        {
            get { return _dependencyReferences; }
        }

        private List<ResourceAssemblyInfo> _resourceAssemblies;
        public IEnumerable<ResourceAssemblyInfo> ResourceAssemblies
        {
            get { return _resourceAssemblies; }
        }

        protected SingleProjectInfo()
        {
        }

        protected SingleProjectInfo(string projectPath, string name, string version, string outputName, List<ReferenceInfo> dependencyReferences, List<ResourceAssemblyInfo> resourceAssemblies)
        {
            ProjectPath = projectPath;
            Name = name;
            Version = version;
            OutputName = outputName;
            _dependencyReferences = dependencyReferences ?? new List<ReferenceInfo>();
            _resourceAssemblies = resourceAssemblies ?? new List<ResourceAssemblyInfo>();
        }

        public static SingleProjectInfo Create(string projectPath, string name, string fileExtension, string version, ITaskItem[] satelliteAssemblies)
        {
            List<ResourceAssemblyInfo> resourceAssemblies = new List<ResourceAssemblyInfo>();

            foreach (ITaskItem satelliteAssembly in satelliteAssemblies)
            {
                string culture = satelliteAssembly.GetMetadata(MetadataKeys.Culture);
                string relativePath = satelliteAssembly.GetMetadata(MetadataKeys.TargetPath);

                resourceAssemblies.Add(new ResourceAssemblyInfo(culture, relativePath));
            }

            string outputName = name + fileExtension;
            return new SingleProjectInfo(projectPath, name, version, outputName, dependencyReferences: null, resourceAssemblies: resourceAssemblies);
        }

        public static Dictionary<string, SingleProjectInfo> CreateProjectReferenceInfos(
            IEnumerable<ITaskItem> referencePaths,
            IEnumerable<ITaskItem> referenceDependencyPaths,
            IEnumerable<ITaskItem> referenceSatellitePaths)
        {
            Dictionary<string, SingleProjectInfo> projectReferences = new Dictionary<string, SingleProjectInfo>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<ITaskItem> projectReferencePaths = referencePaths
                .Where(r => IsProjectReference(r));

            foreach (ITaskItem projectReferencePath in projectReferencePaths)
            {
                string sourceProjectFile = projectReferencePath.GetMetadata(MetadataKeys.MSBuildSourceProjectFile);

                if (string.IsNullOrEmpty(sourceProjectFile))
                {
                    throw new BuildErrorException(Strings.MissingItemMetadata, MetadataKeys.MSBuildSourceProjectFile, "ReferencePath", projectReferencePath.ItemSpec);
                }

                string outputName = Path.GetFileName(projectReferencePath.ItemSpec);
                string name = Path.GetFileNameWithoutExtension(outputName);
                string version = null; // it isn't possible to know the version from the MSBuild info.
                                       // The version will be retrieved from the project assets file.

                projectReferences.Add(
                    sourceProjectFile,
                    new SingleProjectInfo(sourceProjectFile, name, version, outputName, dependencyReferences: null, resourceAssemblies: null));
            }

            //  Include direct references of referenced projects, but only if they are CopyLocal
            IEnumerable<ITaskItem> projectReferenceDependencyPaths = referenceDependencyPaths
                .Where(r => IsProjectReference(r) &&
                            MSBuildUtilities.ConvertStringToBool(r.GetMetadata(MetadataKeys.CopyLocal)));

            foreach (ITaskItem projectReferenceDependencyPath in projectReferenceDependencyPaths)
            {
                string sourceProjectFile = projectReferenceDependencyPath.GetMetadata(MetadataKeys.MSBuildSourceProjectFile);

                if (string.IsNullOrEmpty(sourceProjectFile))
                {
                    throw new BuildErrorException(Strings.MissingItemMetadata, MetadataKeys.MSBuildSourceProjectFile, "ReferenceDependencyPath", projectReferenceDependencyPath.ItemSpec);
                }

                SingleProjectInfo referenceProjectInfo;
                if (projectReferences.TryGetValue(sourceProjectFile, out referenceProjectInfo))
                {
                    ReferenceInfo dependencyReferenceInfo = ReferenceInfo.CreateReferenceInfo(projectReferenceDependencyPath);
                    referenceProjectInfo._dependencyReferences.Add(dependencyReferenceInfo);
                }
            }

            IEnumerable<ITaskItem> projectReferenceSatellitePaths = referenceSatellitePaths.Where(r => IsProjectReference(r));

            foreach (ITaskItem projectReferenceSatellitePath in projectReferenceSatellitePaths)
            {
                string sourceProjectFile = projectReferenceSatellitePath.GetMetadata(MetadataKeys.MSBuildSourceProjectFile);

                if (string.IsNullOrEmpty(sourceProjectFile))
                {
                    throw new BuildErrorException(Strings.MissingItemMetadata, MetadataKeys.MSBuildSourceProjectFile, "ReferenceSatellitePath", projectReferenceSatellitePath.ItemSpec);
                }

                SingleProjectInfo referenceProjectInfo;
                if (projectReferences.TryGetValue(sourceProjectFile, out referenceProjectInfo))
                {
                    string originalItemSpec = projectReferenceSatellitePath.GetMetadata(MetadataKeys.OriginalItemSpec);

                    if (!string.IsNullOrEmpty(originalItemSpec))
                    {
                        ReferenceInfo referenceInfo = referenceProjectInfo._dependencyReferences.SingleOrDefault(r => r.FullPath.Equals(originalItemSpec));

                        if (referenceInfo is null)
                        {
                            // We only want to add the reference satellite path if it isn't already covered by a dependency

                            ResourceAssemblyInfo resourceAssemblyInfo =
                                ResourceAssemblyInfo.CreateFromReferenceSatellitePath(projectReferenceSatellitePath);
                            referenceProjectInfo._resourceAssemblies.Add(resourceAssemblyInfo);
                        }
                    }
                }
            }

            return projectReferences;
        }

        private static bool IsProjectReference(ITaskItem i)
        {
            return string.Equals(i.GetMetadata(MetadataKeys.ReferenceSourceTarget), "ProjectReference", StringComparison.OrdinalIgnoreCase);
        }
    }
}
