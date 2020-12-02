// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Microsoft.Build.BuildEngine
{
    internal static class NativeMethods
    {
        internal static readonly IntPtr NullPtr = IntPtr.Zero;
        internal static readonly IntPtr InvalidHandle = new IntPtr(-1);

        internal const uint PAGE_READWRITE = 0x04;
        internal const uint FILE_MAP_ALL_ACCESS = 0x000F0000 |
                                                    0x0001 |
                                                    0x0002 |
                                                    0x0004 |
                                                    0x0008 |
                                                    0x0010;
        internal const uint NORMAL_PRIORITY_CLASS = 0x0020;
        internal const uint CREATE_NO_WINDOW      = 0x08000000;
        internal const Int32 STARTF_USESTDHANDLES = 0x00000100;
        internal const uint PAGE_SIZE = 4096;
        internal const int SECURITY_DESCRIPTOR_REVISION = 1;
        internal const int ERROR_SUCCESS          = 0;

        internal const string  ADMINONLYSDDL      = "D:" +                    //Discretionary ACL
                                                    "(A;OICI;GA;;;BA)" +       //Allow full control to administrators
                                                    "(A;OICI;GA;;;SY)";         //Allow full control to System

        [DllImport("advapi32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CheckTokenMembership
        (
            IntPtr TokenHandle,
            IntPtr SidToCheck,
            [Out, MarshalAs(UnmanagedType.Bool)]
            out bool IsMember
        );

        [DllImport("advapi32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AllocateAndInitializeSid
        (
            IntPtr siaNtAuthority, 
            byte nSubAuthorityCount, 
            int dwSubAuthority0, int dwSubAuthority1, 
            int dwSubAuthority2, int dwSubAuthority3, 
            int dwSubAuthority4, int dwSubAuthority5, 
            int dwSubAuthority6, int dwSubAuthority7, 
            out IntPtr pSid
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern IntPtr FreeSid(IntPtr pSid);

        /// <summary>
        ///  Checks to see if the process is running as administrator or not
        /// </summary>
        /// <returns></returns>
        internal static bool IsUserAdministrator()
        {
            int SECURITY_BUILTIN_DOMAIN_RID = 0x00000020;
            int DOMAIN_ALIAS_RID_ADMINS  = 0x00000220;
            IntPtr pNtAuthority = Marshal.AllocHGlobal(6);
            Marshal.WriteInt32(pNtAuthority, 0, 0);
            Marshal.WriteByte(pNtAuthority, 4, 0);
            Marshal.WriteByte(pNtAuthority, 5, 5);
            IntPtr psidRidGroup;
            bool bRet = AllocateAndInitializeSid(pNtAuthority, 2, SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS, 0, 0, 0, 0, 0, 0, out psidRidGroup);
            try
            {
                if (bRet)
                {
                    if (!CheckTokenMembership(NullPtr, psidRidGroup, out bRet))
                    {
                        bRet = false;
                    }
                }
            }
            finally
            {
                FreeSid(psidRidGroup);
            }

            return bRet;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeSecurityDescriptor(IntPtr pSecurityDescriptor, int dwRevision);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetSecurityDescriptorDacl
        (
            IntPtr pSecurityDescriptor,
            [In, MarshalAs(UnmanagedType.Bool)]
            bool bDaclPresent,
            IntPtr pDacl,
            [In, MarshalAs(UnmanagedType.Bool)]
            bool bDaclDefaulted
        );

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFileMapping
        (
            IntPtr hFile,
            IntPtr lpFileMappingAttributes,
            uint flProtect,
            uint dwMaximumSizeHigh,
            uint dwMaximumSizeLow,
            string lpName
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr MapViewOfFile
        (
            SafeFileHandle handle,
            uint dwDesiredAccess,
            uint dwFileOffsetHigh,
            uint dwFileOffsetLow,
            IntPtr dwNumberOfBytesToMap // use IntPtr here because the size will need to change based on processor architecture (32 vs 64bit)
        );


        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnmapViewOfFile
        (
            IntPtr lpBaseAddress
        );

        [DllImport("kernel32.dll",  CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcess
        (
            string lpApplicationName,
            string lpCommandLine, 
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            [In, MarshalAs(UnmanagedType.Bool)]
            bool bInheritHandles,
            uint dwCreationFlags, 
            IntPtr lpEnvironment, 
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("advapi32",  CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor
        (
            string StringSecurityDescriptor,
            uint SDRevision,
            ref IntPtr SecurityDescriptor,
            ref uint SecurityDescriptorSize
        );

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            internal Int32 cb;
            internal string lpReserved;
            internal string lpDesktop;
            internal string lpTitle;
            internal Int32 dwX;
            internal Int32 dwY;
            internal Int32 dwXSize;
            internal Int32 dwYSize;
            internal Int32 dwXCountChars;
            internal Int32 dwYCountChars;
            internal Int32 dwFillAttribute;
            internal Int32 dwFlags;
            internal Int16 wShowWindow;
            internal Int16 cbReserved2;
            internal IntPtr lpReserved2;
            internal IntPtr hStdInput;
            internal IntPtr hStdOutput;
            internal IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_DESCRIPTOR
        {
            public byte Revision;
            public byte Sbz1;
            public short Control;
            public int Owner;  // void*
            public int Group;  // void*
            public int Sacl;   // ACL*
            public int Dacl;   // ACL*
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            IntPtr hProcess;
            IntPtr hThread;
            int dwProcessId;
            int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }
    }
}

