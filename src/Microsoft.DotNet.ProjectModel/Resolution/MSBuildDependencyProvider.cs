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
    public class MSBuildDependencyProvider
    {
        private readonly Project _rootProject;
        private readonly Func<string, Project> _projectResolver;

        public MSBuildDependencyProvider(Project rootProject, Func<string, Project> projectResolver)
        {
            _rootProject = rootProject;
            _projectResolver = projectResolver;
        }

        public MSBuildProjectDescription GetDescription(NuGetFramework targetFramework,
                                                        LockFileLibrary projectLibrary,
                                                        LockFileTargetLibrary targetLibrary,
                                                        bool isDesignTime)
        {
            // During design time fragment file could be missing. When fragment file is missing none of the
            // assets can be found but it is acceptable during design time.
            var compatible = targetLibrary.FrameworkAssemblies.Any() ||
                             targetLibrary.CompileTimeAssemblies.Any() ||
                             targetLibrary.RuntimeAssemblies.Any() ||
                             isDesignTime;

            var dependencies = new List<ProjectLibraryDependency>(
                targetLibrary.Dependencies.Count + targetLibrary.FrameworkAssemblies.Count);
            PopulateDependencies(dependencies, targetLibrary, targetFramework);

            var msbuildProjectFilePath = GetMSBuildProjectFilePath(projectLibrary);
            var msbuildProjectDirectoryPath = Path.GetDirectoryName(msbuildProjectFilePath);

            var exists = Directory.Exists(msbuildProjectDirectoryPath);

            var projectFile = projectLibrary.Path == null ? null : _projectResolver(projectLibrary.Path);

            var msbuildPackageDescription = new MSBuildProjectDescription(
                msbuildProjectDirectoryPath,
                msbuildProjectFilePath,
                projectLibrary,
                targetLibrary,
                projectFile,
                dependencies,
                compatible,
                resolved: compatible && exists);

            return msbuildPackageDescription;
        }

        private string GetMSBuildProjectFilePath(LockFileLibrary projectLibrary)
        {
            if (_rootProject == null)
            {
                throw new InvalidOperationException("Root xproj project does not exist. Cannot compute the path of its referenced csproj projects.");
            }

            var rootProjectPath = Path.GetDirectoryName(_rootProject.ProjectFilePath);
            var msbuildProjectFilePath = Path.Combine(rootProjectPath, projectLibrary.MSBuildProject);

            return Path.GetFullPath(msbuildProjectFilePath);
        }

        private void PopulateDependencies(
            List<ProjectLibraryDependency> dependencies,
            LockFileTargetLibrary targetLibrary,
            NuGetFramework targetFramework)
        {
            foreach (var dependency in targetLibrary.Dependencies)
            {
                dependencies.Add(new ProjectLibraryDependency
                {
                    LibraryRange = new LibraryRange(dependency.Id, dependency.VersionRange, LibraryDependencyTarget.All)
                });
            }

            if (!targetFramework.IsPackageBased)
            {
                // Only add framework assemblies for non-package based frameworks.
                foreach (var frameworkAssembly in targetLibrary.FrameworkAssemblies)
                {
                    dependencies.Add(new ProjectLibraryDependency
                    {
                        LibraryRange = new LibraryRange(frameworkAssembly, LibraryDependencyTarget.Reference)
                    });
                }
            }
        }

        public static bool IsMSBuildProjectLibrary(LockFileLibrary projectLibrary)
        {
            var msbuildProjectPath = projectLibrary.MSBuildProject;
            if (msbuildProjectPath == null)
            {
                return false;
            }

            var extension = Path.GetExtension(msbuildProjectPath);

            return !string.Equals(extension, ".xproj", StringComparison.OrdinalIgnoreCase);
        }
    }
}
