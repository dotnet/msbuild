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
    internal class LogWriterLine
    {
        private static int Counter = 0;
        public int Id;
        public string Text = String.Empty;

        public LogWriterLine() {
            Text = String.Empty;
            Id = Counter++;
        }
        public LogWriterLine(string text) {
            Text = text;
            Id = Counter++;
        }

        public void Update(string text)
        {
            LogWriterOld.UpdateLine(Id, text);
        }

        public void Delete()
        {
            LogWriterOld.DeleteLine(Id);
        }
    }
    internal static class LogWriterOld
    {
        public static int InitialCursorTop;
        public static List<LogWriterLine> Lines = new List<LogWriterLine>();
        public static int LastLineTop
        {
            get { return InitialCursorTop + Lines.Count; }
        }

        static int GetLineIndexById(int lineId)
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Id == lineId) return i;
            }
            return -1;
        }


        public static LogWriterLine WriteNewLine(string text) 
        {
            // Get line top
            int lineTop = LastLineTop + 1;
            // Create line
            LogWriterLine line = new LogWriterLine(text);
            // Append
            Lines.Add(line);
            // Print
            Console.Write(""
                + ANSIBuilder.Cursor.Position(lineTop, 0)
                + line.Text
                + "\n");
            // Return
            Console.Out.Flush();
            return line;
        }
        public static LogWriterLine? WriteNewLineAt(int lineId, string text)
        {
            int lineIndex = GetLineIndexById(lineId);
            if(lineIndex == -1) return null;
            return WriteNewLineAtIndex(lineIndex, text);
        }

        public static LogWriterLine? WriteNewLineAtIndex(int lineIndex, string text)
        {
            // If line index is equal to lines size, just add a new line
            if (lineIndex >= Lines.Count) return WriteNewLine(text);
            // Add one line at the end
            WriteNewLine("");
            // Shift everything one line down
            for (int i = Lines.Count - 1; i > lineIndex - 1; i--)
            {
                UpdateLineByIndex(i, Lines[i - 1].Text);
            }
            UpdateLineByIndex(lineIndex, text);
            Console.Write(ANSIBuilder.Cursor.Position(LastLineTop, 0) + "\n");
            Console.Out.Flush();
            return null;
        }
        public static void DeleteLine(int lineId)
        {
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1) return;
            DeleteLineByIndex(lineIndex);
        }
        public static void DeleteLineByIndex(int lineIndex)
        {
            // Count lines before deleition
            int currentLinesCount = Lines.Count;
            // Shift remaining lines up
            for (int i = lineIndex; i < currentLinesCount - 1; i++)
            {
                UpdateLineByIndex(i, Lines[i+1].Text);
            }
            // Erase contents from last line
            UpdateLineByIndex(currentLinesCount - 1, ANSIBuilder.Eraser.LineCursorToEnd());
            // Remove from memory
            Lines.RemoveAt(currentLinesCount - 1);
            // Position cursor
            Console.Write(ANSIBuilder.Cursor.Position(LastLineTop, 0) + "\n");
            Console.Out.Flush();
        }

        public static LogWriterLine? UpdateLine(int lineId, string text)
        {
            // Check if line exists
            int lineIndex = GetLineIndexById(lineId);
            if(lineIndex == -1) return null;
            return UpdateLineByIndex(lineIndex, text);
        }

        public static LogWriterLine? UpdateLineByIndex(int lineIndex, string text)
        {
            // Get line position
            int lineTop = lineIndex + InitialCursorTop + 1;
            // Update in list
            Lines[lineIndex].Text = text;
            // Print
            Console.Write(""
                // + ANSIBuilder.Cursor.Position(lineTop, 0)
                + ANSIBuilder.Cursor.UpAndScroll(LastLineTop - lineTop + 1)
                + ANSIBuilder.Eraser.LineCursorToEnd()
                + Lines[lineIndex].Text
                + ANSIBuilder.Cursor.Position(LastLineTop, 0)
                + "\n");
            Console.Out.Flush();
            return Lines[lineIndex];
        }
    }
}
