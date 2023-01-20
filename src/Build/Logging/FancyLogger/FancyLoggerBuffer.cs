// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging.FancyLogger
{
    public class FancyLoggerBufferLine
    {
        private static int Counter = 0;
        private string _text = string.Empty;
        public List<string> WrappedText { get; private set; } = new();
        public int Id;
        public bool ShouldWrapLines;
        public string Text
        {
            get => _text;
            set
            {
                // Set text value and get wrapped lines
                _text = value;
                if (ShouldWrapLines) WrappedText = ANSIBuilder.ANSIWrap(value, Console.BufferWidth);
                else WrappedText = new List<string> { value };
            }
        }

        public FancyLoggerBufferLine()
        {
            Id = Counter++;
            Text = string.Empty;
            ShouldWrapLines = false;
        }
        public FancyLoggerBufferLine(string text)
            : this()
        {
            Text = text;
        }
        public FancyLoggerBufferLine(string text, bool shouldWrapLines)
            : this()
        {
            ShouldWrapLines = shouldWrapLines;
            Text = text;
        }
    }

    public class FancyLoggerBuffer
    {
        private static List<FancyLoggerBufferLine> Lines = new();
        public static int TopLineIndex = 0;
        public static string Footer = string.Empty;
        private static bool AutoScrollEnabled = true;
        public static string FooterText = string.Empty;
        private static bool IsTerminated = false;
        public static void Initialize()
        {

            Task.Run(() =>
            {
                // Configure buffer, encoding and cursor
                Console.OutputEncoding = Encoding.UTF8;
                Console.Write(ANSIBuilder.Buffer.UseAlternateBuffer());
                Console.Write(ANSIBuilder.Cursor.Invisible());

                // Counter for delaying render
                int i = 0;

                // Execute while the buffer is active
                while (!IsTerminated)
                {
                    // Delay by 1/60 seconds
                    i++;
                    Task.Delay((i/60) * 1_000).ContinueWith((t) =>
                    {
                        Render();
                    });
                    if (Console.KeyAvailable)
                    { 
                        // Handle keyboard input
                        ConsoleKey key = Console.ReadKey().Key;
                        switch (key)
                        {
                            case ConsoleKey.UpArrow:
                                if (TopLineIndex > 0) TopLineIndex--;
                                break;
                            case ConsoleKey.DownArrow:
                                TopLineIndex++;
                                break;
                            case ConsoleKey.Spacebar:
                                AutoScrollEnabled = !AutoScrollEnabled;
                                break;
                            default:
                                break;
                        }
                    }
                }
            });
        }

        public static void Terminate()
        {
            IsTerminated = true;
            // Reset configuration for buffer and cursor, and clear screen
            Console.Write(ANSIBuilder.Buffer.UseMainBuffer());
            Console.Write(ANSIBuilder.Eraser.Display());
            Console.Clear();
            Console.Write(ANSIBuilder.Cursor.Visible());
            Lines = new();
        }

        #region Rendering
        public static void Render()
        {
            if (IsTerminated) return;
            Console.Write(
                // Write header
                ANSIBuilder.Cursor.Home() +
                ANSIBuilder.Eraser.LineCursorToEnd() + ANSIBuilder.Formatting.Inverse(ANSIBuilder.Alignment.Center("MSBuild - Build in progress")) +
                // Write footer
                ANSIBuilder.Cursor.Position(Console.BufferHeight - 1, 0) + ANSIBuilder.Eraser.LineCursorToEnd() +
                new string('-', Console.BufferWidth) + '\n' + FooterText
            );
            if (Lines.Count == 0) return;

            // Iterate over lines and display on terminal
            // TODO: Delimit range to improve performance 
            int accumulatedLineCount = 0;
            foreach (FancyLoggerBufferLine line in Lines)
            {
                foreach (string s in line.WrappedText) {
                    // Get line index relative to scroll area
                    int lineIndex = accumulatedLineCount - TopLineIndex;
                    // Print if line in scrolling area
                    if (lineIndex >= 0 && lineIndex < Console.BufferHeight - 3)
                    {
                        Console.Write(ANSIBuilder.Cursor.Position(lineIndex + 2, 0) + ANSIBuilder.Eraser.LineCursorToEnd() + s);
                    }
                    accumulatedLineCount++;
                }
            }
        }
        #endregion
        #region Line identification
        public static int GetLineIndexById(int lineId)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Id == lineId) return i;
            }
            return -1;
        }

        public static FancyLoggerBufferLine? GetLineById(int lineId)
        {
            int index = GetLineIndexById(lineId);
            if (index == -1) return null;
            return Lines[index];
        }
        #endregion

        #region Line create, update and delete
        // Write new line
        public static FancyLoggerBufferLine? WriteNewLineAfter(int lineId, string text)
        {
            return WriteNewLineAfter(lineId, text, true);
        }
        public static FancyLoggerBufferLine? WriteNewLineAfter(int lineId, string text, bool shouldWrapLines)
        {
            FancyLoggerBufferLine line = new FancyLoggerBufferLine(text, shouldWrapLines);
            return WriteNewLineAfter(lineId, line);
        }
        public static FancyLoggerBufferLine? WriteNewLineAfter(int lineId, FancyLoggerBufferLine line)
        {
            if (lineId != -1)
            {
                // Get line index
                int lineIndex = GetLineIndexById(lineId);
                if (lineIndex == -1) return null;
                // Get line end index
                Lines.Insert(lineIndex, line);
            }
            else
            {
                Lines.Add(line);
            }
            // TODO: Handle autoscrolling
            return line;
        }

        public static FancyLoggerBufferLine? WriteNewLine(string text)
        {
            return WriteNewLine(text, true);
        }
        public static FancyLoggerBufferLine? WriteNewLine(string text, bool shouldWrapLines)
        {
            FancyLoggerBufferLine line = new FancyLoggerBufferLine(text, shouldWrapLines);
            return WriteNewLine(line);
        }
        public static FancyLoggerBufferLine? WriteNewLine(FancyLoggerBufferLine line)
        {
            return WriteNewLineAfter(Lines.Count > 0 ? Lines.Last().Id : -1, line);
        }

        // Update line
        public static FancyLoggerBufferLine? UpdateLine(int lineId, string text)
        {
            // Get line
            FancyLoggerBufferLine? line = GetLineById(lineId);
            if (line == null) return null;
            line.Text = text;
            // Return
            return line;
            // TODO: Handle autoscrolling
        }

        // Delete line
        public static void DeleteLine(int lineId)
        {
            // Get line index
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1) return;
            // Save top line
            int topLineId = Lines[TopLineIndex].Id;
            // Delete
            Lines.RemoveAt(lineIndex);
            // TODO: Handle autoscrolling
        }
        #endregion
    }
}
