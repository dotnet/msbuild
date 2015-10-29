// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Extensions.ProjectModel
{
    public static class ProjectRootResolver
    {
        public static readonly string GlobalFileName = "global.json";
        public static string ResolveRootDirectory(string projectPath)
        {
            var di = new DirectoryInfo(projectPath);

            while (di.Parent != null)
            {
                var globalJsonPath = Path.Combine(di.FullName, GlobalFileName);

                if (File.Exists(globalJsonPath))
                {
                    return di.FullName;
                }

                di = di.Parent;
            }

            // If we don't find any files then make the project folder the root
            return projectPath;
        }
    }
}
