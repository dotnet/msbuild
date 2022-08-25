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
        public static extern int mkdir(string pathname, int mode);
        public static string CreateTempPath()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const int S_IRUSR = 256;
                const int S_IWUSR = 128;
                const int S_IXUSR = 64;
                int _700 = S_IRUSR | S_IWUSR | S_IXUSR;
                mkdir(path, _700);
                ResetTempFilePermissions(path);
            }
            else
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public static string CreateTempFile(string tempDirectory, string extension = "")
        {
            if (extension == "")
            {
                extension = Path.GetExtension(Path.GetRandomFileName());
            }
            string fileName = Path.ChangeExtension(Path.Combine(tempDirectory, Path.GetTempFileName()), extension);
            var fstream = File.Create(fileName.ToString());
            fstream.Close();
            ResetTempFilePermissions(fileName);
            return fileName;
        }

        /// <summary>
        ///  
        /// </summary>
        /// <returns>The full path of a newly created temp file with OK permissions.</returns>
        public static string CreateTempFile()
        {
            return Path.GetTempFileName();
        }


        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, int mode);
        [DllImport("libc", SetLastError = true)]
        public static extern int chown(string path, int owner, int group);
        public static void ResetTempFilePermissions(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if(Environment.GetEnvironmentVariable("SUDO_USER") != null)
                {
                    string UID = Environment.GetEnvironmentVariable("SUDO_UID");
                    string GID = Environment.GetEnvironmentVariable("SUDO_GID");
                    chown(path, Int32.Parse(UID), Int32.Parse(GID)); 
                }
                const int S_IRUSR = 256;
		        const int S_IWUSR = 128;
		        const int S_IXUSR = 64;
                int _700 = S_IRUSR | S_IWUSR | S_IXUSR;
                chmod(path, _700);
            }
        }

    }
}
