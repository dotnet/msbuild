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
        private readonly Func<string, Project> _projectResolver;

        public MSBuildDependencyProvider(Func<string, Project> projectResolver)
        {
            _projectResolver = projectResolver;
        }

        public MSBuildProjectDescription GetDescription(NuGetFramework targetFramework, LockFileProjectLibrary projectLibrary, LockFileTargetLibrary targetLibrary)
        {
            var compatible = targetLibrary.FrameworkAssemblies.Any() ||
                             targetLibrary.CompileTimeAssemblies.Any() ||
                             targetLibrary.RuntimeAssemblies.Any();

            var dependencies = new List<LibraryRange>(targetLibrary.Dependencies.Count + targetLibrary.FrameworkAssemblies.Count);
            PopulateDependencies(dependencies, targetLibrary, targetFramework);

            var path = Path.GetDirectoryName(Path.GetFullPath(projectLibrary.MSBuildProject));
            var exists = Directory.Exists(path);

            var projectFile = projectLibrary.Path == null ? null : _projectResolver(projectLibrary.Path);

            var msbuildPackageDescription = new MSBuildProjectDescription(
                path,
                projectLibrary,
                targetLibrary,
                projectFile,
                dependencies,
                compatible,
                resolved: compatible && exists);

            return msbuildPackageDescription;
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

            if (!targetFramework.IsPackageBased)
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
