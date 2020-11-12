// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Shared.LanguageParser;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class StreamMappedString_Tests
    {
        /// <summary>
        /// Test for a string that has ANSI but non-ascii characters.
        /// </summary>
        [Fact]
        public void Regress_Mutation_ForceANSIWorks_RelatedTo172107()
        {
            // Can't embed the 'Ã' directly because the string is Unicode already and the Unicode<-->ANSI transform
            // isn't bidirectional.
            MemoryStream sourcesStream = (MemoryStream)StreamHelpers.StringToStream("namespace d?a { class Class {} }");

            // Instead, directly write the ANSI character into the memory buffer.
            sourcesStream.Seek(11, SeekOrigin.Begin);
            sourcesStream.WriteByte(0xc3);    // Plug the 'Ã' in 
            sourcesStream.Seek(0, SeekOrigin.Begin);

            // Should not throw an exception because we force ANSI.
            StreamMappedString s = new StreamMappedString(sourcesStream, /* forceANSI */ true);
            s.GetAt(11);
        }

        [Fact]
        public void Regress_Mutation_BackingUpMoreThanOnePageWorks()
        {
            Stream stream = StreamHelpers.StringToStream("A" + new String('b', StreamMappedString.DefaultPageSize * 4));
            StreamMappedString s = new StreamMappedString(stream, false);

            // Get the last character...
            s.GetAt(StreamMappedString.DefaultPageSize * 4);

            // ...now get the first character.
            Assert.Equal('A', s.GetAt(0));
        }

        [Fact]
        public void Regress_Mutation_RetrievingFromLastPageWorks()
        {
            Stream stream = StreamHelpers.StringToStream("A" + new String('b', StreamMappedString.DefaultPageSize));
            StreamMappedString s = new StreamMappedString(stream, false);

            // Get the last character...
            s.GetAt(StreamMappedString.DefaultPageSize);

            // ...now get the first character (which should be saved on lastPage).
            Assert.Equal('A', s.GetAt(0));
        }

        [Fact]
        public void Regress_Mutation_LastCharacterShouldBeNewLine()
        {
            Stream stream = StreamHelpers.StringToStream("A");
            StreamMappedString s = new StreamMappedString(stream, false);

            // Get the last character (which should be the appended newLine).
            Assert.Equal('\xd', s.GetAt(1));
        }

        [Fact]
        public void Regress_Mutation_1AShouldBeStripped()
        {
            Stream stream = StreamHelpers.StringToStream("x\x1Ay");
            StreamMappedString s = new StreamMappedString(stream, false);

            // Get the last character (which should be 'y' and not 0x1A).
            Assert.Equal('y', s.GetAt(1));
        }

        [Fact]
        public void Regress_Mutation_MultiplePagesOf1AShouldBeStripped()
        {
            Stream stream = StreamHelpers.StringToStream(new String('\x1a', StreamMappedString.DefaultPageSize * 2) + "x");
            StreamMappedString s = new StreamMappedString(stream, false);

            // Get the last character (which should be 'x' and not 0x1A).
            Assert.Equal('x', s.GetAt(0));
        }

        [Fact]
        public void Regress_Mutation_NewLineGetsAppendedAcrossPageBoundaries()
        {
            Stream stream = StreamHelpers.StringToStream(new String('x', StreamMappedString.DefaultPageSize));
            StreamMappedString s = new StreamMappedString(stream, false);

            // Get the last character (which should be '\xd').
            Assert.Equal('\xd', s.GetAt(StreamMappedString.DefaultPageSize));
        }

        [Fact]
        public void Regress_Mutation_SubstringWorks()
        {
            Stream stream = StreamHelpers.StringToStream("abcdefg");
            StreamMappedString s = new StreamMappedString(stream, false);

            Assert.Equal("bcd", s.Substring(1, 3));
        }

        [Fact]
        public void Regress_Mutation_SubstringWorksWithPageSizeOne()
        {
            Stream stream = StreamHelpers.StringToStream("abcdefg");
            StreamMappedString s = new StreamMappedString(stream, false, /* pageSize */ 1);

            Assert.Equal("bcd", s.Substring(1, 3));
        }

        [Fact]
        public void Regress_Mutation_SubstringWorksFromPriorPage()
        {
            Stream stream = StreamHelpers.StringToStream("abcxdef");
            StreamMappedString s = new StreamMappedString(stream, false, 7);

            // Move to the last page
            s.GetAt(7);

            // And then extract a string from the beginning page.
            Assert.Equal("abcxdef", s.Substring(0, 7));
        }

        [Fact]
        public void Regress_Mutation_SubstringReadPastEndThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Stream stream = StreamHelpers.StringToStream("abcdefg");
                StreamMappedString s = new StreamMappedString(stream, false);

                Assert.Equal(String.Empty, s.Substring(1, 30));
            }
           );
        }
        [Fact]
        public void Regress_Mutation_SubstringOnLastPageWorks()
        {
            Stream stream = StreamHelpers.StringToStream("abcdefg" + new String('x', StreamMappedString.DefaultPageSize));
            StreamMappedString s = new StreamMappedString(stream, false);

            // Move to the second page
            s.GetAt(StreamMappedString.DefaultPageSize);

            // Get a string from the firstPage
            Assert.Equal("abc", s.Substring(0, 3));
        }

        [Fact]
        public void Regress_Mutation_UnicodeIsDetected()
        {
            Stream stream = StreamHelpers.StringToStream("\u00C3ngelo's Steak House", System.Text.Encoding.UTF32);
            StreamMappedString s = new StreamMappedString(stream, false);

            // This won't read correctly with ANSI encoding.
            Assert.Equal('\u00C3', s.GetAt(0));
        }

        [Fact]
        public void Regress_Mutation_ReadingCharactersForwardOnlyShouldCauseNoAdditionalResets()
        {
            RestartCountingStream stream = new RestartCountingStream(StreamHelpers.StringToStream("abcdefg"));
            StreamMappedString s = new StreamMappedString(stream, false);

            // Get a few characters.
            s.GetAt(0);
            s.GetAt(1);
            s.GetAt(2);
            s.GetAt(3);
            s.GetAt(4);

            // There should be exactly one reset for this.
            Assert.Equal(0, stream.ResetCount);
        }

        [Fact]
        public void Regress_Mutation_IsPastEndWorks()
        {
            RestartCountingStream stream = new RestartCountingStream(StreamHelpers.StringToStream("a"));
            StreamMappedString s = new StreamMappedString(stream, false);

            // There's only one character, so IsPastEnd(2) should be true.
            Assert.True(s.IsPastEnd(2)); // <-- 2 required because of extra \xd added.
        }

        [Fact]
        public void Regress_Mutation_MinimizePagesAllocated()
        {
            Stream stream = StreamHelpers.StringToStream("a" + new String('x', StreamMappedString.DefaultPageSize * 2));
            StreamMappedString s = new StreamMappedString(stream, false);

            // Get a few characters.
            s.GetAt(0);
            s.GetAt(StreamMappedString.DefaultPageSize);
            s.GetAt(StreamMappedString.DefaultPageSize * 2);

            // Even though three pages were read, only two allocations should have occurred.
            Assert.Equal(2, s.PagesAllocated);
        }

        [Fact]
        public void Regress_Mutation_1DNotAppendedIfAlreadyThere()
        {
            RestartCountingStream stream = new RestartCountingStream(StreamHelpers.StringToStream("\xd"));
            StreamMappedString s = new StreamMappedString(stream, false);

            // There's only one \x1d so IsPastEnd(1) should be true.
            Assert.True(s.IsPastEnd(1));
        }

        [Fact]
        public void Regress_Codereview_RequestPageWellPastEnd()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Stream stream = StreamHelpers.StringToStream("x");
                StreamMappedString s = new StreamMappedString(stream, false);

                // Read something way past the end. This should result in a range exception.
                s.GetAt(1000000);
            }
           );
        }

        [Fact]
        public void Regress_Mutation_FirstCharacterOnPagePastEndDoesntExist()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Stream stream = StreamHelpers.StringToStream("abc");
                StreamMappedString s = new StreamMappedString(stream, false, 256);

                s.GetAt(256);
            }
           );
        }

        [Fact]
        public void Regress_Mutation_RequestPageWellPastEnd()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                Stream stream = StreamHelpers.StringToStream(new String('x', StreamMappedString.DefaultPageSize * 2));
                StreamMappedString s = new StreamMappedString(stream, false);

                // Read something way past the end. This should result in a range exception.
                s.GetAt(1000000);
            }
           );
        }


        /// <summary>
        /// A stream class that counts the number of times it was reset.
        /// </summary>
        private class RestartCountingStream : Stream
        {
            private int _resetCount;
            private Stream _stream;

            public RestartCountingStream(Stream stream)
            {
                _stream = stream;
            }

            /// <summary>
            /// Returns the number of times this stream was reset.
            /// </summary>
            public int ResetCount
            {
                get { return _resetCount; }
            }

            public override bool CanRead
            {
                get { return _stream.CanRead; }
            }

            public override bool CanSeek
            {
                get { throw new Exception("The method or operation is not implemented."); }
            }

            public override bool CanWrite
            {
                get { throw new Exception("The method or operation is not implemented."); }
            }

            public override void Flush()
            {
                throw new Exception("The method or operation is not implemented.");
            }

            public override long Length
            {
                get { throw new Exception("The method or operation is not implemented."); }
            }

            public override long Position
            {
                get
                {
                    return _stream.Position;
                }
                set
                {
                    throw new Exception("The method or operation is not implemented.");
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _stream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                ++_resetCount;
                return this.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                throw new Exception("The method or operation is not implemented.");
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }
    }
}





