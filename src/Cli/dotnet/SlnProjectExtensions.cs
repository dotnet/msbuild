// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Sln.Internal;

namespace Microsoft.DotNet.Tools.Common
{
    internal static class SlnProjectExtensions
    {
        public static string GetFullSolutionFolderPath(this SlnProject slnProject)
        {
            var slnFile = slnProject.ParentFile;
            var nestedProjects = slnFile.Sections
                .GetOrCreateSection("NestedProjects", SlnSectionType.PreProcess)
                .Properties;
            var solutionFolders = slnFile.Projects
                .GetProjectsByType(ProjectTypeGuids.SolutionFolderGuid)
                .ToArray();

            string path = slnProject.Name;
            string id = slnProject.Id;

            // If the nested projects contains this project's id then it has a parent
            // Traverse from the project to each parent prepending the solution folder to the path
            while (nestedProjects.ContainsKey(id))
            {
                id = nestedProjects[id];

                string solutionFolderPath = solutionFolders.Single(p => p.Id == id).FilePath;
                path = Path.Combine(solutionFolderPath, path);
            }

            return path;
        }
    }
}
