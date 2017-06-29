// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Embeds the App Name into the AppHost.exe  
    /// </summary>
    public class EmbedAppNameInHost : TaskBase
    {
        [Required]
        public string AppHostSourcePath { get; set; }

        [Required]
        public string AppHostDestinationDirectoryPath { get; set; }

        [Required]
        public string AppBinaryName { get; set; }

        [Output]
        public string ModifiedAppHostPath { get; set; }

        private static string placeHolder = "c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2"; //hash value embedded in default apphost executable
        private static byte[] bytesToSearch = Encoding.UTF8.GetBytes(placeHolder);
        protected override void ExecuteCore()
        {
            var hostExtension = Path.GetExtension(AppHostSourcePath);
            var appbaseName = Path.GetFileNameWithoutExtension(AppBinaryName);
            var bytesToWrite = Encoding.UTF8.GetBytes(AppBinaryName);
            var destinationDirectory = Path.GetFullPath(AppHostDestinationDirectoryPath);
            ModifiedAppHostPath = Path.Combine(destinationDirectory, $"{appbaseName}{hostExtension}");

            if ( File.Exists(ModifiedAppHostPath))
            {
                //We have already done the required modification to apphost.exe
                return;
            }

            if (bytesToWrite.Length > 1024)
            {
                throw new BuildErrorException(Strings.FileNameIsTooLong, AppBinaryName);
            }

            var array = File.ReadAllBytes(AppHostSourcePath);

            SearchAndReplace(array, bytesToSearch, bytesToWrite, AppHostSourcePath);

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Copy AppHostSourcePath to ModifiedAppHostPath so it inherits the same attributes\permissions.
            File.Copy(AppHostSourcePath, ModifiedAppHostPath);

            // Re-write ModifiedAppHostPath with the proper contents.
            using (FileStream fs = new FileStream(ModifiedAppHostPath, FileMode.Truncate, FileAccess.ReadWrite, FileShare.Read))
            {
                fs.Write(array, 0, array.Length);
            }
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
        private static int KMPSearch(byte[] pattern, byte[] bytes)
        {
            int m = 0;
            int i = 0;
            int[] table = ComputeKMPFailureFunction(pattern);

            while (m + i < bytes.Length)
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

        private static  void SearchAndReplace(byte[] array, byte[] searchPattern, byte[] patternToReplace, string appHostSourcePath)
        {
            int offset = KMPSearch(searchPattern, array);
            if (offset < 0)
            {
                throw new BuildErrorException(Strings.AppHostHasBeenModified, appHostSourcePath, placeHolder);
            }

            patternToReplace.CopyTo(array, offset);

            if (patternToReplace.Length < searchPattern.Length)
            {
                for (int i = patternToReplace.Length; i < searchPattern.Length; i++)
                {
                    array[i + offset] = 0x0;
                }
            }
        }
    }
}
