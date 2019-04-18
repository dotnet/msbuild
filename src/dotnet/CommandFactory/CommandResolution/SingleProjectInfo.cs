// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

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
