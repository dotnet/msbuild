// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging.Ansi
{
    internal sealed class AnsiCursor
    {
        internal string Style(CursorStyle style) => string.Format("\x1b[{0} q", (int)style);

        internal string Up(int n = 1) => string.Format("\x1b[{0}A", n);

        internal string UpAndScroll(int n)
        {
            string result = "";
            for (int i = 0; i < n; i++)
            {
                result += "\x1bM";
            }

            return result;
        }

        internal string Down(int n = 1) => string.Format("\x1b[{0}B", n);

        internal string Forward(int n = 1) => string.Format("\x1b[{0}C", n);

        internal string Backward(int n = 1) => string.Format("\x1b[{0}D", n);

        internal string Home() => string.Format("\x1b[H");

        internal string Position(int row, int column) => string.Format("\x1b[{0};{1}H", row, column);

        internal string SavePosition() => string.Format("\x1b[s");

        internal string RestorePosition() => string.Format("\x1b[u");

        internal string Invisible() => "\x1b[?25l";

        internal string Visible() => "\x1b[?25h";
    }
}
