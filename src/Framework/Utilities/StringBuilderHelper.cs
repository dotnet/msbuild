// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NET
using System.Runtime.CompilerServices;
#endif
using System.Text;
using System.Threading;

#pragma warning disable IDE0038 // Use pattern matching

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Helper struct to use with custom interpolated string handlers.
/// </summary>
internal ref struct StringBuilderHelper(int capacity)
{
    private StringBuilder? _builder = StringBuilderCache.Acquire(capacity);

    public readonly void AppendLiteral(string value)
        => _builder?.Append(value);

    public readonly void AppendFormatted<T>(T value)
    {
        if (value is null || _builder is null)
        {
            return;
        }

        // PERF: Intentionally not using pattern matching here to avoid boxing.
        // - On .NET, we prefer ISpanFormattable and perform that check below.
        // - On .NET Framework, we make a constrained call to IFormattable to avoid boxing,
        //   which is not possible with pattern matching.
        if (value is IFormattable)
        {
#if NET
            if (value is ISpanFormattable)
            {
                // This calls the Append overload that takes a StringBuilder.AppendInterpolatedStringHandler,
                // which will avoid allocations for value types.
                _builder.Append($"{value}");
                return;
            }
#endif

            // Make a constrained call through IFormattable to avoid boxing value types.
            _builder.Append(((IFormattable)value).ToString(format: null, formatProvider: null));
        }
        else
        {
            _builder.Append(value.ToString());
        }
    }

    public readonly void AppendFormatted<T>(T value, string? format)
        where T : IFormattable
    {
        if (value is null || _builder is null)
        {
            return;
        }

#if NET
        if (value is ISpanFormattable)
        {
            // Use System.Runtime.CompilerServices.DefaultInterpolatedStringHandler
            // to do the work of appending 'value' and formatting with 'format'.
            // We initialize with a stack-allocated buffer of 64 chars, which would
            // only be a significant concern if this code path were running on .NET Framework.
            var handler = new DefaultInterpolatedStringHandler(
                literalLength: 0,
                formattedCount: 1,
                provider: null,
                initialBuffer: stackalloc char[64]);

            handler.AppendFormatted(value, alignment: 0, format);

            _builder.Append(handler.Text);

            handler.Clear();
            return;
        }
#endif

        _builder.Append(value.ToString(format, formatProvider: null));
    }

    public string GetFormattedText()
        => Interlocked.Exchange(ref _builder, null) is { } builder
            ? StringBuilderCache.GetStringAndRelease(builder)
            : string.Empty;
}
