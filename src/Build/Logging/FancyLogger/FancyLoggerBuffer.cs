// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging.FancyLogger
{
    public class FancyLoggerBufferLine
    {
        private static int Counter = 0;
        public int Id;
        public string Text;
        public FancyLoggerBufferLine? NextLine;

        private string _fullText;
        public string FullText
        {
            get => _fullText;
            set
            {
                // Assign value
                _fullText = value;
                // Delete next line if exists
                if (NextLine is not null)
                {
                    FancyLoggerBuffer.DeleteLine(NextLine.Id);
                    NextLine = null;
                }
                // If text overflows
                if (value.Length > Console.BufferWidth)
                {
                    // Get breakpoints
                    int breakpoint = ANSIBuilder.ANSIBreakpoint(value, Console.BufferWidth);
                    // Text
                    Text = value.Substring(0, breakpoint);
                    // Next line
                    if (breakpoint + 1 < value.Length)
                    {
                        NextLine = new FancyLoggerBufferLine(value.Substring(breakpoint));
                    }
                }
                else
                {
                    Text = value;
                }
            }
        }

        public FancyLoggerBufferLine()
        {
            Id = Counter++;
            Text = string.Empty;
            _fullText = string.Empty;
        }
        public FancyLoggerBufferLine(string text)
            : this()
        {
            FullText = text;
        }

        public List<FancyLoggerBufferLine> NextLines()
        {
            List<FancyLoggerBufferLine> results = new();
            if (NextLine is not null)
            {
                results.Add(NextLine);
                results.AddRange(NextLine.NextLines());
            }
            return results;
        }
    }

    public class FancyLoggerBuffer
    {
        private static List<FancyLoggerBufferLine> Lines = new();
        private static int TopLineIndex = 0;
        private static bool AutoScrollEnabled = true;
        public static void Initialize()
        {
            // Use alternate buffer
            // TODO: Remove. Tries to solve a bug when switching from and to the alternate buffer
            Console.Write(ANSIBuilder.Buffer.UseMainBuffer());
            Console.Write(ANSIBuilder.Buffer.UseAlternateBuffer());

            Task.Run(async () => {
                while (true)
                {
                    await Task.Delay(500 / 60);
                    Render();
                }
            });

            Task.Run(() =>
            {
                while (true)
                {
                    switch (Console.ReadKey().Key)
                    {
                        case ConsoleKey.UpArrow:
                            if (TopLineIndex > 0) TopLineIndex--;
                            break;
                        case ConsoleKey.DownArrow:
                            if (TopLineIndex < Console.BufferHeight - 3) TopLineIndex++;
                            break;
                        case ConsoleKey.Spacebar:
                        case ConsoleKey.Escape:
                            AutoScrollEnabled = !AutoScrollEnabled;
                            break;
                    }
                }
            });
        }

        public static void Terminate()
        {
            // TODO: Remove. Tries to solve a bug when switching from and to the alternate buffer
            Console.Write(ANSIBuilder.Buffer.UseMainBuffer());
            Console.Write(ANSIBuilder.Eraser.Display());
            Lines = new();
        }

        #region Rendering
        public static void Render()
        {
            if (Lines.Count == 0) return;
            // Write Header
            Console.Write(
                // Write header
                ANSIBuilder.Cursor.Home() +
                ANSIBuilder.Eraser.LineCursorToEnd() + ANSIBuilder.Formatting.Inverse(ANSIBuilder.Alignment.Center("MSBuild - Build in progress")) +
                // Write footer
                ANSIBuilder.Eraser.LineCursorToEnd() + ANSIBuilder.Cursor.Position(Console.BufferHeight - 1, 0) +
                // TODO: Remove and replace with actual footer
                new string('-', Console.BufferWidth) + '\n' + "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"
            );
            // Write lines
            // TODO: Update to make more efficient (store nextlines as lists instead of nested, add cache, etc)
            List<FancyLoggerBufferLine> linesWithWrappings = GetLinesWithWrappings();
            for (int i = 0; i < Console.BufferHeight - 3; i++)
            {
                int lineIndex = i + TopLineIndex;
                Console.Write(
                    ANSIBuilder.Cursor.Position(i + 2, 0) +
                    ANSIBuilder.Eraser.LineCursorToEnd() + 
                    (lineIndex < linesWithWrappings.Count ? linesWithWrappings[lineIndex].Text : String.Empty)
                );
            }
        }
        #endregion

        public static List<FancyLoggerBufferLine> GetLinesWithWrappings()
        {
            List<FancyLoggerBufferLine> result = new();
            foreach (FancyLoggerBufferLine line in Lines)
            {
                result.Add(line);
                result.AddRange(line.NextLines());
            }
            return result;
        }

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
            FancyLoggerBufferLine line = new FancyLoggerBufferLine(text);
            return WriteNewLineAfter(lineId, line);
        }
        public static FancyLoggerBufferLine? WriteNewLineAfter(int lineId, FancyLoggerBufferLine line, bool overrideOverflowLines = false)
        {
            // Save top line (current if no lines)
            int topLineId = Lines.Count > 0 ? Lines[TopLineIndex].Id : line.Id;
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
            // Get updated top line index
            TopLineIndex = GetLineIndexById(topLineId);
            // Return
            return line;
        }

        public static FancyLoggerBufferLine? WriteNewLine(string text)
        {
            FancyLoggerBufferLine line = new FancyLoggerBufferLine(text);
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
            line.FullText = text;
            // Return
            return line;
        }

        // Delete line
        public static void DeleteLine(int lineId)
        {
            // TODO: What if line id is equal to topLineId?????
            // Get line index
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1) return;
            // Save top line
            int topLineId = Lines[TopLineIndex].Id;
            // Delete
            Lines.RemoveAt(lineIndex);
            // Get updated top line index
            if (topLineId != lineId)
            {
                TopLineIndex = GetLineIndexById(topLineId);
            }
        }
        #endregion
    }
}
