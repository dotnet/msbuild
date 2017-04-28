// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// NOTE: Currently, only the NET46 build ships (with Visual Studio/desktop msbuild), 
// but the netstandard1.3 adaptation here acts a proof-of-concept for cross-platform 
// portability of the underlying hostfxr API and gives us build and test coverage 
// on non-Windows machines.
#if NETSTANDARD1_3

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal static partial class Interop
    {
        internal static readonly bool s_runningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static int hostfxr_resolve_sdk(string exe_dir, string working_dir, [Out] StringBuilder buffer, int buffer_size)
        {
            // hostfxr string encoding is platform -specific so dispatch to the 
            // appropriately annotated P/Invoke for the current platform.
            return s_runningOnWindows
                ? windows_hostfxr_resolve_sdk(exe_dir, working_dir, buffer, buffer_size)
                : unix_hostfxr_resolve_sdk(exe_dir, working_dir, buffer, buffer_size);
        }

        [DllImport("hostfxr", EntryPoint = nameof(hostfxr_resolve_sdk), CharSet = CharSet.Unicode, ExactSpelling=true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int windows_hostfxr_resolve_sdk(string exe_dir, string working_dir, [Out] StringBuilder buffer, int buffer_size);

        // CharSet.Ansi is UTF8 on Unix
        [DllImport("hostfxr", EntryPoint = nameof(hostfxr_resolve_sdk), CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
        private static extern int unix_hostfxr_resolve_sdk(string exe_dir, string working_dir, [Out] StringBuilder buffer, int buffer_size);
    }
}

#endif // NETSTANDARD1_3