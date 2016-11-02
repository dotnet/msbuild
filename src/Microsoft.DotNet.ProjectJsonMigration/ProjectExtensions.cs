// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license info

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.DotNet.Internal.ProjectModel;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal static class ProjectExtensions
    {
        public static ProjectType GetProjectType(this Project project)
        {
            ProjectType projectType = ProjectType.Library;
            if (project.IsTestProject)
            {
                projectType = ProjectType.Test;
            }
            else if (project.HasEntryPoint())
            {
                if (project.HasDependency(ContainingName(".AspNetCore.")))
                {
                    projectType = ProjectType.Web;
                }
                else
                {
                    projectType = ProjectType.Console;
                }
            }

            return projectType;
        }

        private static bool HasEntryPoint(this Project project)
        {
            return project.GetCompilerOptions(null, "Debug").EmitEntryPoint.GetValueOrDefault();
        }

        private static Func<ProjectLibraryDependency, bool> ContainingName(string nameSegment)
        {
            return x => x.Name.IndexOf(nameSegment, StringComparison.OrdinalIgnoreCase) > -1;
        }

        public static bool HasDependency(this Project project, Func<ProjectLibraryDependency, bool> pred)
        {
            if (HasAnyDependency(project.Dependencies, pred))
            {
                return true;
            }

            foreach (var tf in project.GetTargetFrameworks())
            {
                if(HasAnyDependency(tf.Dependencies, pred))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyDependency(
            IEnumerable<ProjectLibraryDependency> dependencies,
            Func<ProjectLibraryDependency, bool> pred)
        {
            return dependencies.Any(pred);
        }
    }
}