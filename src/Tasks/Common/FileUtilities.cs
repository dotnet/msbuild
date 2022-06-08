// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.NET.Build.Tasks
{
    static partial class FileUtilities
    {
        public static Version GetFileVersion(string sourcePath)
        {
            if (sourcePath != null)
            {
                var fvi = FileVersionInfo.GetVersionInfo(sourcePath);

                if (fvi != null)
                {
                    return new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
                }
            }

            return null;
        }

        static readonly HashSet<string> s_assemblyExtensions = new HashSet<string>(new[] { ".dll", ".exe", ".winmd" }, StringComparer.OrdinalIgnoreCase);
        public static Version TryGetAssemblyVersion(string sourcePath)
        {
            var extension = Path.GetExtension(sourcePath);

            return s_assemblyExtensions.Contains(extension) ? GetAssemblyVersion(sourcePath) : null;
        }

    }
}
