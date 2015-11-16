// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Compiler
{
    internal static class ResourcePathUtility
    {
        public static string GetResourceName(string projectFolder, string resourcePath)
        {
            // If the file is outside of the project folder, we are assuming it is directly in the root
            // otherwise, keep the folders that are inside the project
            return PathUtility.IsChildOfDirectory(projectFolder, resourcePath) ?
                PathUtility.GetRelativePath(projectFolder, resourcePath) :
                Path.GetFileName(resourcePath);
        }

        public static bool IsResxResourceFile(string fileName)
        {
            var ext = Path.GetExtension(fileName);

            return
                string.Equals(ext, ".resx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".restext", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".resources", StringComparison.OrdinalIgnoreCase);
        }
    }
}