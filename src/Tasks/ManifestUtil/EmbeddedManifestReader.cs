// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    internal class EmbeddedManifestReader
    {
        private static readonly IntPtr s_id1 = new IntPtr(1);
        private Stream _manifest;

        private EmbeddedManifestReader(string path)
        {
            IntPtr hModule = IntPtr.Zero;
            try
            {
                hModule = NativeMethods.LoadLibraryExW(path, IntPtr.Zero, NativeMethods.LOAD_LIBRARY_AS_DATAFILE);
                if (hModule == IntPtr.Zero)
                {
                    return;
                }
                NativeMethods.EnumResNameProc callback = EnumResNameCallback;
                NativeMethods.EnumResourceNames(hModule, NativeMethods.RT_MANIFEST, callback, IntPtr.Zero);
            }
            finally
            {
                if (hModule != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(hModule);
                }
            }
        }

        private bool EnumResNameCallback(IntPtr hModule, IntPtr pType, IntPtr pName, IntPtr param)
        {
            if (pName != s_id1)
            {
                return false; // only look for resources with ID=1
            }
            IntPtr hResInfo = NativeMethods.FindResource(hModule, pName, NativeMethods.RT_MANIFEST);
            if (hResInfo == IntPtr.Zero)
            {
                return false; //continue looking
            }
            IntPtr hResource = NativeMethods.LoadResource(hModule, hResInfo);
            NativeMethods.LockResource(hResource);
            uint bufsize = NativeMethods.SizeofResource(hModule, hResInfo);
            var buffer = new byte[bufsize];

            Marshal.Copy(hResource, buffer, 0, buffer.Length);
            _manifest = new MemoryStream(buffer, false);
            return false; //found what we are looking for
        }

        public static Stream Read(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            if (!path.EndsWith(".manifest", StringComparison.Ordinal) && !path.EndsWith(".dll", StringComparison.Ordinal))
            {
                // Everything that does not end with .dll or .manifest is not a valid native assembly (this includes
                //    EXEs with ID1 manifest)
                return null;
            }

            int t1 = Environment.TickCount;
            EmbeddedManifestReader r = new EmbeddedManifestReader(path);
            Util.WriteLog(String.Format(CultureInfo.CurrentCulture, "EmbeddedManifestReader.Read t={0}", Environment.TickCount - t1));
            return r._manifest;
        }
    }
}
