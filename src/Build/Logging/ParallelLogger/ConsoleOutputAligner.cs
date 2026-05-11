// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Align output to multiple lines so no logged test is lost due to limited <see cref="Console.BufferWidth"/>.
    /// During alignment optional prefix/indent is applied.
    /// </summary>
    /// <remarks>
    /// This class is not thread safe.
    /// </remarks>
    internal class ConsoleOutputAligner
    {
        internal const int ConsoleTabWidth = 8;

        private readonly int _bufferWidth;
        private readonly bool _alignMessages;
        private readonly IStringBuilderProvider _stringBuilderProvider;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="bufferWidth">Console buffer width. -1 if unknown/unlimited</param>
        /// <param name="alignMessages">Whether messages are aligned/wrapped into console buffer width</param>
        /// <param name="stringBuilderProvider"></param>
        public ConsoleOutputAligner(int bufferWidth, bool alignMessages, IStringBuilderProvider stringBuilderProvider)
        {
            _bufferWidth = bufferWidth;
            _alignMessages = alignMessages;
            _stringBuilderProvider = stringBuilderProvider;
        }

        /// <summary>
        /// Based on bufferWidth split message into multiple lines and indent if needed.
        /// TAB character are interpreted by standard Console logic.
        /// </summary>
        /// <param name="message">Input message. May contains tabs and new lines. Both \r\n and \n is supported but replaced into current environment new line.</param>
        /// <param name="prefixAlreadyWritten">true if message already contains prefix (message context, timestamp, etc...).</param>
        /// <param name="prefixWidth">Width of the prefix. Every line in result string will be indented by this number of spaces except 1st line with already written prefix.</param>
        /// <returns>Aligned message ready to be written to Console</returns>
        /// <remarks>
        /// For optimization purposes this method uses single <see cref="StringBuilder"/> instance. This makes this method non thread safe.
        /// Calling side is expected this usage is non-concurrent. This shall not be an issue as it is expected that writing into Console shall be serialized anyway.
        /// </remarks>
        public string AlignConsoleOutput(string message, bool prefixAlreadyWritten, int prefixWidth)
        {
            int i = 0;
            int j = message.IndexOfAny(MSBuildConstants.CrLf);

            // Empiric value of average line length in console output. Used to estimate number of lines in message for StringBuilder capacity.
            // Wrongly estimated capacity is not a problem as StringBuilder will grow as needed. It is just optimization to avoid multiple reallocations.
            const int averageLineLength = 40;
            int estimatedCapacity = message.Length + ((prefixAlreadyWritten ? 0 : prefixWidth)  + Environment.NewLine.Length) * (message.Length / averageLineLength + 1);
            StringBuilder sb = _stringBuilderProvider.Acquire(estimatedCapacity);

            // The string contains new lines, treat each new line as a different string to format and send to the console
            while (j >= 0)
            {
                AlignAndIndentLineOfMessage(sb, prefixAlreadyWritten, prefixWidth, message, i, j - i);
                i = j + (message[j] == '\r' && (j + 1) < message.Length && message[j + 1] == '\n' ? 2 : 1);
                j = message.IndexOfAny(MSBuildConstants.CrLf, i);
            }

            // Process rest of message
            AlignAndIndentLineOfMessage(sb, prefixAlreadyWritten, prefixWidth, message, i, message.Length - i);

            return _stringBuilderProvider.GetStringAndRelease(sb);
        }

        /// <summary>
        /// Append aligned and indented message lines into running <see cref="StringBuilder"/>.
        /// </summary>
        private void AlignAndIndentLineOfMessage(StringBuilder sb, bool prefixAlreadyWritten, int prefixWidth, string message, int start, int count)
        {
            int bufferWidthMinusNewLine = _bufferWidth - 1;

            bool bufferIsLargerThanPrefix = bufferWidthMinusNewLine > prefixWidth;
            if (_alignMessages && bufferIsLargerThanPrefix && count > 0)
            {
                // If the buffer is larger then the prefix information (timestamp and key) then reformat the messages

                // Beginning index of string to be written
                int index = 0;
                // Loop until all the string has been sent to the console
                while (index < count)
                {
                    // Position of virtual console cursor.
                    // By simulating cursor position adjustment for tab characters '\t' we can compute
                    //   exact numbers of characters from source string to fit into Console.BufferWidth.
                    int cursor = 0;

                    // Write prefix if needed
                    if ((!prefixAlreadyWritten || index > 0 || start > 0) && prefixWidth > 0)
                    {
                        sb.Append(' ', prefixWidth);
                    }
                    // We have to adjust cursor position whether the prefix has been already written or we wrote/indented it ourselves
                    cursor += prefixWidth;

                    // End index of string to be written (behind last character)
                    int endIndex = index;
                    while (cursor < bufferWidthMinusNewLine)
                    {
                        int remainingCharsToEndOfBuffer = Math.Min(bufferWidthMinusNewLine - cursor, count - endIndex);
                        int nextTab = message.IndexOf('\t', start + endIndex, remainingCharsToEndOfBuffer);
                        if (nextTab >= 0)
                        {
                            // Position before tab
                            cursor += nextTab - (start + endIndex);
                            // Move to next tab position
                            cursor += ConsoleTabWidth - cursor % ConsoleTabWidth;
                            // Move end index after the '\t' in preparation for following IndexOf '\t'
                            endIndex += nextTab - (start + endIndex) + 1;
                        }
                        else
                        {
                            endIndex += remainingCharsToEndOfBuffer;
                            break;
                        }
                    }

                    sb.Append(message, start + index, endIndex - index);
                    sb.AppendLine();

                    index = endIndex;
                }
            }
            else
            {
                // If there is not enough room just print the message out and let the console do the formatting
                if (!prefixAlreadyWritten && prefixWidth > 0)
                {
                    sb.Append(' ', prefixWidth);
                }

                sb.Append(message, start, count);
                sb.AppendLine();
            }
        }
    }
}
