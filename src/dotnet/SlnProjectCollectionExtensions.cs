// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Sln.Internal;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Common
{
    public static class SlnProjectCollectionExtensions
    {
        public static HashSet<string> GetReferencedSolutionFolders(this SlnProjectCollection projects)
        {
            var referencedSolutionFolders = new HashSet<string>();

            var solutionFolderProjects = projects
                .Where(p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid)
                .ToList();

            if (solutionFolderProjects.Any())
            {
                var nonSolutionFolderProjects = projects
                    .Where(p => p.TypeGuid != ProjectTypeGuids.SolutionFolderGuid)
                    .ToList();

                foreach (var project in nonSolutionFolderProjects)
                {
                    var solutionFolders = project.GetSolutionFoldersFromProject();
                    foreach (var solutionFolder in solutionFolders)
                    {
                        if (!referencedSolutionFolders.Contains(solutionFolder))
                        {
                            referencedSolutionFolders.Add(solutionFolder);
                        }
                    }
                }
            }

            return referencedSolutionFolders;
        }
    }
}
