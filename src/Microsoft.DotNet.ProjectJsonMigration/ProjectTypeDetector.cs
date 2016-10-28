// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license info

using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Internal.ProjectModel;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public static class ProjectTypeDetector
    {
        public static bool TryDetectProjectType(string projectDirectory, out string projectType)
        {
            string projectJsonFile = Path.Combine(projectDirectory, "project.json");
            if (!File.Exists(projectJsonFile))
            {
                projectType = null;
                return false;
            }

            if (IsWebProject(projectJsonFile))
            {
                projectType = "web";
                return true;
            }

            projectType = null;
            return false;
        }

        private static bool IsWebProject(string projectJsonFile)
        {
            Project project;
            if (ProjectReader.TryGetProject(projectJsonFile, out project))
            {
                if(project.IsTestProject)
                {
                    return false;
                }

                foreach (var tf in project.GetTargetFrameworks())
                {
                    if (tf.CompilerOptions.EmitEntryPoint.GetValueOrDefault()
                        && HasAnyPackageContainingName(tf, ".AspNetCore."))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasAnyPackageContainingName(TargetFrameworkInformation tf, string nameSegment)
        {
            return tf.Dependencies.Any(x => x.Name.IndexOf(nameSegment, StringComparison.OrdinalIgnoreCase) > -1);
        }
    }
}