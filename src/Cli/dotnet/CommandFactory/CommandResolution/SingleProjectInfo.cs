// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.CommandFactory
{
    internal class SingleProjectInfo
    {
        public string Name { get; }
        public string Version { get; }

        public IEnumerable<ResourceAssemblyInfo> ResourceAssemblies { get; }

        public SingleProjectInfo(string name, string version, IEnumerable<ResourceAssemblyInfo> resourceAssemblies)
        {
            Name = name;
            Version = version;
            ResourceAssemblies = resourceAssemblies;
        }

        public string GetOutputName()
        {
            return $"{Name}.dll";
        }
    }
}
