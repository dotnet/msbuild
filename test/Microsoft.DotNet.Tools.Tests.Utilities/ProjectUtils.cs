// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class ProjectUtils
    {
        public static string GetProjectJson(string testRoot, string project)
        {
            // We assume that the project name same as the directory name with contains the project.json
            // We can do better here by using ProjectReader to get the correct project name
            string projectPath = Directory.GetFiles(testRoot, "project.json", SearchOption.AllDirectories)
                                          .FirstOrDefault(pj => Directory.GetParent(pj).Name.Equals(project));

            if (string.IsNullOrEmpty(projectPath))
            {
                throw new Exception($"Cannot file project '{project}' in '{testRoot}'");
            }

            return projectPath;
        }
    }
}
