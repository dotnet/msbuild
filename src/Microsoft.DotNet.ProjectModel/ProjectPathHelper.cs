// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.DotNet.ProjectModel
{
    public static class ProjectPathHelper
    {
        public static string NormalizeProjectDirectoryPath(string path)
        {
            string fullPath = Path.GetFullPath(path);

            if (IsProjectFilePath(fullPath))
            {
                return Path.GetDirectoryName(fullPath);
            }
            else if (IsDirectoryContainingProjectFile(fullPath))
            {
                return fullPath;
            }

            return null;
        }

        public static string NormalizeProjectFilePath(string path)
        {
            if (!path.EndsWith(Project.FileName))
            {
                path = Path.Combine(path, Project.FileName);
            }

            return Path.GetFullPath(path);
        }

        private static bool IsProjectFilePath(string path)
        {
            return File.Exists(path) &&
                string.Equals(Path.GetFileName(path), Project.FileName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDirectoryContainingProjectFile(string path)
        {
            return Directory.Exists(path) && File.Exists(Path.Combine(path, Project.FileName));
        }
    }
}
