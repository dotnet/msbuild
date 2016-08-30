// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace NuGet.Legacy
{
    internal static class PathUtility
    {
        public static string GetPathWithForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }

        public static string GetPathWithBackSlashes(string path)
        {
            return path.Replace('/', '\\');
        }

        public static string GetPathWithDirectorySeparator(string path)
        {
            if (Path.DirectorySeparatorChar == '/')
            {
                return GetPathWithForwardSlashes(path);
            }
            else
            {
                return GetPathWithBackSlashes(path);
            }
        }
    }
}