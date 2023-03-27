// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.LiveLogger;

internal interface ITerminal : IDisposable
{
    void BeginUpdate();

    void EndUpdate();

    void Write(string text);

    void WriteLine(string text);

    void WriteLine(ReadOnlySpan<char> text);

    void WriteLineFitToWidth(ReadOnlySpan<char> input);

    void WriteColor(TerminalColor color, string text);

    void WriteColorLine(TerminalColor color, string text);
}

internal enum TerminalColor
{
    Black = 30,
    Red = 31,
    Green = 32,
    Yellow = 33,
    Blue = 34,
    Magenta = 35,
    Cyan = 36,
    White = 37,
    Default = 39
}
