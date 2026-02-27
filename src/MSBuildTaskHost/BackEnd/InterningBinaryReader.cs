// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Build.TaskHost.Utilities;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.TaskHost.BackEnd;

/// <summary>
/// Replacement for BinaryReader which attempts to intern the strings read by ReadString.
/// </summary>
internal sealed class InterningBinaryReader : BinaryReader
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
    private readonly Buffer _buffer;

    /// <summary>
    /// The decoder used to translate from UTF8 (or whatever).
    /// </summary>
    private readonly Decoder _decoder;

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
    /// Taken from ndp\clr\src\bcl\system\io\binaryreader.cs-ReadString().
    /// </summary>
    public override string ReadString()
    {
        char[]? resultBuffer = null;
        try
        {
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
                return string.Empty;
            }

            char[] charBuffer = _buffer.CharBuffer;
            do
            {
                readLength = ((stringLength - currPos) > MaxCharsBuffer) ? MaxCharsBuffer : (stringLength - currPos);

                byte[]? rawBuffer = null;
                int rawPosition = 0;

                if (BaseStream is MemoryStream memoryStream)
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
                        ErrorUtilities.ThrowInternalError($"From calculating based on the memorystream, about to read n = {n}. length = {length}, rawPosition = {rawPosition}, readLength = {readLength}, stringLength = {stringLength}, currPos = {currPos}.");
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
                        ErrorUtilities.ThrowInternalError($"From getting the length out of BaseStream.Read directly, about to read n = {n}. readLength = {readLength}, stringLength = {stringLength}, currPos = {currPos}");
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
            Debug.Fail(e.ToString());
            throw;
        }
    }

    /// <summary>
    /// A shared buffer to avoid extra allocations in InterningBinaryReader.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for managing the lifetime of the returned buffer and for passing it to <see cref="Create"/>.
    /// </remarks>
    internal static BinaryReaderFactory CreateSharedBuffer()
        => new Buffer();

    /// <summary>
    /// Holds the preallocated buffer.
    /// </summary>
    private sealed class Buffer : BinaryReaderFactory
    {
        /// <summary>
        /// Gets the char buffer.
        /// </summary>
        internal char[] CharBuffer
            => field ??= new char[MaxCharsBuffer];

        /// <summary>
        /// Gets the byte buffer.
        /// </summary>
        internal byte[] ByteBuffer
            => field ??= new byte[Encoding.UTF8.GetMaxByteCount(MaxCharsBuffer)];

        public override BinaryReader Create(Stream stream)
            => new InterningBinaryReader(stream, buffer: this);
    }
}
