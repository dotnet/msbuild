// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public class MakeRelative : Task
    {
        [Required]
        public string Path1 { get; set; }

        [Required]
        public string Path2 { get; set; }

        public char SeparatorChar { get; set; }

        [Output]
        public ITaskItem RelativePath { get; set; }

        public override bool Execute()
        {
            if (SeparatorChar == default(char))
            {
                SeparatorChar = Path.DirectorySeparatorChar;
            }

            var relativePath = GetRelativePath(Path1, Path2, SeparatorChar);

            RelativePath = ToTaskItem(Path1, Path2, relativePath);

            return true;
        }

        private static TaskItem ToTaskItem(string path1, string path2, string relativePath)
        {
            var framework = new TaskItem();
            framework.ItemSpec = relativePath;

            framework.SetMetadata("Path1", path1);
            framework.SetMetadata("Path2", path2);
            framework.SetMetadata("RelativePath", relativePath);

            return framework;
        }

        private static string GetRelativePath(string path1, string path2, char separator = default(char))
        {

            StringComparison compare;
            if (CurrentPlatform.IsWindows)
            {
                compare = StringComparison.OrdinalIgnoreCase;
                // check if paths are on the same volume
                if (!string.Equals(Path.GetPathRoot(path1), Path.GetPathRoot(path2)))
                {
                    // on different volumes, "relative" path is just Path2
                    return path2;
                }
            }
            else
            {
                compare = StringComparison.Ordinal;
            }

            var index = 0;
            var path1Segments = path1.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var path2Segments = path2.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // if path1 does not end with / it is assumed the end is not a directory
            // we will assume that is isn't a directory by ignoring the last split
            var len1 = path1Segments.Length - 1;
            var len2 = path2Segments.Length;

            // find largest common absolute path between both paths
            var min = Math.Min(len1, len2);
            while (min > index)
            {
                if (!string.Equals(path1Segments[index], path2Segments[index], compare))
                {
                    break;
                }
                // Handle scenarios where folder and file have same name (only if os supports same name for file and directory)
                // e.g. /file/name /file/name/app
                else if ((len1 == index && len2 > index + 1) || (len1 > index && len2 == index + 1))
                {
                    break;
                }
                ++index;
            }

            var path = "";

            // check if path2 ends with a non-directory separator and if path1 has the same non-directory at the end
            if (len1 + 1 == len2 && !string.IsNullOrEmpty(path1Segments[index]) &&
                string.Equals(path1Segments[index], path2Segments[index], compare))
            {
                return path;
            }

            for (var i = index; len1 > i; ++i)
            {
                path += ".." + separator;
            }
            for (var i = index; len2 - 1 > i; ++i)
            {
                path += path2Segments[i] + separator;
            }
            // if path2 doesn't end with an empty string it means it ended with a non-directory name, so we add it back
            if (!string.IsNullOrEmpty(path2Segments[len2 - 1]))
            {
                path += path2Segments[len2 - 1];
            }

            return path;
        }
    }
}
