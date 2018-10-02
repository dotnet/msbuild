// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for dealing with encoding.
    /// </summary>
    internal static class EncodingUtilities
    {
        private static Encoding s_currentOemEncoding;

        /// <summary>
        /// Get the current system locale code page, OEM version. OEM code pages are used for console-based input/output
        /// for historical reasons.
        /// </summary>
        internal static Encoding CurrentSystemOemEncoding
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

                try
                {
                    if (NativeMethodsShared.IsWindows)
                    {
#if RUNTIME_TYPE_NETCORE
                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
                        // get the current OEM code page
                        s_currentOemEncoding = Encoding.GetEncoding(NativeMethodsShared.GetOEMCP());
                    }
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

                return s_currentOemEncoding;
            }
        }

        /// <summary>
        /// Checks two encoding types to determine if they are similar to each other (equal or if
        /// the Encoding Name is the same).
        /// </summary>
        /// <param name="encoding1"></param>
        /// <param name="encoding2"></param>
        /// <returns>True if the two Encoding objects are equal or similar.</returns>
        internal static bool SimilarToEncoding(this Encoding encoding1, Encoding encoding2)
        {
            if (encoding1 == null)
                return encoding2 == null;

            if (encoding2 == null)
                return false;

            if (Equals(encoding1, encoding2))
                return true;

            return encoding1.EncodingName == encoding2.EncodingName;
        }

        /// <summary>
        /// Check if an encoding type is UTF8 (with or without BOM).
        /// </summary>
        /// <param name="encoding"></param>
        /// <returns>True if the encoding is UTF8.</returns>
        internal static bool IsUtf8Encoding(this Encoding encoding)
        {
            return SimilarToEncoding(encoding, Encoding.UTF8);
        }

        /// <summary>
        /// Check the first 3 bytes of a stream to determine if it matches the UTF8 preamble.
        /// </summary>
        /// <param name="stream">Steam to check.</param>
        /// <returns>True when the first 3 bytes of the Stream are equal to the UTF8 preamble (BOM).</returns>
        internal static bool StartsWithPreamble(this Stream stream)
        {
            return StartsWithPreamble(stream, Encoding.UTF8.GetPreamble());
        }

        /// <summary>
        /// Check the first 3 bytes of a stream to determine if it matches the given preamble.
        /// </summary>
        /// <param name="stream">Steam to check.</param>
        /// <param name="preamble">Preamble to look for.</param>
        /// <returns>True when the first 3 bytes of the Stream are equal to the preamble.</returns>
        internal static bool StartsWithPreamble(this Stream stream, byte[] preamble)
        {
            if (preamble == null) return false;

            int bytesRead;
            var buffer = new byte[preamble.Length];

            var position = stream.Position;
            if (stream.Position != 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            try
            {
                bytesRead = stream.Read(buffer, 0, preamble.Length);
            }
            finally
            {
                stream.Seek(position, SeekOrigin.Begin);
            }

            // Bytes read and preamble must be the same length and contain the same not contain any differences
            return bytesRead == preamble.Length && !buffer.Where((t, i) => preamble[i] != t).Any();
        }

        /// <summary>
        /// Check the first 3 bytes of a file to determine if it matches the 3-byte UTF8 preamble (BOM).
        /// </summary>
        /// <param name="file">Path to file to check.</param>
        /// <returns>True when the first 3 bytes of the file are equal to the UTF8 BOM.</returns>
        internal static bool FileStartsWithPreamble(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return StartsWithPreamble(stream);
            }
        }
    }
}
