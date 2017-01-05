// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Sln.Internal;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools.Common
{
    public static class SlnProjectExtensions
    {
        public static IList<string> GetSolutionFoldersFromProject(this SlnProject project)
        {
            var currentDirString = $".{Path.DirectorySeparatorChar}";

            var directoryPath = Path.GetDirectoryName(project.FilePath);
            if (directoryPath.StartsWith(currentDirString))
            {
                directoryPath = directoryPath.Substring(currentDirString.Length);
            }

            return directoryPath.StartsWith("..")
                ? new List<string>()
                : new List<string>(directoryPath.Split(Path.DirectorySeparatorChar));
        }
    }
}
