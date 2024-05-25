// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Portions of the code in this file were ported from the spectre.console by Patrik Svensson, Phil Scott, Nils Andresen
// https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Internal/Backends/Ansi/AnsiDetector.cs
// and from the supports-ansi project by Qingrong Ke
// https://github.com/keqingrong/supports-ansi/blob/master/index.js

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Framework.Logging
{
    internal class AnsiDetector
    {
        private static readonly Regex[] terminalsRegexes =
        {
            new("^xterm"), // xterm, PuTTY, Mintty
            new("^rxvt"), // RXVT
            new("^(?!eterm-color).*eterm.*"), // Accepts eterm, but not eterm-color, which does not support moving the cursor, see #9950.
            new("^screen"), // GNU screen, tmux
            new("tmux"), // tmux
            new("^vt100"), // DEC VT series
            new("^vt102"), // DEC VT series
            new("^vt220"), // DEC VT series
            new("^vt320"), // DEC VT series
            new("ansi"), // ANSI
            new("scoansi"), // SCO ANSI
            new("cygwin"), // Cygwin, MinGW
            new("linux"), // Linux console
            new("konsole"), // Konsole
            new("bvterm"), // Bitvise SSH Client
            new("^st-256color"), // Suckless Simple Terminal, st
            new("alacritty"), // Alacritty
        };

        internal static bool IsAnsiSupported(string termType)
        {
            if (string.IsNullOrEmpty(termType))
            {
                return false;
            }

            if (terminalsRegexes.Any(regex => regex.IsMatch(termType)))
            {
                return true;
            }

            return false;
        }
    }
}
