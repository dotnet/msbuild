// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace dia2
{
    [DefaultMember("Item"), Guid("10F3DBD9-664F-4469-B808-9471C7A50538"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IDiaEnumSourceFiles
    {
        [DispId(1)]
        int count
        {

            get;
        }

        IEnumerator GetEnumerator();

        [return: MarshalAs(UnmanagedType.Interface)]
        IDiaSourceFile Item([In] uint index);

        void Next([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaSourceFile rgelt, out uint pceltFetched);

        void Skip([In] uint celt);

        void Reset();

        void Clone([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSourceFiles ppenum);
    }
}