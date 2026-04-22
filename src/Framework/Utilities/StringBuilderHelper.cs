// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Helper struct to use with custom interpolated string handlers.
/// </summary>
/// <remarks>
///  Acquires a <see cref="StringBuilder"/> from <see cref="StringBuilderCache"/> on construction.
///  The builder is returned to the cache when <see cref="GetFormattedText"/> is called, which
///  produces the final string and releases the underlying <see cref="StringBuilder"/>.
/// </remarks>
internal ref struct StringBuilderHelper(int capacity)
{
    private StringBuilder? _builder = StringBuilderCache.Acquire(capacity);

    public readonly void AppendLiteral(string value)
        => _builder?.Append(value);

    public readonly void AppendFormatted<T>(T value)
        => _builder?.Append(value?.ToString());

    public readonly void AppendFormatted<TValue>(TValue value, string format)
        where TValue : IFormattable
        => _builder?.Append(value?.ToString(format, formatProvider: null));

    /// <summary>
    ///  Returns the formatted string and releases the underlying <see cref="StringBuilder"/>
    ///  back to the <see cref="StringBuilderCache"/>. Subsequent calls return <see cref="string.Empty"/>.
    /// </summary>
    public string GetFormattedText()
    {
        StringBuilder? builder = _builder;
        _builder = null;

        return builder is not null
                ? StringBuilderCache.GetStringAndRelease(builder)
                : string.Empty;
    }
}
