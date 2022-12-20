// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging.FancyLogger
{
    /// <summary>
    /// Represents an identifiable line inside the fancylogger buffer.
    /// </summary>
    public class FancyLoggerBufferLine
    {
        private static int counter = 0;
        public int Id;
        public string Text;
        public bool IsHidden;
        public int IdentationLevel = -1;
        public FancyLoggerBufferLine()
        {
            Id = counter++;
            Text = String.Empty;
        }
        public FancyLoggerBufferLine(string text)
        {
            Id = counter++;
            Text = text;
        }
        public FancyLoggerBufferLine(string text, int identationLevel) : this(text)
        {
            IdentationLevel = identationLevel;
        }
        public void Hide()
        {
            IsHidden = true;
        }
        public void Unhide()
        {
            IsHidden = false;
        }
        public int GetIndex()
        {
            return FancyLoggerBuffer.GetLineIndexById(Id);
        }
    }

    /// <summary>
    /// Buffer manager for FancyLogger
    /// </summary>
    internal static class FancyLoggerBuffer
    {
        // Status
        public static bool AutoScrollEnabled { get; private set; }
        public static bool IsTerminated { get; private set; }
        public static int CurrentTopLineIndex { get; private set; }
        public static int Height { get { return Console.WindowHeight; } }
        // Lines to be presented by the buffer
        private static List<FancyLoggerBufferLine> lines = new();
        public static void Initialize()
        {
            // Setup event listeners
            Task.Run(() =>
            {
                while (true)
                {
                    if (IsTerminated) return;
                    switch (Console.ReadKey().Key)
                    {
                        case ConsoleKey.Q:
                            IsTerminated = true;
                        break;
                        case ConsoleKey.UpArrow:
                            ScrollToLine(CurrentTopLineIndex - 1);
                            break;
                        case ConsoleKey.DownArrow:
                            ScrollToLine(CurrentTopLineIndex + 1);
                            break;
                        case ConsoleKey.Home:
                            ScrollToLine(0);
                            break;
                        case ConsoleKey.End:
                            ScrollToEnd();
                            break;
                        case ConsoleKey.Spacebar:
                        case ConsoleKey.Escape:
                            ToggleAutoScroll();
                            break;
                    }
                }
            });
            // Switch to alternate
            Console.Write(ANSIBuilder.Buffer.UseMainBuffer());
            Console.Write(ANSIBuilder.Buffer.UseAlternateBuffer());
            // Settings
            AutoScrollEnabled = true;
            // Render contents
            WriteTitleBar();
            WriteFooter("~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            ScrollToEnd();
        }
        public static void Terminate()
        {
            // Switch to main buffer
            Console.Write(ANSIBuilder.Buffer.UseMainBuffer());
            // Dispose event listeners
            IsTerminated = true;
            // Delete lines
            lines = new();
        }

        #region Scrolling
        private static void ScrollToLine(int firstLineIndex)
        {
            if (firstLineIndex < 0 || firstLineIndex >= lines.Count) return;
            CurrentTopLineIndex = firstLineIndex;
            int i = 0;
            while (i < Height - 4)
            {
                int lineIndex = i + firstLineIndex;
                Console.Write(""
                    + ANSIBuilder.Cursor.Position(i + 2, 0)
                    + ANSIBuilder.Eraser.LineCursorToEnd()
                    // + ((lineIndex < lines.Count && lines[lineIndex].IsHidden) ? " Hidden" : "")
                    + ((lineIndex < lines.Count) ? ANSIBuilder.Tabulator.ForwardTab(lines[lineIndex].IdentationLevel) + lines[lineIndex].Text : "")
                );
                i++;
            }

            Console.Write(ANSIBuilder.Cursor.Position(Height, 0));
        }
        private static void ScrollToEnd()
        { 
            // If number of lines is smaller than height
            if (lines.Count < Height - 2)
            {
                ScrollToLine(0);
            }
            else
            {
                ScrollToLine(lines.Count - Height + 4);
            }
            // Go to end
            Console.Write(ANSIBuilder.Cursor.Position(Height, 0));
        }
        private static void ToggleAutoScroll()
        {
            AutoScrollEnabled = !AutoScrollEnabled;
        }
        #endregion
        #region Line Referencing
        public static int GetLineIndexById(int lineId)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Id == lineId) return i;
            }
            return -1;
        }
        public static FancyLoggerBufferLine? GetLineById(int lineId)
        {
            int i = GetLineIndexById(lineId);
            if (i == -1) return null;
            return lines[i];
        }
        #endregion
        #region Writing
        public static void WriteTitleBar()
        {
            Console.Write(""
                + ANSIBuilder.Cursor.Home()
                + ANSIBuilder.Formatting.Inverse(ANSIBuilder.Alignment.Center("MSBuild - Build in progress"))
            );
        }
        public static void WriteFooter(string text)
        {
            Console.Write(""
                + ANSIBuilder.Cursor.Position(Height - 2, 0) // Position at bottom
                + new string('-', Console.BufferWidth) + "\n"
                + ANSIBuilder.Eraser.LineCursorToEnd()
                + text
            );
        }
        public static FancyLoggerBufferLine WriteNewLine(string text)
        {
            // Create line
            FancyLoggerBufferLine line = new FancyLoggerBufferLine(text);
            return WriteNewLine(line);
        }
        public static FancyLoggerBufferLine WriteNewLine(FancyLoggerBufferLine line)
        {
            // Add line
            lines.Add(line);
            // Update contents
            if (AutoScrollEnabled) ScrollToEnd();
            else ScrollToLine(CurrentTopLineIndex);
            return line;
        }
        public static FancyLoggerBufferLine? WriteNewLineAfter(string text, int lineId)
        {
            // get line
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1) return null;

            FancyLoggerBufferLine line = new FancyLoggerBufferLine(text);
            return WriteNewLineAfterIndex(line, lineIndex);
        }

        public static FancyLoggerBufferLine? WriteNewLineAfter(FancyLoggerBufferLine line, int lineId)
        {
            // get line
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1) return null;

            return WriteNewLineAfterIndex(line, lineIndex);
        }

        public static FancyLoggerBufferLine? WriteNewLineAfterIndex(FancyLoggerBufferLine line, int lineIndex)
        {
            if (lineIndex == -1) return null;
            lines.Insert(lineIndex + 1, line);
            // Scroll to end if lineIndex >= lines
            if (lineIndex >= lines.Count -2 && AutoScrollEnabled) ScrollToEnd();
            else ScrollToLine(CurrentTopLineIndex);
            return line;
        }

        public static void DeleteLine(int lineId)
        {
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1) return;
            lines.RemoveAt(lineIndex);
            ScrollToLine(CurrentTopLineIndex);
        }

        public static FancyLoggerBufferLine? UpdateLine(int lineId, string text)
        {
            FancyLoggerBufferLine? line = GetLineById(lineId);
            if (line == null) return null;

            line.Text = text;
            ScrollToLine(CurrentTopLineIndex);
            return line;
        }
        #endregion

        public static void HideLine(int lineId)
        {
            FancyLoggerBufferLine? line = GetLineById(lineId);
            if (line == null) return;
            line.Hide();
            ScrollToLine(CurrentTopLineIndex);
        }
        public static void UnhideLine(int lineId)
        {
            FancyLoggerBufferLine? line = GetLineById(lineId);
            if (line == null) return;
            line.Unhide();
            ScrollToLine(CurrentTopLineIndex);
        }
    }
}
