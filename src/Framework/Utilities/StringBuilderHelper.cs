// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Threading;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Helper struct to use with custom interpolated string handlers.
/// </summary>
internal ref struct StringBuilderHelper
{
    private StringBuilder? _builder;

    public StringBuilderHelper(int capacity, bool condition)
    {
        if (condition)
        {
            _builder = StringBuilderCache.Acquire(capacity);
        }
    }

    public readonly void AppendLiteral(string value)
        => _builder!.Append(value);

    public readonly void AppendFormatted<T>(T value)
        => _builder!.Append(value?.ToString());

    public readonly void AppendFormatted<TValue>(TValue value, string format)
        where TValue : IFormattable
        => _builder!.Append(value?.ToString(format, formatProvider: null));

    public string GetFormattedText()
    {
        var builder = Interlocked.Exchange(ref _builder, null);

        if (builder is not null)
        {
            return StringBuilderCache.GetStringAndRelease(builder);
        }

        // GetFormattedText() should never be called if the condition passed in was false.
        FrameworkErrorUtilities.ThrowInternalError("Unreachable code path.");
        return null;
    }
}
