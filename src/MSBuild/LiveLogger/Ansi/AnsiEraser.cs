// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.Ansi
{
    internal sealed class AnsiEraser
    {
        internal string DisplayCursorToEnd() => string.Format("\x1b[0J");

        internal string DisplayStartToCursor() => string.Format("\x1b[1J");

        internal string Display() => String.Format("\x1b[2J");

        internal string LineCursorToEnd() => string.Format("\x1b[0K");

        internal string LineStartToCursor() => string.Format("\x1b[1K");

        internal string Line() => string.Format("\x1b[2k");
    }
}
