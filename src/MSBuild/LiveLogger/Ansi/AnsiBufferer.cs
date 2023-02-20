// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging.Ansi
{
    internal sealed class AnsiBufferer
    {
        internal string Fill() => string.Format("\x1b#8");

        internal string UseAlternateBuffer() => "\x1b[?1049h";

        internal string UseMainBuffer() => "\x1b[?1049l";
    }
}
