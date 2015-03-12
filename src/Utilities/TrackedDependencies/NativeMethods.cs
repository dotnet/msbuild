// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;

namespace Microsoft.Build.Utilities
{
    internal static partial class NativeMethods
    {
        private const int MAX_PATH = 260;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, BestFitMapping = false)]
        internal static extern bool GetFileAttributesEx(String name, int fileInfoLevel, ref WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);

        /// <summary>
        /// Get the last write time in utc of the fullpath to the file. 
        /// If the file does not exist, then DateTime.MinValue is returned
        /// </summary>
        /// <param name="fullPath">Full path to the file in the filesystem</param>
        /// <returns></returns>
        internal static DateTime GetLastWriteTimeUtc(string fullPath)
        {
            DateTime fileModifiedTime = DateTime.MinValue;
            WIN32_FILE_ATTRIBUTE_DATA data = new WIN32_FILE_ATTRIBUTE_DATA();
            bool success = false;

            success = NativeMethods.GetFileAttributesEx(fullPath, 0, ref data);
            if (success)
            {
                long dt = ((long)(data.ftLastWriteTimeHigh) << 32) | ((long)data.ftLastWriteTimeLow);
                fileModifiedTime = DateTime.FromFileTimeUtc(dt);
            }

            return fileModifiedTime;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WIN32_FILE_ATTRIBUTE_DATA
    {
        internal int fileAttributes;
        internal uint ftCreationTimeLow;
        internal uint ftCreationTimeHigh;
        internal uint ftLastAccessTimeLow;
        internal uint ftLastAccessTimeHigh;
        internal uint ftLastWriteTimeLow;
        internal uint ftLastWriteTimeHigh;
        internal uint fileSizeHigh;
        internal uint fileSizeLow;
    }
}
