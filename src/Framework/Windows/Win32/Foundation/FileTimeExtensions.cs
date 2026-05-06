// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace System;

/// <summary>
/// Extension members for <see cref="FILETIME"/>.
/// </summary>
internal static class FileTimeExtensions
{
    extension(FILETIME fileTime)
    {
        /// <summary>
        /// Converts the <see cref="FILETIME.dwHighDateTime"/> and <see cref="FILETIME.dwLowDateTime"/>
        /// fields into a single 64-bit value representing the number of 100-nanosecond intervals
        /// since January 1, 1601 (UTC).
        /// </summary>
        internal long ToLong() => ((long)(uint)fileTime.dwHighDateTime << 32) | (long)(uint)fileTime.dwLowDateTime;
    }
}
