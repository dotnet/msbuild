// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal class SingleProjectInfo
    {
        public string ProjectPath { get; }
        public string Name { get; }
        public string Version { get; }
        public string OutputName { get; }

        private List<ResourceAssemblyInfo> _resourceAssemblies;
        public IEnumerable<ResourceAssemblyInfo> ResourceAssemblies
        {
            get { return _resourceAssemblies; }
        }

        private SingleProjectInfo(string projectPath, string name, string version, string outputName, List<ResourceAssemblyInfo> resourceAssemblies)
        {
            ProjectPath = projectPath;
            Name = name;
            Version = version;
            OutputName = outputName;
            _resourceAssemblies = resourceAssemblies;
        }

        public static SingleProjectInfo Create(string projectPath, string name, string fileExtension, string version, ITaskItem[] satelliteAssemblies)
        {
            List<ResourceAssemblyInfo> resourceAssemblies = new List<ResourceAssemblyInfo>();

            foreach (ITaskItem satelliteAssembly in satelliteAssemblies)
            {
                string culture = satelliteAssembly.GetMetadata("Culture");
                string relativePath = satelliteAssembly.GetMetadata("TargetPath");

                resourceAssemblies.Add(new ResourceAssemblyInfo(culture, relativePath));
            }

            string outputName = name + fileExtension;
            return new SingleProjectInfo(projectPath, name, version, outputName, resourceAssemblies);
        }

        public static Dictionary<string, SingleProjectInfo> CreateProjectReferenceInfos(
            IEnumerable<ITaskItem> referencePaths,
            IEnumerable<ITaskItem> referenceSatellitePaths)
        {
            Dictionary<string, SingleProjectInfo> projectReferences = new Dictionary<string, SingleProjectInfo>();

            IEnumerable<ITaskItem> projectReferencePaths = referencePaths
                .Where(r => string.Equals(r.GetMetadata("ReferenceSourceTarget"), "ProjectReference", StringComparison.OrdinalIgnoreCase));

            foreach (ITaskItem projectReferencePath in projectReferencePaths)
            {
                string sourceProjectFile = projectReferencePath.GetMetadata("MSBuildSourceProjectFile");

                if (string.IsNullOrEmpty(sourceProjectFile))
                {
                    throw new BuildErrorException(Strings.MissingItemMetadata, "MSBuildSourceProjectFile", "ReferencePath", projectReferencePath.ItemSpec);
                }

                string outputName = Path.GetFileName(projectReferencePath.ItemSpec);
                string name = Path.GetFileNameWithoutExtension(outputName);
                string version = null; // it isn't possible to know the version from the MSBuild info.
                                       // The version will be retrieved from the project assets file.

                List<ResourceAssemblyInfo> resourceAssemblies = new List<ResourceAssemblyInfo>();

                projectReferences.Add(
                    sourceProjectFile,
                    new SingleProjectInfo(sourceProjectFile, name, version, outputName, resourceAssemblies));
            }

            IEnumerable<ITaskItem> projectReferenceSatellitePaths = referenceSatellitePaths
                .Where(r => string.Equals(r.GetMetadata("ReferenceSourceTarget"), "ProjectReference", StringComparison.OrdinalIgnoreCase));

            foreach (ITaskItem projectReferenceSatellitePath in projectReferenceSatellitePaths)
            {
                string sourceProjectFile = projectReferenceSatellitePath.GetMetadata("MSBuildSourceProjectFile");

                if (string.IsNullOrEmpty(sourceProjectFile))
                {
                    throw new BuildErrorException(Strings.MissingItemMetadata, "MSBuildSourceProjectFile", "ReferenceSatellitePath", projectReferenceSatellitePath.ItemSpec);
                }

                SingleProjectInfo referenceProjectInfo;
                if (projectReferences.TryGetValue(sourceProjectFile, out referenceProjectInfo))
                {
                    ResourceAssemblyInfo resourceAssemblyInfo =
                        ResourceAssemblyInfo.CreateFromReferenceSatellitePath(projectReferenceSatellitePath);
                    referenceProjectInfo._resourceAssemblies.Add(resourceAssemblyInfo);
                }
            }

            return projectReferences;
        }
    }
}
