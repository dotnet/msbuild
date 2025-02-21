// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework.Logging;

namespace Microsoft.Build.Logging;

/// <summary>
/// An abstraction of a terminal, built specifically to fit the <see cref="TerminalLogger"/> needs.
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
    /// <see langword="true"/> if the terminal emulator supports progress reporting.
    /// </summary>
    bool SupportsProgressReporting { get; }

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
}
