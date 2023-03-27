// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.Build.Logging.LiveLogger;

internal sealed class Terminal : ITerminal
{
    private Encoding _originalOutputEncoding;

    private StringBuilder _outputBuilder = new();

    private bool _isBuffering = false;

    public Terminal()
    {
        _originalOutputEncoding = Console.OutputEncoding;
        Console.OutputEncoding = Encoding.UTF8;
    }

    public void BeginUpdate()
    {
        if (_isBuffering)
        {
            throw new InvalidOperationException();
        }
        _isBuffering = true;
    }

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

    public void WriteLineFitToWidth(ReadOnlySpan<char> input)
    {
        WriteLine(input.Slice(0, Math.Min(input.Length, Console.BufferWidth - 1)));
    }

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

    public void Dispose()
    {
        Console.OutputEncoding = _originalOutputEncoding;
    }
}
