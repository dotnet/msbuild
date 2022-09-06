// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.NET.Build.Tasks
{
    static partial class FileUtilities
    {
        static int S_IRUSR = 256;
        static int S_IWUSR = 128;
        static int S_IXUSR = 64;
        static int S_IRWXU = S_IRUSR | S_IWUSR | S_IXUSR; // 700 (octal) Permissions 

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

        [DllImport("libc", SetLastError = true)]
        private static extern int mkdir(string pathname, int mode);
        public static string CreateTempSubdirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mkdir(path, S_IRWXU);
            }
            else
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }
    }
}
