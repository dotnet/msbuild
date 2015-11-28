// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public class ProjectDependencyProvider
    {
        public ProjectDescription GetDescription(string name, string path, LockFileTargetLibrary targetLibrary)
        {
            Project project;

            // Can't find a project file with the name so bail
            if (!ProjectReader.TryGetProject(path, out project))
            {
                return new ProjectDescription(name, path);
            }

            return GetDescription(targetLibrary.TargetFramework, project);
        }

        public ProjectDescription GetDescription(NuGetFramework targetFramework, Project project)
        {
            // This never returns null
            var targetFrameworkInfo = project.GetTargetFramework(targetFramework);
            var targetFrameworkDependencies = new List<LibraryRange>(targetFrameworkInfo.Dependencies);

            if (targetFramework != null && targetFramework.IsDesktop())
            {
                targetFrameworkDependencies.Add(new LibraryRange("mscorlib", LibraryType.ReferenceAssembly));

                targetFrameworkDependencies.Add(new LibraryRange("System", LibraryType.ReferenceAssembly));

                if (targetFramework.Version >= new Version(3, 5))
                {
                    targetFrameworkDependencies.Add(new LibraryRange("System.Core", LibraryType.ReferenceAssembly));

                    if (targetFramework.Version >= new Version(4, 0))
                    {
                        targetFrameworkDependencies.Add(new LibraryRange("Microsoft.CSharp", LibraryType.ReferenceAssembly));
                    }
                }
            }

            var dependencies = project.Dependencies.Concat(targetFrameworkDependencies).ToList();

            // Mark the library as unresolved if there were specified frameworks
            // and none of them resolved
            bool unresolved = targetFrameworkInfo.FrameworkName == null;

            return new ProjectDescription(
                new LibraryRange(project.Name, LibraryType.Unspecified),
                project,
                dependencies,
                targetFrameworkInfo,
                !unresolved);
        }
    }
}
