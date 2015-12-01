// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace dia2
{
    [Guid("AE605CDC-8105-4A23-B710-3259F1E26112"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IDiaInjectedSource
    {
        [DispId(1)]
        uint crc
        {

            get;
        }
        [DispId(2)]
        ulong length
        {

            get;
        }
        [DispId(3)]
        string fileName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(4)]
        string objectFileName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(5)]
        string virtualFilename
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }
        [DispId(6)]
        uint sourceCompression
        {

            get;
        }

        void get_source([In] uint cbData, out uint pcbData, out byte pbData);
    }
}
