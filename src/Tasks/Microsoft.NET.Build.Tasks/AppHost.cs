// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Embeds the App Name into the AppHost.exe
    /// </summary>
    public static class AppHost
    {
        private const string _placeHolder = "c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2"; //hash value embedded in default apphost executable
        private readonly static byte[] _bytesToSearch = Encoding.UTF8.GetBytes(_placeHolder);

        /// <summary>
        /// Create an AppHost with embedded configuration of app binary location
        /// </summary>
        /// <param name="appHostSourceFilePath">The path of Apphost template, which has the place holder</param>
        /// <param name="appHostDestinationFilePath">The destination path for desired location to place, including the file name</param>
        /// <param name="appBinaryFilePath">Full path to app binary or relative path to the result apphost file</param>
        /// <param name="overwriteExisting">If override the file existed in <paramref name="appHostDestinationFilePath"/></param>
        public static void Create(
            string appHostSourceFilePath,
            string appHostDestinationFilePath,
            string appBinaryFilePath,
            bool overwriteExisting = false)
        {
            var hostExtension = Path.GetExtension(appHostSourceFilePath);
            var appbaseName = Path.GetFileNameWithoutExtension(appBinaryFilePath);
            var bytesToWrite = Encoding.UTF8.GetBytes(appBinaryFilePath);
            var destinationDirectory = new FileInfo(appHostDestinationFilePath).Directory.FullName;

            if (bytesToWrite.Length > 1024)
            {
                throw new BuildErrorException(Strings.FileNameIsTooLong, appBinaryFilePath);
            }

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Copy AppHostSourcePath to ModifiedAppHostPath so it inherits the same attributes\permissions.
            File.Copy(appHostSourceFilePath, appHostDestinationFilePath, overwriteExisting);

            // Re-write ModifiedAppHostPath with the proper contents.
            using (var memoryMappedFile = MemoryMappedFile.CreateFromFile(appHostDestinationFilePath))
            {
                using (MemoryMappedViewAccessor accessor = memoryMappedFile.CreateViewAccessor())
                {
                    SearchAndReplace(accessor, _bytesToSearch, bytesToWrite, appHostSourceFilePath);
                }
            }

            // Memory-mapped write does not updating last write time
            File.SetLastWriteTimeUtc(appHostDestinationFilePath, DateTime.UtcNow);
        }

        // See: https://en.wikipedia.org/wiki/Knuth%E2%80%93Morris%E2%80%93Pratt_algorithm
        private static int[] ComputeKMPFailureFunction(byte[] pattern)
        {
            int[] table = new int[pattern.Length];
            if (pattern.Length >= 1)
            {
                table[0] = -1;
            }
            if (pattern.Length >= 2)
            {
                table[1] = 0;
            }

            int pos = 2;
            int cnd = 0;
            while (pos < pattern.Length)
            {
                if (pattern[pos - 1] == pattern[cnd])
                {
                    table[pos] = cnd + 1;
                    cnd++;
                    pos++;
                }
                else if (cnd > 0)
                {
                    cnd = table[cnd];
                }
                else
                {
                    table[pos] = 0;
                    pos++;
                }
            }
            return table;
        }

        // See: https://en.wikipedia.org/wiki/Knuth%E2%80%93Morris%E2%80%93Pratt_algorithm
        private static unsafe int KMPSearch(byte[] pattern, byte* bytes, long bytesLength)
        {
            int m = 0;
            int i = 0;
            int[] table = ComputeKMPFailureFunction(pattern);

            while (m + i < bytesLength)
            {
                if (pattern[i] == bytes[m + i])
                {
                    if (i == pattern.Length - 1)
                    {
                        return m;
                    }
                    i++;
                }
                else
                {
                    if (table[i] > -1)
                    {
                        m = m + i - table[i];
                        i = table[i];
                    }
                    else
                    {
                        m++;
                        i = 0;
                    }
                }
            }

            return -1;
        }

        private static unsafe void SearchAndReplace(
            MemoryMappedViewAccessor accessor,
            byte[] searchPattern,
            byte[] patternToReplace,
            string appHostSourcePath)
        {
            byte* pointer = null;

            try
            {
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                byte* bytes = pointer + accessor.PointerOffset;

                int position = KMPSearch(searchPattern, bytes, accessor.Capacity);
                if (position < 0)
                {
                    throw new BuildErrorException(Strings.AppHostHasBeenModified, appHostSourcePath, _placeHolder);
                }

                accessor.WriteArray(
                    position: position,
                    array: patternToReplace,
                    offset: 0,
                    count: patternToReplace.Length);

                Pad0(searchPattern, patternToReplace, bytes, position);
            }
            finally
            {
                if (pointer != null)
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }

        private static unsafe void Pad0(byte[] searchPattern, byte[] patternToReplace, byte* bytes, int offset)
        {
            if (patternToReplace.Length < searchPattern.Length)
            {
                for (int i = patternToReplace.Length; i < searchPattern.Length; i++)
                {
                    bytes[i + offset] = 0x0;
                }
            }
        }
    }
}
