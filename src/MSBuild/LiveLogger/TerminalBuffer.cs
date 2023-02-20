// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Build.Logging
{
    internal static class TerminalBuffer
    {
        private static readonly int ScrollableAreaHeight = Console.BufferHeight - 3;

        private static List<TerminalBufferLine> s_terminalBufferLines = new();

        private static volatile int s_topLineIndex = 0;

        internal static bool ShouldRerender { get; set; } = true;

        internal static bool IsTerminated { get; private set; } = false;

        internal static int TopLineIndex { get; } = 0;

        internal static string FooterText { get; set; } = string.Empty;

        internal static void Initialize()
        {
            // Configure buffer, encoding and cursor
            Console.OutputEncoding = Encoding.UTF8;
            Console.Write(AnsiBuilder.Bufferer.UseAlternateBuffer());
            Console.Write(AnsiBuilder.Cursor.Invisible());
        }

        internal static void Terminate()
        {
            IsTerminated = true;

            // Delete contents from alternate buffer before switching back to main buffer
            Console.Write(AnsiBuilder.Cursor.Home() + AnsiBuilder.Eraser.DisplayCursorToEnd());

            // Reset configuration for buffer and cursor, and clear screen
            Console.Write(AnsiBuilder.Bufferer.UseMainBuffer());
            Console.Write(AnsiBuilder.Cursor.Visible());
            s_terminalBufferLines = new();
        }

        internal static void Render()
        {
            if (IsTerminated || !ShouldRerender)
            {
                return;
            }

            ShouldRerender = false;
            Console.Write(
                AnsiBuilder.Cursor.Home() +
                AnsiBuilder.Eraser.LineCursorToEnd() +
                AnsiBuilder.Formatter.Inverse(AnsiBuilder.Aligner.Center("MSBuild - Build in progress")) +
                AnsiBuilder.Cursor.Position(Console.BufferHeight - 1, 0) + AnsiBuilder.Eraser.LineCursorToEnd() +
                new string('-', Console.BufferWidth) + '\n' + FooterText);

            if (s_terminalBufferLines.Count == 0)
            {
                return;
            }

            // Iterate over lines and display on terminal
            string contents = string.Empty;
            int accumulatedLineCount = 0;
            int lineIndex = 0;
            foreach (TerminalBufferLine line in s_terminalBufferLines)
            {
                // Continue if accum line count + next lines < scrolling area
                if (accumulatedLineCount + line.WrappedTextItemsCount < TopLineIndex)
                {
                    accumulatedLineCount += line.WrappedTextItemsCount;
                    continue;
                }

                // Break if exceeds scrolling area
                if (accumulatedLineCount - TopLineIndex > ScrollableAreaHeight)
                {
                    break;
                }

                foreach (string s in line.GetWrappedTextItems())
                {
                    // Get line index relative to scroll area
                    lineIndex = accumulatedLineCount - TopLineIndex;

                    // Print if line in scrolling area
                    if (lineIndex >= 0 && lineIndex < ScrollableAreaHeight)
                    {
                        contents += AnsiBuilder.Cursor.Position(lineIndex + 2, 0) + AnsiBuilder.Eraser.LineCursorToEnd() + s;
                    }

                    accumulatedLineCount++;
                }
            }

            // Iterate for the rest of the screen
            for (int i = lineIndex + 1; i < ScrollableAreaHeight; i++)
            {
                contents += AnsiBuilder.Cursor.Position(i + 2, 0) + AnsiBuilder.Eraser.LineCursorToEnd();
            }

            Console.Write(contents);
        }

        internal static void IncrementTopLineIndex() => Interlocked.Increment(ref s_topLineIndex);

        internal static void DecrementTopLineIndex() => Interlocked.Decrement(ref s_topLineIndex);

        internal static int GetLineIndexById(int lineId) => s_terminalBufferLines.FindIndex(x => x.Id == lineId);

        internal static TerminalBufferLine? WriteNewLineAfter(int lineId, string text) => WriteNewLineAfter(lineId, text, true);

        internal static TerminalBufferLine? WriteNewLineAfter(int lineId, string text, bool shouldWrapLines) =>
            WriteNewLineAfter(lineId, new TerminalBufferLine(text, shouldWrapLines));

        internal static TerminalBufferLine? WriteNewLine(string text) => WriteNewLine(text, true);

        internal static TerminalBufferLine? WriteNewLine(string text, bool shouldWrapLines) =>
            WriteNewLine(new TerminalBufferLine(text, shouldWrapLines));

        internal static void DeleteLine(int lineId)
        {
            // Get line index
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1)
            {
                return;
            }

            // Delete
            s_terminalBufferLines.RemoveAt(lineIndex);
            ShouldRerender = true;
        }

        private static TerminalBufferLine? WriteNewLineAfter(int lineId, TerminalBufferLine line)
        {
            if (lineId != -1)
            {
                // Get line index
                int lineIndex = GetLineIndexById(lineId);
                if (lineIndex == -1)
                {
                    return null;
                }

                // Get line end index
                s_terminalBufferLines.Insert(lineIndex, line);
            }
            else
            {
                s_terminalBufferLines.Add(line);
            }

            return line;
        }

        private static TerminalBufferLine? WriteNewLine(TerminalBufferLine line) => WriteNewLineAfter(
                s_terminalBufferLines.Count > 0
                ? s_terminalBufferLines.Last().Id
                : -1,
                line);
    }
}
