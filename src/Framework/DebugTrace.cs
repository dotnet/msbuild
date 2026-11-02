// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework;

/// <summary>
///  Writes categorized diagnostic messages to <see cref="Trace"/> when the
///  <c>MSBUILDENABLEDEBUGTRACING</c> environment variable is set. When tracing is disabled,
///  calls are effectively free: string formatting is deferred via
///  <see cref="WriteLineInterpolatedStringHandler"/> and never runs.
/// </summary>
internal static class DebugTrace
{
    private static readonly bool s_enabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDENABLEDEBUGTRACING"));

    /// <summary>
    ///  Writes <paramref name="message"/> to <see cref="Trace"/> under the given
    ///  <paramref name="category"/>, if tracing is enabled.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="category">
    ///  The category to associate with the message. Defaults to the name of the calling member.
    /// </param>
    public static void WriteLine(string message, [CallerMemberName] string category = "")
    {
        if (s_enabled)
        {
            Trace.WriteLine(message, category);
        }
    }

    /// <summary>
    ///  Writes an interpolated message to <see cref="Trace"/> under the given
    ///  <paramref name="category"/>, if tracing is enabled. The interpolated string is only
    ///  formatted when tracing is enabled.
    /// </summary>
    /// <param name="handler">The interpolated string to write.</param>
    /// <param name="category">
    ///  The category to associate with the message. Defaults to the name of the calling member.
    /// </param>
    public static void WriteLine(ref WriteLineInterpolatedStringHandler handler, [CallerMemberName] string category = "")
    {
        if (s_enabled)
        {
            Trace.WriteLine(handler.GetFormattedText(), category);
        }
    }

    /// <summary>
    ///  Interpolated string handler used by <see cref="WriteLine(ref WriteLineInterpolatedStringHandler, string)"/>
    ///  to defer string formatting unless tracing is enabled.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct WriteLineInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public WriteLineInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            isEnabled = s_enabled;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }
}
