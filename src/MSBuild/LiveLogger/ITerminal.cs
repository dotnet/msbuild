// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.LiveLogger;

/// <summary>
/// An abstraction of a terminal, built specifically to fit the <see cref="LiveLogger"/> needs.
/// </summary>
internal interface ITerminal : IDisposable
{
    /// <summary>
    /// Width of the terminal buffer.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Height of the terminal buffer.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Starts buffering the text passed via the <c>Write*</c> methods.
    /// </summary>
    /// <remarks>
    /// Upon calling this method, the terminal should be buffering all output internally until <see cref="EndUpdate"/> is called.
    /// </remarks>
    void BeginUpdate();

    /// <summary>
    /// Flushes the text buffered between <see cref="BeginUpdate"/> was called and now into the output.
    /// </summary>
    void EndUpdate();

    /// <summary>
    /// Writes a string to the output. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void Write(string text);

    /// <summary>
    /// Writes a string to the output. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void Write(ReadOnlySpan<char> text);

    /// <summary>
    /// Writes a string to the output. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void WriteLine(string text);

    /// <summary>
    /// Writes a string to the output, truncating it if it wouldn't fit on one screen line.
    /// Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void WriteLineFitToWidth(ReadOnlySpan<char> text);

    /// <summary>
    /// Writes a string to the output using the given color. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void WriteColor(TerminalColor color, string text);

    /// <summary>
    /// Writes a string to the output using the given color. Or buffers it if <see cref="BeginUpdate"/> was called.
    /// </summary>
    void WriteColorLine(TerminalColor color, string text);

    /// <summary>
    /// Return string representing text wrapped in VT100 color codes.
    /// </summary>
    string RenderColor(TerminalColor color, string text);
}

/// <summary>
/// Enumerates the text colors supported by <see cref="ITerminal"/>.
/// </summary>
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
