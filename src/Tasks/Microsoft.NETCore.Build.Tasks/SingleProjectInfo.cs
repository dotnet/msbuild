// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.NETCore.Build.Tasks
{
    public class SingleProjectInfo
    {
        public string Name { get; }
        public string Version { get; }

        public IEnumerable<ResourceAssemblyInfo> ResourceAssemblies { get; }

        private SingleProjectInfo(string name, string version, IEnumerable<ResourceAssemblyInfo> resourceAssemblies)
        {
            Name = name;
            Version = version;
            ResourceAssemblies = resourceAssemblies;
        }

        public static SingleProjectInfo Create(string name, string version, ITaskItem[] satelliteAssemblies)
        {
            List<ResourceAssemblyInfo> resourceAssemblies = new List<ResourceAssemblyInfo>();

            foreach (ITaskItem satelliteAssembly in satelliteAssemblies)
            {
                string culture = satelliteAssembly.GetMetadata("Culture");
                string relativePath = satelliteAssembly.GetMetadata("TargetPath");

                resourceAssemblies.Add(new ResourceAssemblyInfo(culture, relativePath));
            }

            return new SingleProjectInfo(name, version, resourceAssemblies);
        }

        public string GetOutputName()
        {
            return $"{Name}.dll";
        }
    }
}
