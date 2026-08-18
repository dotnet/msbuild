// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Handle for a FindFirstFileW enumeration. Owns the FindClose call on Dispose.
    /// </summary>
    internal sealed class SafeFindFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        // Default ctor is used internally by SetHandle below; we don't want the P/Invoke
        // marshaller path here anymore since SafeFindFileHandle is constructed manually
        // from the HANDLE returned by PInvoke.FindFirstFile.
        internal SafeFindFileHandle(IntPtr handle)
            : base(ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <nodoc/>
        [System.Runtime.Versioning.SupportedOSPlatform("windows5.1.2600")]
        protected override bool ReleaseHandle()
        {
            return PInvoke.FindClose(new HANDLE(handle));
        }
    }
}
