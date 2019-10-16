// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal static partial class Interop
    {
        internal static readonly bool RunningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        static Interop()
        {
            if (RunningOnWindows)
            {
                PreloadWindowsLibrary("hostfxr.dll");
            }
        }

        // MSBuild SDK resolvers are required to be AnyCPU, but we have a native dependency and .NETFramework does not
        // have a built-in facility for dynamically loading user native dlls for the appropriate platform. We therefore 
        // preload the version with the correct architecture (from a corresponding sub-folder relative to us) on static
        // construction so that subsequent P/Invokes can find it.
        private static void PreloadWindowsLibrary(string dllFileName)
        {
            string basePath = Path.GetDirectoryName(typeof(Interop).Assembly.Location);
            string architecture = IntPtr.Size == 8 ? "x64" : "x86";
            string dllPath = Path.Combine(basePath, architecture, dllFileName);

            // return value is intentionally ignored as we let the subsequent P/Invokes fail naturally.
            LoadLibraryExW(dllPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        }

        // lpFileName passed to LoadLibraryEx must be a full path.
        private const int LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, int dwFlags);


        [Flags]
        internal enum hostfxr_resolve_sdk2_flags_t : int
        {
            disallow_prerelease = 0x1,
        }

        internal enum hostfxr_resolve_sdk2_result_key_t : int
        {
            resolved_sdk_dir = 0,
            global_json_path = 1,
        }

        internal static class Windows
        {
            private const CharSet UTF16 = CharSet.Unicode;

            [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = UTF16)]
            internal delegate void hostfxr_resolve_sdk2_result_fn(
                hostfxr_resolve_sdk2_result_key_t key,
                string value);

            [DllImport("hostfxr", CharSet = UTF16, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int hostfxr_resolve_sdk2(
                string exe_dir,
                string working_dir,
                hostfxr_resolve_sdk2_flags_t flags,
                hostfxr_resolve_sdk2_result_fn result);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = UTF16)]
            internal delegate void hostfxr_get_available_sdks_result_fn(
                int sdk_count,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
                string[] sdk_dirs);

            [DllImport("hostfxr", CharSet = UTF16, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int hostfxr_get_available_sdks(
                string exe_dir,
                hostfxr_get_available_sdks_result_fn result);
        }

        internal static class Unix
        {
            // Ansi marshaling on Unix is actually UTF8
            private const CharSet UTF8 = CharSet.Ansi;
            private static string PtrToStringUTF8(IntPtr ptr) => Marshal.PtrToStringAnsi(ptr);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = UTF8)]
            internal delegate void hostfxr_resolve_sdk2_result_fn(
                hostfxr_resolve_sdk2_result_key_t key,
                string value);

            [DllImport("hostfxr", CharSet = UTF8, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int hostfxr_resolve_sdk2(
                string exe_dir,
                string working_dir,
                hostfxr_resolve_sdk2_flags_t flags,
                hostfxr_resolve_sdk2_result_fn result);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = UTF8)]
            internal delegate void hostfxr_get_available_sdks_result_fn(
                int sdk_count,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
                string[] sdk_dirs);

            [DllImport("hostfxr", CharSet = UTF8, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int hostfxr_get_available_sdks(
                string exe_dir,
                hostfxr_get_available_sdks_result_fn result);

            [DllImport("libc", CharSet = UTF8, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr realpath(string path, IntPtr buffer);

            [DllImport("libc", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
            private static extern void free(IntPtr ptr);

            internal static string realpath(string path)
            {
                var ptr = realpath(path, IntPtr.Zero);
                var result = PtrToStringUTF8(ptr);
                free(ptr);
                return result;
            }
        }
    }
}
