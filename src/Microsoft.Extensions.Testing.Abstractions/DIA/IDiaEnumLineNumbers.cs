// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace dia2
{
    [DefaultMember("Item"), Guid("FE30E878-54AC-44F1-81BA-39DE940F6052"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IDiaEnumLineNumbers
    {
        [DispId(1)]
        int count
        {

            get;
        }

        IEnumerator GetEnumerator();

        [return: MarshalAs(UnmanagedType.Interface)]
        IDiaLineNumber Item([In] uint index);

        void Next([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaLineNumber rgelt, out uint pceltFetched);

        void Skip([In] uint celt);

        void Reset();

        void Clone([MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppenum);
    }
}