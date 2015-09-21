// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Shared
{
    internal static partial class EnvironmentUtilities
    {
        public static bool Is64BitProcess => Marshal.SizeOf<IntPtr>() == 8;

        //  Copied from .NET Framework source for Environment.Is64BitOperatingSystem (along with Win32Native APIs)
        public static bool Is64BitOperatingSystem
        {
            get
            {
                bool isWow64; // WinXP SP2+ and Win2k3 SP1+
                return Win32Native.DoesWin32MethodExist(Win32Native.KERNEL32, "IsWow64Process")
                    && Win32Native.IsWow64Process(Win32Native.GetCurrentProcess(), out isWow64)
                    && isWow64;
            }
        }

        private static class Win32Native
        {
            internal const String KERNEL32 = "kernel32.dll";

            [DllImport(KERNEL32, CharSet = NativeMethodsShared.AutoOrUnicode, SetLastError = true)]
            internal static extern IntPtr GetCurrentProcess();

            // Note - do NOT use this to call methods.  Use P/Invoke, which will
            // do much better things w.r.t. marshaling, pinning memory, security 
            // stuff, better interactions with thread aborts, etc.  This is used
            // solely by DoesWin32MethodExist for avoiding try/catch EntryPointNotFoundException
            // in scenarios where an OS Version check is insufficient
            [DllImport(KERNEL32, CharSet = CharSet.Ansi, BestFitMapping = false, SetLastError = true, ExactSpelling = true)]
            private static extern IntPtr GetProcAddress(IntPtr hModule, String methodName);

            [DllImport(KERNEL32, CharSet = NativeMethodsShared.AutoOrUnicode, BestFitMapping = false, SetLastError = true)]
            private static extern IntPtr GetModuleHandle(String moduleName);

            internal static bool DoesWin32MethodExist(String moduleName, String methodName)
            {
                // GetModuleHandle does not increment the module's ref count, so we don't need to call FreeLibrary.
                IntPtr hModule = Win32Native.GetModuleHandle(moduleName);
                if (hModule == IntPtr.Zero)
                {
                    //BCLDebug.Assert(hModule != IntPtr.Zero, "GetModuleHandle failed.  Dll isn't loaded?");
                    return false;
                }
                IntPtr functionPointer = Win32Native.GetProcAddress(hModule, methodName);
                return (functionPointer != IntPtr.Zero);
            }

            // There is no need to call CloseProcess or to use a SafeHandle if you get the handle
            // using GetCurrentProcess as it returns a pseudohandle
            [DllImport(KERNEL32, SetLastError = true, CallingConvention = CallingConvention.Winapi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool IsWow64Process(
                       [In]
                       IntPtr hSourceProcessHandle,
                       [Out, MarshalAs(UnmanagedType.Bool)]
                       out bool isWow64);
        }
    }
}
