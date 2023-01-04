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

        public FancyLoggerBufferLine()
        {
            Id = Counter++;
            Text = String.Empty;
        }
        public FancyLoggerBufferLine(string text)
            : this()
        {
            Text = text;
        }
    }

    public class FancyLoggerBuffer
    {
        private static List<FancyLoggerBufferLine> Lines = new();
        private static int TopLineIndex = 0;
        // private static bool AutoScrollEnabled = true;
        public static void Initialize()
        {
            // Use alternate buffer
            Console.Write(ANSIBuilder.Buffer.UseMainBuffer());
            Console.Write(ANSIBuilder.Buffer.UseAlternateBuffer());

            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(500 / 60);
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
                            if (TopLineIndex < Lines.Count) TopLineIndex++;
                            break;
                        case ConsoleKey.Spacebar:
                        case ConsoleKey.Escape:
                            // AutoScrollEnabled = !AutoScrollEnabled;
                            break;
                    }
                }
            });
        }

        public static void Terminate()
        {
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
                new string('-', Console.BufferWidth) + '\n' + "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"
            );
            // Write lines
            for (int i = 0; i < Console.BufferHeight - 3; i++)
            {
                int lineIndex = i + TopLineIndex;
                Console.Write(
                    ANSIBuilder.Cursor.Position(i + 2, 0) +
                    ANSIBuilder.Eraser.LineCursorToEnd() + 
                    (lineIndex < Lines.Count ? Lines[lineIndex].Text : String.Empty)
                );
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
            FancyLoggerBufferLine line = new FancyLoggerBufferLine(text);
            return WriteNewLineAfter(lineId, line);
        }
        public static FancyLoggerBufferLine? WriteNewLineAfter(int lineId, FancyLoggerBufferLine line)
        {
            // Get line index
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1) return null;
            // Save top line
            int topLineId = Lines[TopLineIndex].Id;
            // Add
            Lines.Insert(lineIndex + 1, line);
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
            // Get last id
            if (Lines.Count > 0)
            {
                int lineId = Lines.Last().Id;
                return WriteNewLineAfter(lineId, line);
            }
            else
            {
                Lines.Add(line);
                return line;
            }
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
