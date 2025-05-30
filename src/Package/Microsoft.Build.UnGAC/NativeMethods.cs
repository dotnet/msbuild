// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

#nullable disable

namespace Microsoft.Build.UnGAC
{
    // See: https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/fusion/iassemblycache-interface
    [ComImport]
    [Guid("E707DCDE-D1CD-11D2-BAB9-00C04F8ECEAE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAssemblyCache
    {
        [PreserveSig]
        uint UninstallAssembly(uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] string pszAssemblyName, IntPtr pRefData, ref ulong pulDisposition);
    }

    public static class NativeMethods
    {
        [DllImport("fusion.dll")]
        internal static extern uint CreateAssemblyCache(out IAssemblyCache ppAsmCache, int dwReserved);
    }
}
