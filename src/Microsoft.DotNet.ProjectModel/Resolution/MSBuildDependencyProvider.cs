// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;

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
                                                        LockFileProjectLibrary projectLibrary,
                                                        LockFileTargetLibrary targetLibrary,
                                                        bool isDesignTime)
        {
            // During design time fragment file could be missing. When fragment file is missing none of the
            // assets can be found but it is acceptable during design time.
            var compatible = targetLibrary.FrameworkAssemblies.Any() ||
                             targetLibrary.CompileTimeAssemblies.Any() ||
                             targetLibrary.RuntimeAssemblies.Any() ||
                             isDesignTime;

            var dependencies = new List<LibraryRange>(targetLibrary.Dependencies.Count + targetLibrary.FrameworkAssemblies.Count);
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

        private string GetMSBuildProjectFilePath(LockFileProjectLibrary projectLibrary)
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
            List<LibraryRange> dependencies,
            LockFileTargetLibrary targetLibrary,
            NuGetFramework targetFramework)
        {
            foreach (var dependency in targetLibrary.Dependencies)
            {
                dependencies.Add(new LibraryRange(
                    dependency.Id,
                    dependency.VersionRange,
                    LibraryType.Unspecified,
                    LibraryDependencyType.Default));
            }

            if (!targetFramework.IsPackageBased())
            {
                // Only add framework assemblies for non-package based frameworks.
                foreach (var frameworkAssembly in targetLibrary.FrameworkAssemblies)
                {
                    dependencies.Add(new LibraryRange(
                        frameworkAssembly,
                        LibraryType.ReferenceAssembly,
                        LibraryDependencyType.Default));
                }
            }
        }

        public static bool IsMSBuildProjectLibrary(LockFileProjectLibrary projectLibrary)
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
