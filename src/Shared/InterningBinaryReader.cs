// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.IO;
using System.Diagnostics;

using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;

using Microsoft.NET.StringTools;

namespace Microsoft.Build
{
    /// <summary>
    /// Replacement for BinaryReader which attempts to intern the strings read by ReadString.
    /// </summary>
    internal class InterningBinaryReader : BinaryReader
    {
        /// <summary>
        /// The maximum size, in bytes, to read at once.
        /// </summary>
#if DEBUG
        private const int MaxCharsBuffer = 10;
#else
        private const int MaxCharsBuffer = 20000;
#endif

        /// <summary>
        /// Shared buffer saves allocating these arrays many times.
        /// </summary>
        private Buffer _buffer;

        /// <summary>
        /// The decoder used to translate from UTF8 (or whatever).
        /// </summary>
        private Decoder _decoder;

        /// <summary>
        /// Comment about constructing.
        /// </summary>
        private InterningBinaryReader(Stream input, Buffer buffer)
            : base(input, Encoding.UTF8)
        {
            if (input == null)
            {
                throw new InvalidOperationException();
            }

            _buffer = buffer;
            _decoder = Encoding.UTF8.GetDecoder();
        }

        /// <summary>
        /// Read a string while checking the string precursor for intern opportunities.
        /// Taken from ndp\clr\src\bcl\system\io\binaryreader.cs-ReadString()
        /// </summary>
        override public String ReadString()
        {
            try
            {
                MemoryStream memoryStream = this.BaseStream as MemoryStream;

                int currPos = 0;
                int n = 0;
                int stringLength;
                int readLength;
                int charsRead = 0;

                // Length of the string in bytes, not chars
                stringLength = Read7BitEncodedInt();
                if (stringLength < 0)
                {
                    throw new IOException();
                }

                if (stringLength == 0)
                {
                    return String.Empty;
                }

                char[] charBuffer = _buffer.CharBuffer;
                char[] resultBuffer = null;
                do
                {
                    readLength = ((stringLength - currPos) > MaxCharsBuffer) ? MaxCharsBuffer : (stringLength - currPos);

                    byte[] rawBuffer = null;
                    int rawPosition = 0;

                    if (memoryStream != null)
                    {
                        // Optimization: we can avoid reading into a byte buffer
                        // and instead read directly from the memorystream's backing buffer
                        rawBuffer = memoryStream.GetBuffer();
                        rawPosition = (int)memoryStream.Position;
                        int length = (int)memoryStream.Length;
                        n = (rawPosition + readLength) < length ? readLength : length - rawPosition;

                        // Attempt to track down an intermittent failure -- n should not ever be negative, but 
                        // we're occasionally seeing it when we do the decoder.GetChars below -- by providing 
                        // a bit more information when we do hit the error, in the place where (by code inspection)
                        // the actual error seems most likely to be occurring. 
                        if (n < 0)
                        {
                            ErrorUtilities.ThrowInternalError("From calculating based on the memorystream, about to read n = {0}. length = {1}, rawPosition = {2}, readLength = {3}, stringLength = {4}, currPos = {5}.", n, length, rawPosition, readLength, stringLength, currPos);
                        }

                        memoryStream.Seek(n, SeekOrigin.Current);
                    }

                    if (rawBuffer == null)
                    {
                        rawBuffer = _buffer.ByteBuffer;
                        rawPosition = 0;
                        n = BaseStream.Read(rawBuffer, 0, readLength);

                        // See above explanation -- the OutOfRange exception may also be coming from our setting of n here ...
                        if (n < 0)
                        {
                            ErrorUtilities.ThrowInternalError("From getting the length out of BaseStream.Read directly, about to read n = {0}. readLength = {1}, stringLength = {2}, currPos = {3}", n, readLength, stringLength, currPos);
                        }
                    }

                    if (n == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    if (currPos == 0 && n == stringLength)
                    {
                        charsRead = _decoder.GetChars(rawBuffer, rawPosition, n, charBuffer, 0);
                        return Strings.WeakIntern(charBuffer.AsSpan(0, charsRead));
                    }

                    resultBuffer ??= new char[stringLength]; // Actual string length in chars may be smaller.
                    charsRead += _decoder.GetChars(rawBuffer, rawPosition, n, resultBuffer, charsRead);

                    currPos += n;
                }
                while (currPos < stringLength);

                return Strings.WeakIntern(resultBuffer.AsSpan(0, charsRead));
            }
            catch (Exception e)
            {
                Debug.Assert(false, e.ToString());
                throw;
            }
        }

        /// <summary>
        /// A shared buffer to avoid extra allocations in InterningBinaryReader.
        /// </summary>
        internal static SharedReadBuffer CreateSharedBuffer()
        {
            return new Buffer();
        }

        /// <summary>
        /// Create a BinaryReader. It will either be an interning reader or standard binary reader
        /// depending on whether the interning reader is possible given the buffer and stream.
        /// </summary>
        internal static BinaryReader Create(Stream stream, SharedReadBuffer sharedBuffer)
        {
            Buffer buffer = (Buffer)sharedBuffer;

            if (buffer == null)
            {
                buffer = new Buffer();
            }

            return new InterningBinaryReader(stream, buffer);
        }

        /// <summary>
        /// Holds thepreallocated buffer. 
        /// </summary>
        private class Buffer : SharedReadBuffer
        {
            /// <summary>
            /// Yes, we are constructing.
            /// </summary>
            internal Buffer()
            {
                this.CharBuffer = new char[MaxCharsBuffer];
                this.ByteBuffer = new byte[Encoding.UTF8.GetMaxByteCount(MaxCharsBuffer)];
            }

            /// <summary>
            /// The char buffer.
            /// </summary>
            internal char[] CharBuffer
            {
                get;
                private set;
            }

            /// <summary>
            /// The byte buffer.
            /// </summary>
            internal byte[] ByteBuffer
            {
                get;
                private set;
            }
        }
    }

    /// <summary>
    /// Opaque holder of shared buffer.
    /// </summary>
    abstract internal class SharedReadBuffer
    {
    }
}
