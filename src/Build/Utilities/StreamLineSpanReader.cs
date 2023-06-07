// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Reads lines of text from a <see cref="Stream"/> into <see cref="ReadOnlySpan{T}"/>s for further processing.
/// Allows efficient, low-allocation consumption of textual data from a stream.
/// </summary>
internal sealed class StreamLineSpanReader
{
    private readonly Stream _stream;
    private readonly Decoder _decoder;
    private readonly byte[] _bytes;
    private char[] _chars;

    private int _byteOffset;
    private int _bytesUntil;
    private int _charOffset;
    private int _lineStartOffset;

    public StreamLineSpanReader(Stream stream, Encoding encoding, int byteBufferSize, int charBufferSize)
    {
        _stream = stream;
        _decoder = encoding.GetDecoder();
        _bytes = new byte[byteBufferSize];
        _chars = new char[charBufferSize];
    }

    /// <summary>
    /// Attempts to produce the next line of text from the stream.
    /// </summary>
    /// <remarks>
    /// For performance, the reader internally shares a single character buffer, which <paramref name="line"/>
    /// is backed by. This means that <paramref name="line"/> is only valid until the next call to this method,
    /// after which the previous <paramref name="line"/> value will be in an undefined state.
    /// </remarks>
    /// <param name="line">The line of text that was just read. Must be consumed before the next call to this method.</param>
    /// <returns>
    /// <see langword="true"/> if a line was successfully retrieved, or <see langword="false"/> if the end of
    /// the file has been reached.
    /// </returns>
    public bool TryReadLine(out ReadOnlySpan<char> line)
    {
        // This algorithm is juggling a few stores of data:
        //
        // - The stream, from which incoming bytes are sourced.
        // - The byte array, via which incoming bytes are materialized in linear form.
        // - The char array, into which bytes are decoded and from which we produce spans representing full lines of text.

        do
        {
            if (_byteOffset == _bytesUntil)
            {
                // We have consumed all bytes from the byte buffer.
                // Reset the offset.
                _byteOffset = 0;

                // Attempt to fill the byte buffer from the stream.
                _bytesUntil = _stream.Read(_bytes, 0, _bytes.Length);
            }

            bool completed = false;

            while (!completed)
            {
                UpdateCharBufferIfNecessary();

                _decoder.Convert(
                    bytes: _bytes,
                    byteIndex: _byteOffset,
                    byteCount: _bytesUntil - _byteOffset,
                    chars: _chars,
                    charIndex: _charOffset,
                    charCount: _chars.Length - _charOffset,
                    flush: _bytesUntil == 0, // If no more input bytes, flush decoder's internal buffer.
                    out int bytesUsed,
                    out int charsUsed,
                    out completed);

                _byteOffset += bytesUsed;
                _charOffset += charsUsed;

                if (completed && _bytesUntil == 0 && bytesUsed == 0 && charsUsed == 0 && _lineStartOffset == _charOffset + 1)
                {
                    // We have no more data.
                    line = default;
                    return false;
                }

                // Check whether we've found a full line of text yet.
                int lineEndOffset = Array.IndexOf(_chars, '\n', _lineStartOffset, _charOffset - _lineStartOffset);

                if (lineEndOffset == -1 && completed && _bytesUntil == 0)
                {
                    // We read the last line
                    lineEndOffset = _charOffset;
                }

                if (lineEndOffset != -1)
                {
                    // We found a line!
                    line = _chars.AsSpan().Slice(_lineStartOffset, lineEndOffset - _lineStartOffset);
                    line = line.Trim('\r');

                    // Prepare for the next line.
                    _lineStartOffset = lineEndOffset + 1;

                    return true;
                }
            }
        }
        while (_bytesUntil != 0);

        // We have no more data.
        line = default;
        return false;

        void UpdateCharBufferIfNecessary()
        {
            if (_charOffset == _chars.Length)
            {
                // We hit the end of the character buffer.
                if (_lineStartOffset == 0)
                {
                    // There's no more room at the start of the buffer.
                    // Grow the buffer.
                    char[] larger = new char[_chars.Length * 2];
                    Array.Copy(_chars, larger, _chars.Length);
                    _chars = larger;
                }
                else
                {
                    // There's room at the start of the buffer, so move pending characters earlier.
                    Array.Copy(_chars, _lineStartOffset, _chars, 0, _charOffset - _lineStartOffset);
                    _charOffset -= _lineStartOffset;
                    _lineStartOffset = 0;
                }
            }
        }
    }
}
