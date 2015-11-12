// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for dealing with console encodings.
    /// </summary>
    internal static class EncodingUtilities
    {
        /// <summary>
        /// Get the current system locale code page, OEM version. OEM code pages are used for console-based input/output
        /// for historical reasons.
        /// </summary>
        static internal Encoding CurrentSystemOemEncoding
        {
            get
            {
                // if we already have it, no need to do it again
                if (s_currentOemEncoding != null)
                    return s_currentOemEncoding;

                // fall back to default ANSI encoding if we have problems
#if FEATURE_ENCODING_DEFAULT
                s_currentOemEncoding = Encoding.Default;
#else
                s_currentOemEncoding = Encoding.UTF8;
#endif
#if FEATURE_ENCODING_DEFAULT
                try
                {
                    // get the current OEM code page
                    s_currentOemEncoding = Encoding.GetEncoding(NativeMethodsShared.GetOEMCP());
                }
                // theoretically, GetEncoding may throw an ArgumentException or a NotSupportedException. This should never
                // really happen, since the code page we pass in has just been returned from the "underlying platform", 
                // so it really should support it. If it ever happens, we'll just fall back to the default encoding.
                // No point in showing any errors to the users, since they most likely wouldn't be actionable.
                catch (ArgumentException ex)
                {
                    Debug.Assert(false, "GetEncoding(default OEM encoding) threw an ArgumentException in EncodingUtilities.CurrentSystemOemEncoding! Please log a bug against MSBuild.", ex.Message);
                }
                catch (NotSupportedException ex)
                {
                    Debug.Assert(false, "GetEncoding(default OEM encoding) threw a NotSupportedException in EncodingUtilities.CurrentSystemOemEncoding! Please log a bug against MSBuild.", ex.Message);
                }
#endif
                return s_currentOemEncoding;
            }
        }

        static private Encoding s_currentOemEncoding;
    }
}
