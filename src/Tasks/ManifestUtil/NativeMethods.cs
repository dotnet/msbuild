﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal static class NativeMethods
    {
        public const UInt32 LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        public static readonly IntPtr RT_MANIFEST = new IntPtr(24);
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibraryExW(string strFileName, IntPtr hFile, UInt32 ulFlags);
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int SetDllDirectoryW(string strPathName);
        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern bool FreeLibrary(IntPtr hModule);
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindResource(IntPtr hModule, IntPtr pName, IntPtr pType);
        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResource);
        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern uint SizeofResource(IntPtr hModule, IntPtr hResource);
        [DllImport("Kernel32.dll")]
        public static extern IntPtr LockResource(IntPtr hGlobal);
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int EnumResourceNames(IntPtr hModule, IntPtr pType, EnumResNameProc enumFunc, IntPtr param);
        public delegate bool EnumResNameProc(IntPtr hModule, IntPtr pType, IntPtr pName, IntPtr param);

        public enum RegKind
        {
            RegKind_Default = 0,
            RegKind_Register = 1,
            RegKind_None = 2
        }
        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void LoadTypeLibEx(string strTypeLibName, RegKind regKind, [MarshalAs(UnmanagedType.Interface)] out object typeLib);

        [DllImport("sfc.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern int SfcIsFileProtected(IntPtr RpcHandle, string ProtFileName);

        [DllImport("mscorwks.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.IUnknown)]
        public static extern object GetAssemblyIdentityFromFile([In, MarshalAs(UnmanagedType.LPWStr)] string filePath, [In] ref Guid riid);
    }
}
