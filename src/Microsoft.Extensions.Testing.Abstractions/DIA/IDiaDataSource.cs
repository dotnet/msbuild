// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace dia2
{
    [Guid("79F1BB5F-B66E-48E5-B6A9-1545C323CA3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IDiaDataSource
    {
        [DispId(1)]
        string lastError
        {

            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        void loadDataFromPdb([MarshalAs(UnmanagedType.LPWStr)] [In] string pdbPath);

        void loadAndValidateDataFromPdb([MarshalAs(UnmanagedType.LPWStr)] [In] string pdbPath, [In] ref Guid pcsig70, [In] uint sig, [In] uint age);

        void loadDataForExe([MarshalAs(UnmanagedType.LPWStr)] [In] string executable, [MarshalAs(UnmanagedType.LPWStr)] [In] string searchPath, [MarshalAs(UnmanagedType.IUnknown)] [In] object pCallback);

        void loadDataFromIStream([MarshalAs(UnmanagedType.Interface)] [In] IStream pIStream);

        void openSession([MarshalAs(UnmanagedType.Interface)] out IDiaSession ppSession);

        void loadDataFromCodeViewInfo([MarshalAs(UnmanagedType.LPWStr)] [In] string executable, [MarshalAs(UnmanagedType.LPWStr)] [In] string searchPath, [In] uint cbCvInfo, [In] ref byte pbCvInfo, [MarshalAs(UnmanagedType.IUnknown)] [In] object pCallback);

        void loadDataFromMiscInfo([MarshalAs(UnmanagedType.LPWStr)] [In] string executable, [MarshalAs(UnmanagedType.LPWStr)] [In] string searchPath, [In] uint timeStampExe, [In] uint timeStampDbg, [In] uint sizeOfExe, [In] uint cbMiscInfo, [In] ref byte pbMiscInfo, [MarshalAs(UnmanagedType.IUnknown)] [In] object pCallback);
    }
}
