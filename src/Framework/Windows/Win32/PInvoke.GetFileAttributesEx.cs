// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace Windows.Win32;

internal partial class PInvoke
{
    /// <inheritdoc cref="GetFileAttributesEx(PCWSTR, GET_FILEEX_INFO_LEVELS, void*)"/>
    [SupportedOSPlatform("windows6.1")]
    internal static unsafe bool GetFileAttributesEx(string name, out WIN32_FILE_ATTRIBUTE_DATA lpFileInformation)
    {
        fixed (WIN32_FILE_ATTRIBUTE_DATA* fileInfoPtr = &lpFileInformation)
        {
            return GetFileAttributesEx(name, GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, fileInfoPtr);
        }
    }
}
