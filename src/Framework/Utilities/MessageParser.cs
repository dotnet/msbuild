// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
#endif
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Provides helpers for extracting a diagnostic code, and optionally the remaining message, from
///  text prefixed with such a code.
/// </summary>
internal static class MessageParser
{
    /// <summary>
    ///  Attempts to extract an MSBuild code prefixed to <paramref name="text"/>. MSBuild codes match
    ///  <c>^\s*(?&lt;CODE&gt;MSB[0-9][0-9][0-9][0-9]):\s*(?&lt;MESSAGE&gt;.*)$</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="code">[out] The extracted code, or <see langword="null"/> if none was found.</param>
    /// <param name="message">
    ///  [out] The text following the code with leading whitespace removed, or <see langword="null"/>
    ///  if no code was found. Empty if the code is followed only by whitespace.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if a code was extracted; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParseMSBuildCode(string text, [NotNullWhen(true)] out string? code, [NotNullWhen(true)] out string? message)
    {
        Assumed.NotNull(text);

        var scanner = new Scanner(text);

        if (!scanner.TryReadMSBuildCode(out code))
        {
            message = null;
            return false;
        }

        scanner.SkipWhiteSpace();
        message = scanner.GetRemaining();

        return true;
    }

    /// <summary>
    ///  Attempts to extract an MSBuild code prefixed to <paramref name="text"/>. MSBuild codes match
    ///  <c>^\s*(?&lt;CODE&gt;MSB[0-9][0-9][0-9][0-9]):</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="code">[out] The extracted code, or <see langword="null"/> if none was found.</param>
    /// <returns>
    ///  <see langword="true"/> if a code was extracted; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Use this method when only the code is needed; it avoids allocating the message.
    /// </remarks>
    public static bool TryGetMSBuildCode(string text, [NotNullWhen(true)] out string? code)
    {
        Assumed.NotNull(text);

        var scanner = new Scanner(text);

        return scanner.TryReadMSBuildCode(out code);
    }

    /// <summary>
    ///  Attempts to strip an MSBuild code prefixed to <paramref name="text"/>, returning the remaining
    ///  message. MSBuild codes match <c>^\s*(?&lt;CODE&gt;MSB[0-9][0-9][0-9][0-9]):\s*(?&lt;MESSAGE&gt;.*)$</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="message">
    ///  [out] The text following the code with leading whitespace removed, or <see langword="null"/>
    ///  if no code was found. Empty if the code is followed only by whitespace.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if a code was stripped; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Use this method when only the message is needed; it avoids allocating the code.
    /// </remarks>
    public static bool TryStripMSBuildCode(string text, [NotNullWhen(true)] out string? message)
    {
        Assumed.NotNull(text);

        var scanner = new Scanner(text);

        if (!scanner.TryConsumeMSBuildCode())
        {
            message = null;
            return false;
        }

        scanner.Consume(':');
        scanner.SkipWhiteSpace();
        message = scanner.GetRemaining();

        return true;
    }

    /// <summary>
    ///  Attempts to extract an arbitrary code prefixed to <paramref name="text"/>. Codes match
    ///  <c>^\s*(?&lt;CODE&gt;[A-Za-z]+[0-9]+):\s*(?&lt;MESSAGE&gt;.*)$</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="code">[out] The extracted code, or <see langword="null"/> if none was found.</param>
    /// <param name="message">
    ///  [out] The text following the code with leading whitespace removed, or <see langword="null"/>
    ///  if no code was found. Empty if the code is followed only by whitespace.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if a code was extracted; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParseAnyCode(string text, [NotNullWhen(true)] out string? code, [NotNullWhen(true)] out string? message)
    {
        Assumed.NotNull(text);

        var scanner = new Scanner(text);

        if (!scanner.TryReadAnyCode(out code))
        {
            message = null;
            return false;
        }

        scanner.SkipWhiteSpace();
        message = scanner.GetRemaining();

        return true;
    }

    /// <summary>
    ///  Attempts to extract an arbitrary code prefixed to <paramref name="text"/>. Codes match
    ///  <c>^\s*(?&lt;CODE&gt;[A-Za-z]+[0-9]+):</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="code">[out] The extracted code, or <see langword="null"/> if none was found.</param>
    /// <returns>
    ///  <see langword="true"/> if a code was extracted; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Use this method when only the code is needed; it avoids allocating the message.
    /// </remarks>
    public static bool TryGetAnyCode(string text, [NotNullWhen(true)] out string? code)
    {
        Assumed.NotNull(text);

        var scanner = new Scanner(text);

        return scanner.TryReadAnyCode(out code);
    }

    /// <summary>
    ///  Attempts to strip an arbitrary code prefixed to <paramref name="text"/>, returning the remaining
    ///  message. Codes match <c>^\s*(?&lt;CODE&gt;[A-Za-z]+[0-9]+):\s*(?&lt;MESSAGE&gt;.*)$</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="message">
    ///  [out] The text following the code with leading whitespace removed, or <see langword="null"/>
    ///  if no code was found. Empty if the code is followed only by whitespace.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if a code was stripped; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Use this method when only the message is needed; it avoids allocating the code.
    /// </remarks>
    public static bool TryStripAnyCode(string text, [NotNullWhen(true)] out string? message)
    {
        Assumed.NotNull(text);

        var scanner = new Scanner(text);

        if (!scanner.TryConsumeAnyCode())
        {
            message = null;
            return false;
        }

        scanner.Consume(':');
        scanner.SkipWhiteSpace();
        message = scanner.GetRemaining();

        return true;
    }

    private ref struct Scanner
    {
        private readonly string _text;
        private int _position;

        public Scanner(string text)
        {
            _text = text;

            // Leading whitespace is never part of a code, so skip it up front.
            SkipWhiteSpace();
        }

        public readonly string GetRemaining()
            => _position < _text.Length
                ? _text.Substring(_position)
                : string.Empty;

        public void SkipWhiteSpace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }

        public void Consume(char c)
        {
            Assumed.LessThan(_position, _text.Length);
            Assumed.Equal(_text[_position], c);

            _position++;
        }

        public bool TryConsumeMSBuildCode()
        {
            if (_text.Length < _position + 8 ||
                _text[_position] is not 'M' ||
                _text[_position + 1] is not 'S' ||
                _text[_position + 2] is not 'B' ||
                !char.IsAsciiDigit(_text[_position + 3]) ||
                !char.IsAsciiDigit(_text[_position + 4]) ||
                !char.IsAsciiDigit(_text[_position + 5]) ||
                !char.IsAsciiDigit(_text[_position + 6]) ||
                _text[_position + 7] is not ':')
            {
                return false;
            }

            _position += 7; // Stop on the ':'; the caller steps past it.

            return true;
        }

        public bool TryReadMSBuildCode([NotNullWhen(true)] out string? code)
        {
            int start = _position;

            if (!TryConsumeMSBuildCode())
            {
                code = null;
                return false;
            }

            code = _text.Substring(start, _position - start);
            Consume(':');

            return true;
        }

        public bool TryConsumeAnyCode()
        {
            // A code is one or more letters followed by one or more digits, terminated by a colon.
            int codeStart = _position;

            while (_position < _text.Length && char.IsAsciiLetter(_text[_position]))
            {
                _position++;
            }

            // There must be at least one letter.
            if (_position == codeStart)
            {
                return false;
            }

            int digitStart = _position;

            while (_position < _text.Length && char.IsAsciiDigit(_text[_position]))
            {
                _position++;
            }

            // There must be at least one digit.
            if (_position == digitStart)
            {
                return false;
            }

            // The code must be terminated by a colon. Stop on it; the caller steps past it.
            return _position < _text.Length && _text[_position] is ':';
        }

        public bool TryReadAnyCode([NotNullWhen(true)] out string? code)
        {
            int start = _position;

            if (!TryConsumeAnyCode())
            {
                code = null;
                return false;
            }

            code = _text.Substring(start, _position - start);
            Consume(':');

            return true;
        }
    }
}
