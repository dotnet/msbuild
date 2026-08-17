// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class ConsoleOutputAlignerTests
    {
        [Theory]
        [InlineData("a", true)]
        [InlineData("a", false)]
        [InlineData("12345", true)]
        [InlineData("12345", false)]
        public void IndentBiggerThanBuffer_IndentedAndNotAligned(string input, bool aligned)
        {
            string indent = "    ";
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: aligned, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: false, prefixWidth: indent.Length);

            output.ShouldBe(indent + input + Environment.NewLine);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("12345")]
        public void NoAlignNoIndent_NotAlignedEvenIfBiggerThanBuffer(string input)
        {
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: false, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: false, prefixWidth: 0);

            output.ShouldBe(input + Environment.NewLine);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(1000)]
        public void NoBufferWidthNoIndent_NotAligned(int sizeOfMessage)
        {
            string input = new string('.', sizeOfMessage);
            var aligner = new ConsoleOutputAligner(bufferWidth: -1, alignMessages: false, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: false, prefixWidth: 0);

            output.ShouldBe(input + Environment.NewLine);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("12345")]
        public void WithoutBufferWidthWithoutIndentWithAlign_NotIndentedAndNotAligned(string input)
        {
            var aligner = new ConsoleOutputAligner(bufferWidth: -1, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: false, prefixWidth: 0);

            output.ShouldBe(input + Environment.NewLine);
        }

        [Theory]
        [InlineData("a")]
        [InlineData("12345")]
        public void NoAlignPrefixAlreadyWritten_NotChanged(string input)
        {
            var aligner = new ConsoleOutputAligner(bufferWidth: 10, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: true, prefixWidth: 0);

            output.ShouldBe(input + Environment.NewLine);
        }

        [Theory]
        [InlineData("", "123")]
        [InlineData(" ", "12")]
        [InlineData("  ", "1")]
        public void SmallerThanBuffer_NotAligned(string indent, string input)
        {
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: false, prefixWidth: indent.Length);

            output.ShouldBe(indent + input + Environment.NewLine);
        }

        [Theory]
        [InlineData("", "1234", "123", "4")]
        [InlineData(" ", "123", " 12", " 3")]
        [InlineData("  ", "12", "  1", "  2")]
        public void BiggerThanBuffer_AlignedWithIndent(string indent, string input, string expected1stLine, string expected2ndLine)
        {
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: false, prefixWidth: indent.Length);

            output.ShouldBe(expected1stLine + Environment.NewLine + expected2ndLine + Environment.NewLine);
        }

        [Theory]
        [InlineData("", "12345678", "123\n" +
                                    "456\n" +
                                    "78\n")]
        [InlineData(" ", "12345678", " 12\n" +
                                     " 34\n" +
                                     " 56\n" +
                                     " 78\n")]
        [InlineData("  ", "1234", "  1\n" +
                                  "  2\n" +
                                  "  3\n" +
                                  "  4\n")]
        public void XTimesBiggerThanBuffer_AlignedToMultipleLines(string indent, string input, string expected)
        {
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: false, prefixWidth: indent.Length);

            output.ShouldBe(expected.Replace("\n", Environment.NewLine));
        }


        [Theory]
        [InlineData("", "1234", "123", "4")]
        [InlineData(" ", "123", "12", " 3")]
        [InlineData("  ", "12", "1", "  2")]
        public void BiggerThanBufferWithPrefixAlreadyWritten_AlignedWithIndentFromSecondLine(string indent, string input, string expected1stLine, string expected2ndLine)
        {
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: true, prefixWidth: indent.Length);

            output.ShouldBe(expected1stLine + Environment.NewLine + expected2ndLine + Environment.NewLine);
        }

        [Theory]
        [InlineData("a\nb")]
        [InlineData("12345\n54321")]
        [InlineData("\t12345\n\t54321")]
        public void MultiLineWithoutAlign_NotChanged(string input)
        {
            input = input.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: false, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: true, prefixWidth: 0);

            output.ShouldBe(input + Environment.NewLine);
        }

        /// <summary>
        /// Although consoles interprets \r as return carrier to the begging of the line, we treat \r as NewLine, as it is most consistent with how file viewers interpret it and
        ///    because logs are rarely read directly from console but more often from log files.
        /// Consequently \n\r shall be interpreted not as sequence but two control characters with equivalent of \n\n.
        /// </summary>
        [Theory]
        [InlineData("a\n\rb", "a\n\n  b")]
        [InlineData("a\rb", "a\n  b")]
        [InlineData("\n\ra", "\n\n  a")]
        [InlineData("\ra", "\n  a")]
        [InlineData("a\nb\n\r", "a\n  b\n\n")]
        [InlineData("a\nb\r", "a\n  b\n")]
        public void NonStandardNewLines_AlignAsExpected(string input, string expected)
        {
            expected = expected.Replace("\n", Environment.NewLine) + Environment.NewLine;

            var aligner = new ConsoleOutputAligner(bufferWidth: 10, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: true, prefixWidth: 2);

            output.ShouldBe(expected);
        }

        [Theory]
        [InlineData("a\nb")]
        [InlineData("123456789\n987654321")]
        [InlineData("\t1\n9\t1")]
        public void ShortMultiLineWithAlign_NoChange(string input)
        {
            input = input.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: 10, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: true, prefixWidth: 0);

            output.ShouldBe(input + Environment.NewLine);
        }

        [Theory]
        [InlineData("a\nb")]
        [InlineData("a\r\nb")]
        [InlineData("a\nb\r\n")]
        [InlineData("a\nb\n")]
        [InlineData("a\n\nb")]
        [InlineData("a\r\n\nb")]
        [InlineData("a\n\r\nb")]
        [InlineData("a\r\n\r\nb")]
        [InlineData("\r\na\nb")]
        [InlineData("\na\nb")]
        [InlineData("\na\r\nb\nc")]
        [InlineData("\r\na\nb\r\nc")]
        public void ShortMultiLineWithMixedNewLines_NewLinesReplacedByActualEnvironmentNewLines(string input)
        {
            string expected = input.Replace("\r", "").Replace("\n", Environment.NewLine) + Environment.NewLine;
            var aligner = new ConsoleOutputAligner(bufferWidth: 10, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: true, prefixWidth: 0);

            output.ShouldBe(expected);
        }

        [Theory]
        [InlineData("", "a\n12345", "a\n123\n45\n")]
        [InlineData("", "12345\na\n54321", "123\n45\na\n543\n21\n")]
        [InlineData(" ", "12345\na\n54321", "12\n 34\n 5\n a\n 54\n 32\n 1\n")]
        public void MultiLineWithPrefixAlreadyWritten(string prefix, string input, string expected)
        {
            input = input.Replace("\n", Environment.NewLine);
            expected = expected.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: true, prefixWidth: prefix.Length);

            output.ShouldBe(expected);
        }

        [Theory]
        [InlineData(" ", "a\n12345", " a\n 12\n 34\n 5\n")]
        [InlineData(" ", "12345\na\n54321", " 12\n 34\n 5\n a\n 54\n 32\n 1\n")]
        public void MultiLineWithoutPrefixAlreadyWritten(string prefix, string input, string expected)
        {
            input = input.Replace("\n", Environment.NewLine);
            expected = expected.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: 4, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: false, prefixWidth: prefix.Length);

            output.ShouldBe(expected);
        }

        [Theory]
        [InlineData("\t")]
        [InlineData("a\nb\tc\nd")]
        public void ShortTextWithTabs_NoChange(string input)
        {
            input = input.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: 50, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: true, prefixWidth: 0);

            output.ShouldBe(input + Environment.NewLine);
        }

        [Theory]
        [InlineData("", "\t", 7, false)]
        [InlineData("", "12345678\t", 15, false)]
        [InlineData(" ", "2345678\t", 15, false)]
        [InlineData(" ", "2345678\t", 15, true)]
        public void LastTabOverLimit_NoChange(string prefix, string input, int bufferWidthWithoutNewLine, bool prefixAlreadyWritten)
        {
            input = input.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: bufferWidthWithoutNewLine + 1, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: prefixAlreadyWritten, prefixWidth: prefix.Length);

            output.ShouldBe((prefixAlreadyWritten ? string.Empty : prefix) + input + Environment.NewLine);
        }

        [Theory]
        [InlineData("", "\t", 8, false)]
        [InlineData("", "12345678\t", 16, false)]
        [InlineData(" ", "2345678\t", 16, false)]
        [InlineData(" ", "2345678\t", 16, true)]
        public void LastTabAtLimit_NoChange(string prefix, string input, int bufferWidthWithoutNewLine, bool prefixAlreadyWritten)
        {
            input = input.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: bufferWidthWithoutNewLine + 1, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: prefixAlreadyWritten, prefixWidth: prefix.Length);

            output.ShouldBe((prefixAlreadyWritten ? string.Empty : prefix) + input + Environment.NewLine);
        }

        [Theory]
        [InlineData("", "\t", 8, false)]
        [InlineData("", "12345678\t", 16, false)]
        [InlineData(" ", "2345678\t", 16, false)]
        [InlineData(" ", "2345678\t", 16, true)]
        public void TabsMakesItJustOverLimit_IndentAndAlign(string prefix, string input, int bufferWidthWithoutNewLine, bool prefixAlreadyWritten)
        {
            input = input.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: bufferWidthWithoutNewLine + 1, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input + "x", prefixAlreadyWritten: prefixAlreadyWritten, prefixWidth: prefix.Length);

            string expected = (prefixAlreadyWritten ? string.Empty : prefix) + input + Environment.NewLine +
                              prefix + "x" + Environment.NewLine;

            output.ShouldBe(expected);
        }

        [Theory]
        // +----+----+---+---+---+---+---+---+
        // | 1  | 2  | 3 | 4 | 5 | 6 | 7 | 8 |
        // +----+----+---+---+---+---+---+---+
        // | \t | .  | . | . | . | . | . | . |
        // +----+----+---+---+---+---+---+---+
        // | 1  |    |   |   |   |   |   |   |
        // +----+----+---+---+---+---+---+---+
        // | a  | \t | . | . | . | . | . | . |
        // +----+----+---+---+---+---+---+---+
        // | b  |    |   |   |   |   |   |   |
        // +----+----+---+---+---+---+---+---+
        [InlineData("", "\t1\na\tb", "\t\n1\na\t\nb\n", 8, false)]
        // +---+---+---+----+---+---+---+---+----+
        // | 1 | 2 | 3 | 4  | 5 | 6 | 7 | 8 | 9  |
        // +---+---+---+----+---+---+---+---+----+
        // | 1 | 2 | 3 | 4  | 5 | 6 | 7 | 8 | \t |
        // +---+---+---+----+---+---+---+---+----+
        // | a | b | c |    |   |   |   |   |    |
        // +---+---+---+----+---+---+---+---+----+
        // | d | e | f | \t | . | . | . | . | g  |
        // +---+---+---+----+---+---+---+---+----+
        [InlineData("", "12345678\tabc\ndef\tg", "12345678\t\nabc\ndef\tg\n", 9, false)]
        // +----+---+---+----+---+---+---+---+----+----+----+----+----+----+----+----+----+
        // | 1  | 2 | 3 | 4  | 5 | 6 | 7 | 8 | 9  | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 |
        // +----+---+---+----+---+---+---+---+----+----+----+----+----+----+----+----+----+
        // | \t | . | . | .  | . | . | . | . | \t | .  | .  | .  | .  | .  | .  | .  | a  |
        // +----+---+---+----+---+---+---+---+----+----+----+----+----+----+----+----+----+
        // | b  | c |   |    |   |   |   |   |    |    |    |    |    |    |    |    |    |
        // +----+---+---+----+---+---+---+---+----+----+----+----+----+----+----+----+----+
        // | d  | e | f | \t | . | . | . | . | \t | .  | .  | .  | .  | .  | .  | .  | g  |
        // +----+---+---+----+---+---+---+---+----+----+----+----+----+----+----+----+----+
        // | h  | i |   |    |   |   |   |   |    |    |    |    |    |    |    |    |    |
        // +----+---+---+----+---+---+---+---+----+----+----+----+----+----+----+----+----+
        [InlineData("", "\t\tabc\ndef\t\tghi", "\t\ta\nbc\ndef\t\tg\nhi\n", 17, false)]
        // +---+---+----+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | 1 | 2 | 3  | 4 | 5  | 6 | 7 | 8 | 9  | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 |
        // +---+---+----+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | a | \t | . | .  | . | . | . | \t | .  | .  | .  | .  | .  | .  | .  | b  |
        // +---+---+----+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | c |    |   |    |   |   |   |    |    |    |    |    |    |    |    |    |
        // +---+---+----+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | d | e  | f | \t | . | . | . | \t | .  | .  | .  | .  | .  | .  | .  | g  |
        // +---+---+----+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | h | i  | 5 | 6  | 7 | 8 | 9  | 0 | 1  | 2  | 3  | 4  | 5  | 6  | 7  | 8  |
        // +---+---+----+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | 9 |    |   |    |   |   |   |    |    |    |    |    |    |    |    |    |
        // +---+---+----+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        [InlineData(" ", "a\t\tbc\ndef\t\tghi567890123456789", " a\t\tb\n c\n def\t\tg\n hi56789012345678\n 9\n", 17, false)]
        // +---+----+---+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | 1 | 2  | 3 | 4 | 5  | 6 | 7 | 8 | 9  | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 |
        // +---+----+---+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | a | \t | . | . | .  | . | . | . | \t | .  | .  | .  | .  | .  | .  | .  | b  |
        // +---+----+---+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | c  |   |   |    |   |   |   |    |    |    |    |    |    |    |    |    |
        // +---+----+---+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | d  | e | f | \t | . | . | . | \t | .  | .  | .  | .  | .  | .  | .  | g  |
        // +---+----+---+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | h  | i | 5 | 6  | 7 | 8 | 9 | 0  | 1  | 2  | 3  | 4  | 5  | 6  | 7  | 8  |
        // +---+----+---+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        // | _ | 9  |   |   |    |   |   |   |    |    |    |    |    |    |    |    |    |
        // +---+----+---+---+----+---+---+---+----+----+----+----+----+----+----+----+----+
        [InlineData(" ", "a\t\tbc\ndef\t\tghi567890123456789", "a\t\tb\n c\n def\t\tg\n hi56789012345678\n 9\n", 17, true)]
        public void MultiLinesOverLimit_IndentAndAlign(string prefix, string input, string expected, int bufferWidthWithoutNewLine, bool prefixAlreadyWritten)
        {
            input = input.Replace("\n", Environment.NewLine);
            expected = expected.Replace("\n", Environment.NewLine);
            var aligner = new ConsoleOutputAligner(bufferWidth: bufferWidthWithoutNewLine + 1, alignMessages: true, stringBuilderProvider: new TestStringBuilderProvider());

            string output = aligner.AlignConsoleOutput(message: input, prefixAlreadyWritten: prefixAlreadyWritten, prefixWidth: prefix.Length);

            output.ShouldBe(expected);
        }

        private sealed class TestStringBuilderProvider : IStringBuilderProvider
        {
            public StringBuilder Acquire(int capacity) => new StringBuilder(capacity);
            public string GetStringAndRelease(StringBuilder builder) => builder.ToString();
        }
    }
}
