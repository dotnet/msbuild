// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFile
    {
        public static readonly int CurrentVersion = 2;
        public static readonly string FileName = "project.lock.json";

        public int Version { get; set; }
        public IList<ProjectFileDependencyGroup> ProjectFileDependencyGroups { get; set; } = new List<ProjectFileDependencyGroup>();
        public IList<LockFilePackageLibrary> PackageLibraries { get; set; } = new List<LockFilePackageLibrary>();
        public IList<LockFileProjectLibrary> ProjectLibraries { get; set; } = new List<LockFileProjectLibrary>();
        public IList<LockFileTarget> Targets { get; set; } = new List<LockFileTarget>();

        public bool IsValidForProject(Project project)
        {
            string message;
            return IsValidForProject(project, out message);
        }

        public bool IsValidForProject(Project project, out string message)
        {
            if (Version != CurrentVersion)
            {
                message = $"The expected lock file version does not match the actual version";
                return false;
            }

            message = $"Dependencies in {Project.FileName} were modified";

            var actualTargetFrameworks = project.GetTargetFrameworks();

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            if (ProjectFileDependencyGroups.Count != actualTargetFrameworks.Count() + 1)
            {
                return false;
            }

            foreach (var group in ProjectFileDependencyGroups)
            {
                IOrderedEnumerable<string> actualDependencies;
                var expectedDependencies = group.Dependencies.OrderBy(x => x);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (group.FrameworkName == null)
                {
                    actualDependencies = project.Dependencies.Select(RenderDependency).OrderBy(x => x);
                }
                else
                {
                    var framework = actualTargetFrameworks
                        .FirstOrDefault(f => Equals(f.FrameworkName, group.FrameworkName));
                    if (framework == null)
                    {
                        return false;
                    }

                    actualDependencies = framework.Dependencies.Select(RenderDependency).OrderBy(x => x);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    return false;
                }
            }

            message = null;
            return true;
        }

        private string RenderDependency(LibraryRange arg)
        {
            var name = arg.Name;

            if (arg.Target == LibraryType.ReferenceAssembly)
            {
                name = $"fx/{name}";
            }

            return $"{name} {VersionUtility.RenderVersion(arg.VersionRange)}";
        }
    }
}