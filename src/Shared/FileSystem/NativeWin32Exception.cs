// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// A possibly-recoverable exception wrapping a failed native call. The <see cref="Win32Exception.NativeErrorCode" /> captures the
    /// associated recent error code (<see cref="System.Runtime.InteropServices.Marshal.GetLastWin32Error" />). The <see cref="Exception.Message" />
    /// accounts for the native code as well as a human readable portion.
    /// </summary>
    /// <remarks>
    /// This is much like <see cref="Win32Exception"/>, but the message field contains the caller-provided part in addition
    /// to the system-provided message (rather than replacing the system provided message).
    /// </remarks>
    [SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors",
        Justification = "We don't need exceptions to cross AppDomain boundaries.")]
    [Serializable]
    internal sealed class NativeWin32Exception : Win32Exception
    {
        /// <summary>
        /// Creates an exception representing a native failure (with a corresponding Win32 error code).
        /// The exception's <see cref="Exception.Message" /> includes the error code, a system-provided message describing it,
        /// and the provided application-specific message prefix (e.g. "Unable to open log file").
        /// </summary>
        public NativeWin32Exception(int nativeErrorCode, [Localizable(false)] string messagePrefix)
            : base(nativeErrorCode, GetFormattedMessageForNativeErrorCode(nativeErrorCode, messagePrefix))
        {
            // Win32Exception does not initialize HResult but many others like IOException do.
            // In order to have a uniform error checking, initialize HResult using something similar to HRESULT_FROM_WIN32
            HResult = HResultFromWin32(nativeErrorCode);
        }

        /// <summary>
        /// Creates an exception representing a native failure (with a corresponding Win32 error code).
        /// The exception's <see cref="Exception.Message" /> includes the error code and a system-provided message describing it.
        /// </summary>
        public NativeWin32Exception(int nativeErrorCode)
            : this(nativeErrorCode, null)
        {
        }

        /// <summary>
        /// Returns a human readable error string for a native error code, like <c>Native: Can't access the log file (0x5: Access is denied)</c>.
        /// The message prefix (e.g. "Can't access the log file") is optional.
        /// </summary>
        public static string GetFormattedMessageForNativeErrorCode(int nativeErrorCode, [Localizable(false)] string messagePrefix = null)
        {
            string systemMessage = new Win32Exception(nativeErrorCode).Message;
            if (!string.IsNullOrEmpty(messagePrefix))
            {
                return string.Format(CultureInfo.InvariantCulture, "Native: {0} (0x{1:X}: {2})", messagePrefix, nativeErrorCode, systemMessage);
            }
            else
            {
                return string.Format(CultureInfo.InvariantCulture, "Native: 0x{0:X}: {1}", nativeErrorCode, systemMessage);
            }
        }

        /// <summary>
        /// Converts a Win32 error code to HResult
        /// </summary>
        public static int HResultFromWin32(int nativeErrorCode)
        {
            if (nativeErrorCode < 0 || nativeErrorCode > 0xFFFF)
            {
                return nativeErrorCode;
            }

            return unchecked((int)0x80070000) | nativeErrorCode;
        }
    }
}
