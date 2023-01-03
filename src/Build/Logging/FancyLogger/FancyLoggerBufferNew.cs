// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging.FancyLogger
{
    public class FancyLoggerBufferLineNew
    {
        private static int Counter = 0;
        public int Id;
        public string Text;

        public FancyLoggerBufferLineNew()
        {
            Id = Counter++;
            Text = String.Empty;
        }
        public FancyLoggerBufferLineNew(string text)
            : this()
        {
            Text = text;
        }
    }

    public class FancyLoggerBufferNew
    {
        private static string Header = String.Empty;
        private static string Footer = String.Empty;
        private static List<FancyLoggerBufferLineNew> Lines;
        private static int TopLineIndex = 0;
        public static void Initialize()
        {
            // Use alternate buffer
            Console.Write(ANSIBuilder.Buffer.UseAlternateBuffer());

            // TODO: Remove
            Header = "This is ms build header";
            Footer = "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~";
        }

        #region Rendering
        public static void Render()
        {
            // First clear all the screen
            Console.Write(ANSIBuilder.Eraser.Display());
            // Adjust top line index
            if (TopLineIndex < 0) TopLineIndex = 0;
            if (TopLineIndex >= Lines.Count) TopLineIndex = Lines.Count - 1;
            // Write Header
            Console.Write(
                ANSIBuilder.Cursor.Home() +
                ANSIBuilder.Formatting.Inverse(ANSIBuilder.Alignment.Center("MSBuild - Build in progress"))
            );
            // Write footer
            Console.Write(
                ANSIBuilder.Cursor.Position(Console.BufferHeight - 2, 0) +
                new string('-', Console.BufferWidth) + '\n' + text
            );
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

        public static FancyLoggerBufferLineNew? GetLineById(int lineId)
        {
            int index = GetLineIndexById(lineId);
            if (index == -1) return null;
            return Lines[index];
        }
        #endregion

        #region Line create, update and delete
        // Write new line
        public void WriteNewLineAfter(int lineId, string text)
        {
            FancyLoggerBufferLineNew line = new FancyLoggerBufferLineNew(text);
            WriteNewLineAfter(lineId, line);
        }
        public void WriteNewLineAfter(int lineId, FancyLoggerBufferLineNew line)
        {
            // Get line index
            int lineIndex = GetLineIndexById(lineId);
            if (lineIndex == -1) return;
            // Save top line
            int topLineId = Lines[TopLineIndex].Id;
            // Add
            Lines.Insert(lineIndex + 1, line);
            // Get updated top line index
            TopLineIndex = GetLineIndexById(topLineId);
        }

        public void WriteNewLine(string text)
        {
            FancyLoggerBufferLineNew line = new FancyLoggerBufferLineNew(text);
            WriteNewLine(line);
        }
        public void WriteNewLine(FancyLoggerBufferLineNew line)
        {
            // Get last id
            int lineId = Lines.Last().Id;
            WriteNewLineAfter(lineId, line);
        }

        // Update line
        public void UpdateLine(int lineId, string text)
        {
            // Get line
            FancyLoggerBufferLineNew? line = GetLineById(lineId);
            if (line == null) return;
            line.Text = text;
        }

        // Delete line
        public void DeleteLine(int lineId)
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
            TopLineIndex = GetLineIndexById(topLineId);
        }
        #endregion


    }
}
