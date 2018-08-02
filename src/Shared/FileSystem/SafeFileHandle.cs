// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Handle for a volume iteration as returned by WindowsNative.FindFirstVolumeW />
    /// </summary>
    internal sealed class SafeFindFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Private constructor for the PInvoke marshaller.
        /// </summary>
        private SafeFindFileHandle()
            : base(ownsHandle: true)
        {
        }

        /// <nodoc/>
        protected override bool ReleaseHandle()
        {
            return WindowsNative.FindClose(handle);
        }
    }
}
