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
    internal class FancyLoggerBufferLine
    {
        private static int counter = 0;
        public int Id;
        public string Text;
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
    }
    internal static class FancyLoggerBuffer
    {
        private static List<FancyLoggerBufferLine> lines = new();
        private static int Height = 0;
        private static int CurrentTopLineIndex = 0;
        public static void Initialize()
        {
            // Setup event listeners
            var arrowsPressTask = Task.Run(() =>
            {
                while (true)
                {
                    switch (Console.ReadKey().Key)
                    {
                        case ConsoleKey.UpArrow:
                            ScrollUp();
                            break;
                        case ConsoleKey.DownArrow:
                            ScrollDown();
                            break;
                    }
                }
            });
            // Switch to alternate buffer
            Console.Write(ANSIBuilder.Buffer.UseAlternateBuffer());
            // Update dimensions
            Height = Console.BufferHeight;
            // TODO: Remove. Just testing
            for (int i = 0; i < 60; i++)
            {
                FancyLoggerBufferLine line = new FancyLoggerBufferLine($"Line {i}");
                lines.Add(line);
            }
            // Render contents
            RenderTitleBar();
            RenderFooter();
            ScrollToEnd();
        }
        private static void RenderTitleBar()
        {
            Console.Write(""
                + ANSIBuilder.Cursor.Home()
                + ANSIBuilder.Formatting.Inverse("                         MSBuild                         ")
            );
        }
        private static void RenderFooter()
        {
            Console.Write(""
                + ANSIBuilder.Cursor.Position(Height - 2, 0) // Position at bottom
                + "---------------------------------------------------------\n"
                + "Build: 13%"
            );
        }

        private static void ScrollToLine(int firstLineIndex)
        {
            if (firstLineIndex < 0) return;
            if (firstLineIndex >= lines.Count) return;
            CurrentTopLineIndex = firstLineIndex;
            for (int i = 0; i < Height - 4; i++)
            {
                // If line exists
                if (i + firstLineIndex < lines.Count)
                {
                    Console.Write(""
                        + ANSIBuilder.Cursor.Position(i + 2, 0)
                        + ANSIBuilder.Eraser.LineCursorToEnd()
                        + lines[i + firstLineIndex].Text);
                } else
                {
                    Console.Write(""
                        + ANSIBuilder.Cursor.Position(i + 2, 0)
                        + ANSIBuilder.Eraser.LineCursorToEnd()
                    );
                }
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

        private static void ScrollUp()
        {
            ScrollToLine(CurrentTopLineIndex - 1);
        }

        private static void ScrollDown()
        {
            ScrollToLine(CurrentTopLineIndex + 1);
        }

        public static void WriteNewLine(string text)
        {
            // Create line
            FancyLoggerBufferLine line = new FancyLoggerBufferLine(text);
            // Add line
            lines.Add(line);
            // Update contents
            ScrollToEnd();
        }
    }
}
