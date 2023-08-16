// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// UrlMonTypeLib.IInternetSecurityManager
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.Utils
{
    [ComImport]
    [Guid("79EAC9EE-BAF9-11CE-8C82-00AA004BA90B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComConversionLoss]
    internal interface IInternetSecurityManager
    {
        void SetSecuritySite([In][MarshalAs(UnmanagedType.Interface)] IInternetSecurityMgrSite pSite);

        void GetSecuritySite([MarshalAs(UnmanagedType.Interface)] out IInternetSecurityMgrSite ppSite);

        void MapUrlToZone([In][MarshalAs(UnmanagedType.LPWStr)] string pwszUrl, out int pdwZone, [In] int dwFlags);

        void GetSecurityId([In][MarshalAs(UnmanagedType.LPWStr)] string pwszUrl, out byte pbSecurityId, [In][Out] ref int pcbSecurityId, [In][ComAliasName("UrlMonTypeLib.ULONG_PTR")] int dwReserved);

        void ProcessUrlAction([In][MarshalAs(UnmanagedType.LPWStr)] string pwszUrl, [In] int dwAction, out byte pPolicy, [In] int cbPolicy, [In] ref byte pContext, [In] int cbContext, [In] int dwFlags, [In] int dwReserved);

        void QueryCustomPolicy([In][MarshalAs(UnmanagedType.LPWStr)] string pwszUrl, [In][ComAliasName("UrlMonTypeLib.GUID")] ref GUID guidKey, [Out] IntPtr ppPolicy, out int pcbPolicy, [In] ref byte pContext, [In] int cbContext, [In] int dwReserved);

        void SetZoneMapping([In] int dwZone, [In][MarshalAs(UnmanagedType.LPWStr)] string lpszPattern, [In] int dwFlags);

        void GetZoneMappings([In] int dwZone, [MarshalAs(UnmanagedType.Interface)] out IEnumString ppenumString, [In] int dwFlags);
    }

    // UrlMonTypeLib.IInternetSecurityMgrSite
    [ComImport]
    [ComConversionLoss]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("79EAC9ED-BAF9-11CE-8C82-00AA004BA90B")]
    interface IInternetSecurityMgrSite
    {
        void GetWindow([Out][ComAliasName("UrlMonTypeLib.wireHWND")] IntPtr phwnd);

        void EnableModeless([In] int fEnable);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct GUID
    {
        public int Data1;

        public ushort Data2;

        public ushort Data3;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Data4;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00000101-0000-0000-C000-000000000046")]
    interface IEnumString
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        void RemoteNext([In] int celt, [MarshalAs(UnmanagedType.LPWStr)] out string rgelt, out int pceltFetched);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void Skip([In] int celt);

        [MethodImpl(MethodImplOptions.InternalCall)]
        void Reset();

        [MethodImpl(MethodImplOptions.InternalCall)]
        void Clone([MarshalAs(UnmanagedType.Interface)] out IEnumString ppenum);
    }
}
