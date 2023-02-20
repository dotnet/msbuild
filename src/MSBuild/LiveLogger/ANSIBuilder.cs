// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Build.Logging.Ansi;

namespace Microsoft.Build.Logging
{
    internal static class AnsiBuilder
    {
        private const string AnsiPattern = @"\x1b(?:[@-Z\-_]|\[[0-?]*[ -\/]*[@-~]|(?:\]8;;.*?\x1b\\))";

        // TODO: This should replace AnsiPattern once LiveLogger's API is internal
        private static readonly Regex s_ansiRegex = new(AnsiPattern);

        static AnsiBuilder()
        {
            Aligner = new(AnsiRemove);
            Bufferer = new();
            Graphics = new();
            Eraser = new();
            Cursor = new();
            Formatter = new();
        }

        internal static AnsiAligner Aligner { get; }

        internal static AnsiBufferer Bufferer { get; }

        internal static AnsiGraphics Graphics { get; }

        internal static AnsiEraser Eraser { get; }

        internal static AnsiCursor Cursor { get; }

        internal static AnsiFormatting Formatter { get; }

        internal static string AnsiRemove(string text) => s_ansiRegex.Replace(text, "");

        /// <summary>
        /// Find a place to break a string after a number of visible characters, not counting VT-100 codes.
        /// </summary>
        /// <param name="text">String to split.</param>
        /// <param name="position">Number of visible characters to split after.</param>
        /// <returns>Index in <paramref name="text"/> that represents <paramref name="position"/> visible characters.</returns>
        // TODO: This should be an optional parameter for AnsiBreakpoint(string text, int positioon, int initialPosition = 0)
        internal static int AnsiBreakpoint(string text, int position) => AnsiBreakpoint(text, position, 0);

        internal static int AnsiBreakpoint(string text, int position, int initialPosition)
        {
            if (position >= text.Length)
            {
                return text.Length;
            }

            int nonAnsiIndex = 0;
            Match nextMatch = s_ansiRegex.Match(text, initialPosition);
            int logicalIndex = 0;
            while (logicalIndex < text.Length && nonAnsiIndex != position)
            {
                // Jump over ansi codes
                if (logicalIndex == nextMatch.Index && nextMatch.Length > 0)
                {
                    logicalIndex += nextMatch.Length;
                    nextMatch = nextMatch.NextMatch();
                }

                // Increment non ansi index
                nonAnsiIndex++;
                logicalIndex++;
            }

            return logicalIndex;
        }

        internal static List<string> AnsiWrap(string text, int maxLength)
        {
            ReadOnlySpan<char> textSpan = text.AsSpan();
            List<string> result = new();
            int breakpoint = AnsiBreakpoint(text, maxLength);
            while (textSpan.Length > breakpoint)
            {
                result.Add(textSpan.Slice(0, breakpoint).ToString());
                textSpan = textSpan.Slice(breakpoint);
                breakpoint = AnsiBreakpoint(text, maxLength, breakpoint);
            }

            result.Add(textSpan.ToString());
            return result;
        }
    }
}
