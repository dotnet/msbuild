// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Interpolated string handler that handles string formatting unconditionally.
/// </summary>
[InterpolatedStringHandler]
internal ref struct UnconditionalInterpolatedStringHandler
{
    private StringBuilderHelper _builder;

    public UnconditionalInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        _builder = new(literalLength);
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
