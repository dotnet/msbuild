// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.Build.Logging.LiveLogger;

/// <summary>
/// An <see cref="ITerminal"/> implementation for ANSI/VT100 terminals.
/// </summary>
internal sealed class Terminal : ITerminal
{
    /// <summary>
    /// The encoding read from <see cref="Console.OutputEncoding"/> when the terminal is instantiated.
    /// </summary>
    private readonly Encoding _originalOutputEncoding;

    /// <summary>
    /// A string buffer used with <see cref="BeginUpdate"/>/<see cref="EndUpdate"/>.
    /// </summary>
    private readonly StringBuilder _outputBuilder = new();

    /// <summary>
    /// True if <see cref="BeginUpdate"/> was called and <c>Write*</c> methods are buffering instead of directly printing.
    /// </summary>
    private bool _isBuffering = false;

    public Terminal()
    {
        _originalOutputEncoding = Console.OutputEncoding;
        Console.OutputEncoding = Encoding.UTF8;
    }

    /// <inheritdoc/>
    public void BeginUpdate()
    {
        if (_isBuffering)
        {
            throw new InvalidOperationException();
        }
        _isBuffering = true;
    }

    /// <inheritdoc/>
    public void EndUpdate()
    {
        if (!_isBuffering)
        {
            throw new InvalidOperationException();
        }
        _isBuffering = false;

        Console.Write(_outputBuilder.ToString());
        _outputBuilder.Clear();
    }

    /// <inheritdoc/>
    public void Write(string text)
    {
        if (_isBuffering)
        {
            _outputBuilder.Append(text);
        }
        else
        {
            Console.Write(text);
        }
    }

    /// <inheritdoc/>
    public void WriteLine(string text)
    {
        if (_isBuffering)
        {
            _outputBuilder.AppendLine(text);
        }
        else
        {
            Console.WriteLine(text);
        }
    }

    /// <inheritdoc/>
    public void WriteLine(ReadOnlySpan<char> text)
    {
        if (_isBuffering)
        {
            _outputBuilder.Append(text);
            _outputBuilder.AppendLine();
        }
        else
        {
            Console.Out.WriteLine(text);
        }
    }

    /// <inheritdoc/>
    public void WriteLineFitToWidth(ReadOnlySpan<char> input)
    {
        WriteLine(input.Slice(0, Math.Min(input.Length, Console.BufferWidth - 1)));
    }

    /// <inheritdoc/>
    public void WriteColor(TerminalColor color, string text)
    {
        if (_isBuffering)
        {
            _outputBuilder
                .Append("\x1b[")
                .Append((int)color)
                .Append(";1m")
                .Append(text)
                .Append("\x1b[m");
        }
        else
        {
            Write($"\x1b[{(int)color};1m{text}\x1b[m");
        }
    }

    /// <inheritdoc/>
    public void WriteColorLine(TerminalColor color, string text)
    {
        if (_isBuffering)
        {
            WriteColor(color, text);
            _outputBuilder.AppendLine();
        }
        else
        {
            WriteLine($"\x1b[{(int)color};1m{text}\x1b[m");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Console.OutputEncoding = _originalOutputEncoding;
    }
}
