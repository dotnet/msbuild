// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
#if FEATURE_WINDOWSINTEROP
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.LibraryLoader;
#endif

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal class EmbeddedManifestReader
    {
        private Stream _manifest;

#if FEATURE_WINDOWSINTEROP
        // The Win32 RT_MANIFEST resource type and the application-manifest resource id (1).
        private const ushort RT_MANIFEST = 24;
        private const nint Id1 = 1;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate BOOL EnumResNameDelegate(HMODULE hModule, PCWSTR lpType, PWSTR lpName, nint lParam);

        private unsafe EmbeddedManifestReader(string path)
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return;
            }

            HMODULE hModule = default;
            try
            {
                hModule = PInvoke.LoadLibraryEx(path, LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_AS_DATAFILE);
                if (hModule.IsNull)
                {
                    return;
                }

                // EnumResourceNames takes an unmanaged stdcall callback. Bridge a managed delegate to a
                // function pointer (works on both .NET Framework and .NET Core) and keep it alive across the call.
                EnumResNameDelegate callback = EnumResNameCallback;
                var pCallback = (delegate* unmanaged[Stdcall]<HMODULE, PCWSTR, PWSTR, nint, BOOL>)Marshal.GetFunctionPointerForDelegate(callback);
                PInvoke.EnumResourceNames(hModule, new PCWSTR((char*)RT_MANIFEST), pCallback, 0);
                GC.KeepAlive(callback);
            }
            finally
            {
                if (!hModule.IsNull)
                {
                    PInvoke.FreeLibrary(hModule);
                }
            }
        }

        [SupportedOSPlatform("windows6.1")]
        private unsafe BOOL EnumResNameCallback(HMODULE hModule, PCWSTR lpType, PWSTR lpName, nint lParam)
        {
            if ((nint)lpName.Value != Id1)
            {
                return false; // only look for resources with ID=1
            }
            HRSRC hResInfo = PInvoke.FindResource(hModule, lpName, (PCWSTR)(char*)RT_MANIFEST);
            if (hResInfo.IsNull)
            {
                return false; // continue looking
            }
            HGLOBAL hResource = PInvoke.LoadResource(hModule, hResInfo);
            void* pData = PInvoke.LockResource(hResource);
            uint bufsize = PInvoke.SizeofResource(hModule, hResInfo);
            var buffer = new byte[bufsize];

            Marshal.Copy((IntPtr)pData, buffer, 0, buffer.Length);
            _manifest = new MemoryStream(buffer, false);
            return false; // found what we are looking for
        }
#else
        // Reading embedded Win32 manifest resources requires Windows interop, which is unavailable in
        // source build. There is nothing to read here, so _manifest stays null and Read returns null.
        private EmbeddedManifestReader(string path)
        {
        }
#endif

        public static Stream Read(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!path.EndsWith(".manifest", StringComparison.Ordinal) && !path.EndsWith(".dll", StringComparison.Ordinal))
            {
                // Everything that does not end with .dll or .manifest is not a valid native assembly (this includes
                //    EXEs with ID1 manifest)
                return null;
            }

            int t1 = Environment.TickCount;
            EmbeddedManifestReader r = new EmbeddedManifestReader(path);
            Util.WriteLog($"EmbeddedManifestReader.Read t={Environment.TickCount - t1}");
            return r._manifest;
        }
    }
}
