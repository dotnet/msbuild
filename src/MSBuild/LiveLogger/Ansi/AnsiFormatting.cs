// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.Ansi
{
    internal sealed class AnsiFormatting
    {
        internal string Color(string text, ForegroundColor color) => string.Format("\x1b[{0}m{1}\x1b[0m", (int)color, text);

        internal string Color(string text, BackgroundColor color) => string.Format("\x1b[{0}m{1}\x1b[0m", (int)color, text);

        internal string Color(string text, BackgroundColor backgrdoundColor, ForegroundColor foregroundColor) => string.Format("\x1b[{0};{1}m{2}\x1b[0m", (int)backgrdoundColor, (int)foregroundColor, text);

        internal string Bold(string text) => string.Format("\x1b[1m{0}\x1b[22m", text);

        internal string Dim(string text) => string.Format("\x1b[2m{0}\x1b[22m", text);

        internal string Italic(string text) => string.Format("\x1b[3m{0}\x1b[23m", text);

        internal string Underlined(string text) => string.Format("\x1b[4m{0}\x1b[24m", text);

        internal string DoubleUnderlined(string text) => string.Format("\x1b[21m{0}\x1b[24m", text);

        internal string Blinking(string text) => string.Format("\x1b[5m{0}\x1b[25m", text);

        internal string Inverse(string text) => string.Format("\x1b[7m{0}\x1b[27m", text);

        internal string Invisible(string text) => string.Format("\x1b[8m{0}\x1b[28m", text);

        internal string CrossedOut(string text) => string.Format("\x1b[9m{0}\x1b[29m", text);

        internal string Overlined(string text) => string.Format("\x1b[53m{0}\x1b[55m", text);

        internal string Hyperlink(string text, string rawUrl)
        {
            string url = rawUrl.Length > 0 ? new Uri(rawUrl).AbsoluteUri : rawUrl;
            return $"\x1b]8;;{url}\x1b\\{text}\x1b]8;;\x1b\\";
        }

        internal string DECLineDrawing(string text) => string.Format("\x1b(0{0}\x1b(B", text);
    }
}
