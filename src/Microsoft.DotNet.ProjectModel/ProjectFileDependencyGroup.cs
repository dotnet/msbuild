// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectFileDependencyGroup
    {
        public ProjectFileDependencyGroup(NuGetFramework frameworkName, IEnumerable<string> dependencies)
        {
            FrameworkName = frameworkName;
            Dependencies = dependencies;
        }

        public NuGetFramework FrameworkName { get; }

        public IEnumerable<string> Dependencies { get; }
    }
}