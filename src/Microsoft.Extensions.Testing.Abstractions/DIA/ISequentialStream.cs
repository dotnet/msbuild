// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace dia2
{
    [Guid("0C733A30-2A1C-11CE-ADE5-00AA0044773D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface ISequentialStream
    {

        void RemoteRead([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] pv, int cb, out uint pcbRead);

        void RemoteWrite([In] ref byte pv, [In] uint cb, out uint pcbWritten);
    }
}
