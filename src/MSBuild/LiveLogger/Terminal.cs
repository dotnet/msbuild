// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.Build.Logging.LiveLogger;

internal sealed class Terminal : ITerminal
{
    private Encoding _originalOutputEncoding;

    public Terminal()
    {
        _originalOutputEncoding = Console.OutputEncoding;
        Console.OutputEncoding = Encoding.UTF8;
    }

    public void Write(string text) => Console.Write(text);
    public void WriteLine(string text) => Console.WriteLine(text);
    public void WriteLine(ReadOnlySpan<char> text) => Console.Out.WriteLine(text);

    public void WriteLineFitToWidth(ReadOnlySpan<char> input)
    {
        WriteLine(input.Slice(0, Math.Min(input.Length, Console.BufferWidth - 1)));
    }

    public void Dispose()
    {
        Console.OutputEncoding = _originalOutputEncoding;
    }
}
