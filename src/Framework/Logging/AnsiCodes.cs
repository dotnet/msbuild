// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
#if NET
using System.Buffers;
#endif

namespace Microsoft.Build.Framework.Logging;

/// <summary>
/// A collection of standard ANSI/VT100 control codes.
/// </summary>
internal static class AnsiCodes
{
#if NET
    private static readonly SearchValues<char> s_programStatusIdentifierCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.+-");
#endif

    internal enum ProgramStatus
    {
        Idle,
        Working,
        Done,
        Blocked,
        Error,
        Clear
    }

    /// <summary>
    /// The control sequence introducer.
    /// </summary>
    public const string CSI = "\x1b[";

    /// <summary>
    /// Select graphic rendition.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/>color-code<see cref="SetColor"/> to change text color.
    /// </remarks>
    public const string SetColor = ";1m";

    /// <summary>
    /// Select graphic rendition - set bold mode.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/><see cref="SetBold"/> to change text to bold.
    /// </remarks>
    public const string SetBold = "1m";

    /// <summary>
    /// A shortcut to reset color back to normal.
    /// </summary>
    public const string SetDefaultColor = CSI + "m";

    /// <summary>
    /// Non-xterm extension to render a hyperlink.
    /// </summary>
    /// <remarks>
    /// Print <see cref="LinkPrefix"/>url<see cref="LinkInfix"/>text<see cref="LinkSuffix"/> to render a hyperlink.
    /// </remarks>
    public const string LinkPrefix = "\x1b]8;;";

    /// <summary>
    /// <see cref="LinkPrefix"/>
    /// </summary>
    public const string LinkInfix = "\x1b\\";

    /// <summary>
    /// <see cref="LinkPrefix"/>
    /// </summary>
    public const string LinkSuffix = "\x1b]8;;\x1b\\";

    /// <summary>
    /// Moves up the specified number of lines and puts cursor at the beginning of the line.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/>N<see cref="MoveUpToLineStart"/> to move N lines up.
    /// </remarks>
    public const string MoveUpToLineStart = "F";

    /// <summary>
    /// Moves forward (to the right) the specified number of characters.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/>N<see cref="MoveForward"/> to move N characters forward.
    /// </remarks>
    public const string MoveForward = "C";

    /// <summary>
    /// Moves backward (to the left) the specified number of characters.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/>N<see cref="MoveBackward"/> to move N characters backward.
    /// </remarks>
    public const string MoveBackward = "D";

    /// <summary>
    /// Clears everything from cursor to end of screen.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/><see cref="EraseInDisplay"/> to clear.
    /// </remarks>
    public const string EraseInDisplay = "J";

    /// <summary>
    /// Clears everything from cursor to the end of the current line.
    /// </summary>
    /// <remarks>
    /// Print <see cref="CSI"/><see cref="EraseInLine"/> to clear.
    /// </remarks>
    public const string EraseInLine = "K";

    /// <summary>
    /// Hides the cursor.
    /// </summary>
    public const string HideCursor = "\x1b[?25l";

    /// <summary>
    /// Shows/restores the cursor.
    /// </summary>
    public const string ShowCursor = "\x1b[?25h";

    /// <summary>
    /// Set progress state to a busy spinner. <br/>
    /// Note: this code works only on ConEmu terminals, and conflicts with push a notification code on iTerm2.
    /// </summary>
    /// <remarks>
    /// <see href="https://conemu.github.io/en/AnsiEscapeCodes.html#ConEmu_specific_OSC">ConEmu specific OSC codes.</see><br/>
    /// <see href="https://iterm2.com/documentation-escape-codes.html">iTerm2 proprietary escape codes.</see>
    /// </remarks>
    public const string SetProgressIndeterminate = "\x1b]9;4;3;\x1b\\";

    /// <summary>
    /// Remove progress state, restoring taskbar status to normal. <br/>
    /// Note: this code works only on ConEmu terminals, and conflicts with push a notification code on iTerm2.
    /// </summary>
    /// <remarks>
    /// <see href="https://conemu.github.io/en/AnsiEscapeCodes.html#ConEmu_specific_OSC">ConEmu specific OSC codes.</see><br/>
    /// <see href="https://iterm2.com/documentation-escape-codes.html">iTerm2 proprietary escape codes.</see>
    /// </remarks>
    public const string RemoveProgress = "\x1b]9;4;0;\x1b\\";

    /// <summary>
    /// Creates an OSC 7501 report. Each report replaces the record addressed by <paramref name="id"/>.
    /// </summary>
    /// <param name="status">The program status, or <see cref="ProgramStatus.Clear"/> to remove the record and its descendants.</param>
    /// <param name="id">A hierarchical record ID, or null for the root record.</param>
    /// <param name="app">A machine-readable application name, or null to inherit it from an ancestor.</param>
    /// <param name="title">A human-readable title. Control characters are replaced and long titles are truncated to fit the protocol limit.</param>
    public static string SetProgramStatus(ProgramStatus status, string? id = null, string? app = null, string? title = null)
    {
        string state = status switch
        {
            ProgramStatus.Idle => "idle",
            ProgramStatus.Working => "working",
            ProgramStatus.Done => "done",
            ProgramStatus.Blocked => "blocked",
            ProgramStatus.Error => "error",
            ProgramStatus.Clear => "clear",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        if (id is not null)
        {
            ValidateProgramStatusIdentifier(id, nameof(id), allowHierarchy: true);
        }
        if (app is not null)
        {
            ValidateProgramStatusIdentifier(app, nameof(app), allowHierarchy: false);
        }

        StringBuilder builder = new();
        builder.Append("\x1b]7501;state=").Append(state);
        if (id is not null)
        {
            builder.Append(":id=").Append(id);
        }
        if (app is not null)
        {
            builder.Append(":app=").Append(app);
        }
        if (title is not null)
        {
            builder.Append(":title=").Append(EncodeProgramStatusTitle(title));
        }
        return builder.Append("\x1b\\").ToString();
    }

    private static void ValidateProgramStatusIdentifier(ReadOnlySpan<char> value, string parameterName, bool allowHierarchy)
    {
        if (value.Length == 0 || value.Length > (allowHierarchy ? 128 : 32))
        {
            throw new ArgumentException("Invalid OSC 7501 identifier length.", parameterName);
        }

        int depth = 1;
        while (true)
        {
            int separator = allowHierarchy ? value.IndexOf('/') : -1;
            ReadOnlySpan<char> segment = separator < 0 ? value : value.Slice(0, separator);
            if (segment.IsEmpty)
            {
                throw new ArgumentException("Invalid OSC 7501 identifier hierarchy.", parameterName);
            }
            if (segment.Length > 32)
            {
                throw new ArgumentException("Invalid OSC 7501 identifier segment length.", parameterName);
            }
#if NET
            if (segment.ContainsAnyExcept(s_programStatusIdentifierCharacters))
            {
                throw new ArgumentException("Invalid OSC 7501 identifier character.", parameterName);
            }
#else
            foreach (char character in segment)
            {
                if (character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.' or '+' or '-'))
                {
                    throw new ArgumentException("Invalid OSC 7501 identifier character.", parameterName);
                }
            }
#endif
            if (separator < 0)
            {
                return;
            }
            if (++depth > 8)
            {
                throw new ArgumentException("Invalid OSC 7501 identifier hierarchy.", parameterName);
            }
            value = value.Slice(separator + 1);
        }
    }

    private static string EncodeProgramStatusTitle(ReadOnlySpan<char> title)
    {
        // 64 UTF-16 code units require at most 192 UTF-8 bytes, the protocol's title limit.
        int length = Math.Min(title.Length, 64);
        if (length > 0 && char.IsHighSurrogate(title[length - 1]))
        {
            length--;
        }

        Span<char> sanitized = stackalloc char[64];
        for (int i = 0; i < length; i++)
        {
            char character = title[i];
            sanitized[i] = char.IsControl(character) ? ' ' : character;
        }
#if NET
        Span<byte> utf8 = stackalloc byte[192];
        int byteCount = Encoding.UTF8.GetBytes(sanitized.Slice(0, length), utf8);
        return Convert.ToBase64String(utf8.Slice(0, byteCount));
#else
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(sanitized.Slice(0, length).ToString()));
#endif
    }

    public static string Colorize(string? s, TerminalColor color)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return s ?? "";
        }

        return $"{CSI}{(int)color}{SetColor}{s}{SetDefaultColor}";
    }

    public static string MakeBold(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return s ?? "";
        }

        return $"{CSI}{SetBold}{s}{SetDefaultColor}";
    }

    public static string MoveCursorBackward(int count) => $"{CSI}{count}{MoveBackward}";

    /// <summary>
    /// Moves cursor to the specified column, or the rightmost column if <paramref name="column"/> is greater than the width of the terminal.
    /// </summary>
    /// <param name="column">Column index.</param>
    /// <returns>Control codes to set the desired position.</returns>
    public static string SetCursorHorizontal(int column) => $"{CSI}{column}G";
}
