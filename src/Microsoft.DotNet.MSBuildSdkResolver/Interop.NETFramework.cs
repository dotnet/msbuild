// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET46

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal static partial class Interop
    {
        internal static readonly bool RunningOnWindows = true;

        static Interop()
        {
            PreloadLibrary("hostfxr.dll");
        }

        // MSBuild SDK resolvers are required to be AnyCPU, but we have a native dependency and .NETFramework does not
        // have a built-in facility for dynamically loading user native dlls for the appropriate platform. We therefore 
        // preload the version with the correct architecture (from a corresponding sub-folder relative to us) on static
        // construction so that subsequent P/Invokes can find it.
        private static void PreloadLibrary(string dllFileName)
        {
            string basePath = Path.GetDirectoryName(typeof(Interop).Assembly.Location);
            string architecture = IntPtr.Size == 8 ? "x64" : "x86";
            string dllPath = Path.Combine(basePath, architecture, dllFileName);

            // return value is intentially ignored as we let the subsequent P/Invokes fail naturally.
            LoadLibraryExW(dllPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        }

        // lpFileName passed to LoadLibraryEx must be a full path.
        private const int LOAD_WITH_ALTERED_SEARCH_PATH = 0x8;

        [DllImport("kernel32", CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("hostfxr", CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int hostfxr_resolve_sdk(string exe_dir, string working_dir, [Out] StringBuilder buffer, int buffer_size);
    }
}

#endif // NET46