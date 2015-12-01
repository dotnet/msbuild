// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace dia2
{
    [Guid("3BFE56B0-390C-4863-9430-1F3D083B7684"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IDiaInputAssemblyFile
    {
        [DispId(1)]
        uint uniqueId
        {

            get;
        }
        [DispId(2)]
        uint index
        {

            get;
        }
        [DispId(3)]
        uint timeStamp
        {

            get;
        }
        [DispId(4)]
        int pdbAvailableAtILMerge
        {

            get;
        }
        [DispId(5)]
        string fileName
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        void get_version([In] uint cbData, out uint pcbData, out byte pbData);
    }
}
