// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license info

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.DotNet.Internal.ProjectModel;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal static class ProjectTypeDetector
    {
        public static string TryDetectProjectType(Project project)
        {
            string projectType = null;
            if (IsWebProject(project))
            {
                projectType = "web";
            }

            return projectType;
        }

        private static bool IsWebProject(Project project)
        {
            if(project.IsTestProject)
            {
                return false;
            }

            var isExecutable = project.GetCompilerOptions(null, "Debug").EmitEntryPoint.GetValueOrDefault();
            if (isExecutable
                && HasAnyPackageContainingName(project, ".AspNetCore."))
            {
                return true;
            }

            return false;
        }

        private static bool HasAnyPackageContainingName(Project project, string nameSegment)
        {
            var containsPackageName = HasAnyPackageContainingName(
                new ReadOnlyCollection<ProjectLibraryDependency>(project.Dependencies),
                nameSegment);
            foreach (var tf in project.GetTargetFrameworks())
            {
                if(containsPackageName)
                {
                    break;
                }

                containsPackageName = HasAnyPackageContainingName(tf.Dependencies, nameSegment);
            }

            return containsPackageName;
        }

        private static bool HasAnyPackageContainingName(
            IReadOnlyList<ProjectLibraryDependency> dependencies,
            string nameSegment)
        {
            return dependencies.Any(x => x.Name.IndexOf(nameSegment, StringComparison.OrdinalIgnoreCase) > -1);
        }
    }
}