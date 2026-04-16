// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Threading;

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
        => _builder?.Append(value?.ToString());

    public readonly void AppendFormatted<TValue>(TValue value, string format)
        where TValue : IFormattable
        => _builder?.Append(value?.ToString(format, formatProvider: null));

    public string GetFormattedText()
        => Interlocked.Exchange(ref _builder, null) is { } builder
            ? StringBuilderCache.GetStringAndRelease(builder)
            : string.Empty;
}
