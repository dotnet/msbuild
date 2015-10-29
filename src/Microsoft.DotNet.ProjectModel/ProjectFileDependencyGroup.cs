// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel
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