// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public class ProjectDependencyProvider
    {
        private Func<string, Project> _resolveProject;

        public ProjectDependencyProvider(Func<string, Project> projectCacheResolver)
        {
            _resolveProject = projectCacheResolver;
        }

        public ProjectDescription GetDescription(string name,
                                                 string path,
                                                 LockFileTargetLibrary targetLibrary,
                                                 Func<string, Project> projectCacheResolver)
        {
            var project = _resolveProject(Path.GetDirectoryName(path));
            if (project != null)
            {
                return GetDescription(NuGetFramework.Parse(targetLibrary.Framework), project, targetLibrary);
            }
            else
            {
                return new ProjectDescription(name, path);
            }
        }

        public ProjectDescription GetDescription(string name, string path, LockFileTargetLibrary targetLibrary)
        {
            return GetDescription(name, path, targetLibrary, projectCacheResolver: null);
        }

        public ProjectDescription GetDescription(NuGetFramework targetFramework, Project project, LockFileTargetLibrary targetLibrary)
        {
            // This never returns null
            var targetFrameworkInfo = project.GetTargetFramework(targetFramework);
            var dependencies = new List<ProjectLibraryDependency>(targetFrameworkInfo.Dependencies);

            // Add all of the project's dependencies
            dependencies.AddRange(project.Dependencies);

            if (targetFramework != null && targetFramework.IsDesktop())
            {
                AddIfMissing(dependencies, "mscorlib");
                AddIfMissing(dependencies, "System");

                if (targetFramework.Version >= new Version(3, 5))
                {
                    AddIfMissing(dependencies, "System.Core");
                    if (targetFramework.Version >= new Version(4, 0))
                    {
                        AddIfMissing(dependencies, "Microsoft.CSharp");
                    }
                }
            }

            if (targetLibrary != null)
            {
                // The lock file entry might have a filtered set of dependencies
                var lockFileDependencies = targetLibrary.Dependencies.ToDictionary(
                    d => d.Id,
                    StringComparer.OrdinalIgnoreCase);

                // Remove all non-framework dependencies that don't appear in the lock file entry
                dependencies.RemoveAll(m =>
                    !lockFileDependencies.ContainsKey(m.Name) &&
                    m.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference);
            }

            // Mark the library as unresolved if there were specified frameworks
            // and none of them resolved
            bool unresolved = targetFrameworkInfo.FrameworkName == null;

            return new ProjectDescription(
                new LibraryRange(project.Name, LibraryDependencyTarget.All),
                project,
                dependencies,
                targetFrameworkInfo,
                !unresolved);
        }

        private static void AddIfMissing(List<ProjectLibraryDependency> dependencies, string dependencyName)
        {
            if (!dependencies.Any(dep => string.Equals(dep.Name, dependencyName, StringComparison.OrdinalIgnoreCase)))
            {
                dependencies.Add(new ProjectLibraryDependency {
                    LibraryRange = new LibraryRange(dependencyName, LibraryDependencyTarget.Reference)
                });
            }
        }
    }
}
