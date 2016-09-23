// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using  System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class ProjectDependencyComparer : IEqualityComparer<ProjectDependency>
    {
        public bool Equals(ProjectDependency one, ProjectDependency two)
        {
                return StringComparer.OrdinalIgnoreCase
                                    .Equals(one.ProjectFilePath, two.ProjectFilePath);
        }

        public int GetHashCode(ProjectDependency item)
        {
                return StringComparer.OrdinalIgnoreCase
                                    .GetHashCode(item.ProjectFilePath);
        }
    }
}
