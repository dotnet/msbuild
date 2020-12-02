// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.Build.Shared.LanguageParser
{
    /// <summary>
    /// A class with string-like semantics mapped over a Stream.
    /// </summary>
    sealed internal class StreamMappedString
    {
        /// <summary>
        /// The raw binary stream that's being read.
        /// </summary>
        private Stream _binaryStream;

        /// <summary>
        /// The reader on top of binaryStream. This is what interprets the encoding.
        /// </summary>
        private StreamReader _reader;

        /// <summary>
        /// When false, try to guess the encoding of binaryStream. When true, force the 
        /// encoding to ANSI.
        /// </summary>
        private bool _forceANSI;

        /// <summary>
        /// The page number that 'currentPage' is pointing to.
        /// </summary>
        private int _currentPageNumber = -1;

        /// <summary>
        /// The final page number of the whole stream.
        /// </summary>
        private int _finalPageNumber = Int32.MaxValue;

        /// <summary>
        /// The number of characters read into currentPage.
        /// </summary>
        private int _charactersRead = 0;

        /// <summary>
        /// The page before currentPage.
        /// </summary>
        private char[] _priorPage = null;

        /// <summary>
        /// The most recently read page.
        /// </summary>
        private char[] _currentPage = null;

        /// <summary>
        /// Count of the total number of pages allocated.
        /// </summary>
        private int _pagesAllocated = 0;

        /// <summary>
        /// Size of pages to use for reading from source file.
        /// </summary>
        private int _pageSize = 0;

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="binaryStream">The raw binary stream that's being read.</param>
        /// <param name="forceANSI">When false, try to guess the encoding of binaryStream. When true, force the encoding to ANSI.</param>
        public StreamMappedString(Stream binaryStream, bool forceANSI)
            : this(binaryStream, forceANSI, /* pageSize */ DefaultPageSize)
        {
        }

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="binaryStream">The raw binary stream that's being read.</param>
        /// <param name="forceANSI">When false, try to guess the encoding of binaryStream. When true, force the encoding to ANSI.</param>
        /// <param name="pageSize">Size of pages to use for reading from source file.</param>
        internal StreamMappedString(Stream binaryStream, bool forceANSI, int pageSize)
        {
            _binaryStream = binaryStream;
            _forceANSI = forceANSI;
            _pageSize = pageSize;
            RestartReader();
        }

        /// <summary>
        /// Restart the stream reader at the beginning.
        /// </summary>
        private void RestartReader()
        {
            _currentPageNumber = -1;
            _charactersRead = 0;
            _priorPage = null;
            _currentPage = null;

            // Reset the stream if we're not at the beginning
            if (_binaryStream.Position != 0)
            {
                _binaryStream.Seek(0, SeekOrigin.Begin);
            }

            if (_forceANSI)
            {
                _reader = new StreamReader // HIGHCHAR: Falling back to ANSI for VB source files.
                    (
                    _binaryStream,
#if FEATURE_ENCODING_DEFAULT
                    Encoding.Default,    // Default means ANSI. 
#else
                    Encoding.ASCII,
#endif
                    false                // If the reader had been able to guess the encoding it would have done so already.
                    );
            }
            else
            {
                Encoding utf8Encoding = new UTF8Encoding(false, true /* throw on illegal bytes */);

                _reader = new StreamReader // HIGHCHAR: VB and C# source files are assumed to be UTF if there are no byte-order marks.
                    (
                        _binaryStream,
                        utf8Encoding,
                        true            // Ask the reader to try to guess the file's encoding.
                    );
            }
        }

        /// <summary>
        /// Get the total number of pages allocated.
        /// </summary>
        public int PagesAllocated
        {
            get { return _pagesAllocated; }
        }

        /// <summary>
        /// The pagesize in characters that will be used if not specified.
        /// </summary>
        public static int DefaultPageSize
        {
            get { return 256; }
        }

        /// <summary>
        /// Return a particular character within the file.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public char GetAt(int offset)
        {
            // Find the page number that contains offset.
            char[] page = GetPage(offset);

            // If null now, then the requested character is out of range.
            if (page == null)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            // Get the relative offset within the buffer.
            int relativeOffset = AbsoluteOffsetToPageOffset(offset);

            // Return the character.
            return page[relativeOffset];
        }

        /// <summary>
        /// Get the page that contains offset. Otherwise, null.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private char[] GetPage(int offset)
        {
            int page = PageFromAbsoluteOffset(offset);

            // Is it earlier than the last page?
            if (page < _currentPageNumber - 1)
            {
                // Restart the stream. Perf hit.
                RestartReader();
            }

            // Read pages until the page is available.
            while (page > _currentPageNumber)
            {
                int originalPageNumber = _currentPageNumber;

                if (!ReadNextPage())
                {
                    break;
                }

                ErrorUtilities.VerifyThrow(originalPageNumber != _currentPageNumber, "Expected a new page.");
            }

            // Is it the current page?
            if (page == _currentPageNumber)
            {
                // If enough bytes were read to satisfy offset then return the buffer.
                if (_charactersRead > AbsoluteOffsetToPageOffset(offset))
                {
                    return _currentPage;
                }

                // Otherwise, null.
                return null;
            }

            // Is it the prior page now?
            if (page == _currentPageNumber - 1)
            {
                return _priorPage;
            }

            // Its out of range.
            return null;
        }

        /// <summary>
        /// Read the next page.
        /// </summary>
        /// <returns></returns>
        private bool ReadNextPage()
        {
            // Is this already known to be the last page?
            if (_currentPageNumber == _finalPageNumber)
            {
                // 0x0d should already be appended.
                return false;
            }

            // If so, read it in to the lastBuffer...
            ReadBlockStripEOF();

            // ...and then swap lastBuffer for buffer.
            SwapPages();
            ++_currentPageNumber;

            // If the number of bytesRead is less than the number requested
            // then this is the last page.
            if (_charactersRead < _pageSize)
            {
                // Mark this as the last page.
                _finalPageNumber = _currentPageNumber;

                // Add a 0xd if the last character is not a newline.
                if (!IsZeroLengthStream() && !TokenChar.IsNewLine(LastCharacterInStream()))
                {
                    AppendCharacterToStream('\xd');
                }
            }

            return _charactersRead > 0;
        }

        /// <summary>
        /// Read characters from the file, and strip out any 1A characters.
        /// </summary>
        private void ReadBlockStripEOF()
        {
            if (_priorPage == null)
            {
                ++_pagesAllocated;
                _priorPage = new char[_pageSize];
            }

            _charactersRead = _reader.ReadBlock(_priorPage, 0, _pageSize);

            for (int i = 0; i < _charactersRead; ++i)
            {
                if (_priorPage[i] == '\x1a')
                {
                    Array.Copy(_priorPage, i + 1, _priorPage, i, _charactersRead - i - 1);
                    _charactersRead += _reader.ReadBlock(_priorPage, _charactersRead - 1, 1);

                    --i;
                    --_charactersRead;
                }
            }
        }

        /// <summary>
        /// Add one character to the end of the stream.
        /// </summary>
        /// <param name="c"></param>
        private void AppendCharacterToStream(char c)
        {
            ErrorUtilities.VerifyThrow(_charactersRead != _pageSize, "Attempt to append to non-last page.");

            _currentPage[_charactersRead] = c;
            ++_charactersRead;
        }

        /// <summary>
        /// Retrieve the last character in the stream.
        /// </summary>
        /// <returns></returns>
        private char LastCharacterInStream()
        {
            char c;
            if (_charactersRead == 0)
            {
                ErrorUtilities.VerifyThrow(_priorPage != null, "There is no last character in the stream.");
                c = _priorPage[_pageSize - 1];
            }
            else
            {
                c = _currentPage[_charactersRead - 1];
            }
            return c;
        }

        /// <summary>
        /// Swap the current page for the last page.
        /// </summary>
        private void SwapPages()
        {
            char[] swap = _currentPage;
            _currentPage = _priorPage;
            _priorPage = swap;
        }

        /// <summary>
        /// True if this stream is zero length.
        /// </summary>
        /// <returns></returns>
        private bool IsZeroLengthStream()
        {
            return _charactersRead == 0 && _currentPageNumber == 0;
        }

        /// <summary>
        /// COnvert from absolute offset to relative offset within a particular page.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private int AbsoluteOffsetToPageOffset(int offset)
        {
            return offset - (PageFromAbsoluteOffset(offset) * _pageSize);
        }

        /// <summary>
        /// Convert from offset to page number.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private int PageFromAbsoluteOffset(int offset)
        {
            return offset / _pageSize;
        }

        /// <summary>
        /// Returns true of the given position is passed the end of the file.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public bool IsPastEnd(int offset)
        {
            return GetPage(offset) == null;
        }

        /// <summary>
        /// Extract a substring.
        /// </summary>
        /// <param name="startPosition"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public string Substring(int startPosition, int length)
        {
            StringBuilder result = new StringBuilder(length);

            int charactersExtracted;
            for (int i = 0; i < length; i += charactersExtracted)
            {
                char[] page = GetPage(startPosition + i);

                // If we weren't able to read enough characters then throw an exception.
                if (page == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(length));
                }

                int relativeStartPosition = AbsoluteOffsetToPageOffset(startPosition + i);
                int charactersOnPage = GetCharactersOnPage(startPosition + i);

                charactersExtracted = Math.Min(length - i, charactersOnPage - relativeStartPosition);
                ErrorUtilities.VerifyThrow(charactersExtracted > 0, "Expected non-zero extraction count.");

                result.Append(page, relativeStartPosition, charactersExtracted);
            }
            return result.ToString();
        }

        /// <summary>
        /// Returns the number of characters on the page given by offset.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        private int GetCharactersOnPage(int offset)
        {
            int page = PageFromAbsoluteOffset(offset);
            ErrorUtilities.VerifyThrow(page >= _currentPageNumber - 1 && page <= _currentPageNumber, "Could not get character count for this page.");

            if (page == _currentPageNumber)
            {
                return _charactersRead;
            }

            return _pageSize;
        }
    }
}
