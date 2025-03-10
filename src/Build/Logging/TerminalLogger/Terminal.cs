// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework.Logging;
#if NETFRAMEWORK
using Microsoft.Build.Shared;
#endif

namespace Microsoft.Build.Logging;

/// <summary>
/// An <see cref="ITerminal"/> implementation for ANSI/VT100 terminals.
/// </summary>
internal sealed class Terminal : ITerminal
{
    /// <summary>
    /// The encoding read from <see cref="Console.OutputEncoding"/> when the terminal is constructed.
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

    internal TextWriter Output { private get; set; }

    private const int BigUnknownDimension = 2 << 23;

    /// <inheritdoc/>
    public int Height
    {
        get
        {
            if (Console.IsOutputRedirected)
            {
                return BigUnknownDimension;
            }

            return Console.BufferHeight;
        }
    }

    /// <inheritdoc/>
    public int Width
    {
        get
        {
            if (Console.IsOutputRedirected)
            {
                return BigUnknownDimension;
            }

            return Console.BufferWidth;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// https://github.com/dotnet/msbuild/issues/8958: iTerm2 treats ;9 code to post a notification instead, so disable progress reporting on Mac.
    /// </remarks>
    public bool SupportsProgressReporting { get; } = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public Terminal()
    {
        _originalOutputEncoding = Console.OutputEncoding;
        Console.OutputEncoding = Encoding.UTF8;

        // Capture the TextWriter AFTER setting the encoding, because setting
        // the encoding creates a new TextWriter in the Console class, but it's
        // possible to hang on to the old one (with the wrong encoding) and emit
        // garbage, as in https://github.com/dotnet/msbuild/issues/9030.
        Output = Console.Out;
    }

    internal Terminal(TextWriter output)
    {
        Output = output;

        _originalOutputEncoding = Encoding.UTF8;
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

        Output.Write(_outputBuilder.ToString());
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
            Output.Write(text);
        }
    }

    /// <inheritdoc/>
    public void Write(ReadOnlySpan<char> text)
    {
        if (_isBuffering)
        {
            _outputBuilder.Append(text);
        }
        else
        {
            Output.Write(text);
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
            Output.WriteLine(text);
        }
    }

    /// <inheritdoc/>
    public void WriteLineFitToWidth(ReadOnlySpan<char> text)
    {
        ReadOnlySpan<char> truncatedText = text.Slice(0, Math.Min(text.Length, Width - 1));
        if (_isBuffering)
        {
            _outputBuilder.Append(truncatedText);
            _outputBuilder.AppendLine();
        }
        else
        {
            Output.WriteLine(truncatedText);
        }
    }

    /// <inheritdoc/>
    public void WriteColor(TerminalColor color, string text)
    {
        if (_isBuffering)
        {
            _outputBuilder
                .Append(AnsiCodes.CSI)
                .Append((int)color)
                .Append(AnsiCodes.SetColor)
                .Append(text)
                .Append(AnsiCodes.SetDefaultColor);
        }
        else
        {
            Write(AnsiCodes.Colorize(text, color));
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
            WriteLine($"{AnsiCodes.CSI}{(int)color}{AnsiCodes.SetColor}{text}{AnsiCodes.SetDefaultColor}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Console.OutputEncoding = _originalOutputEncoding;
        }
        catch
        {
            // In some terminal emulators setting back the previous console output encoding fails.
            // See https://github.com/dotnet/msbuild/issues/9662.
            // We do not want to throw an exception if it happens, since it is a non-essentual failure in the logger.
        }
    }
}
