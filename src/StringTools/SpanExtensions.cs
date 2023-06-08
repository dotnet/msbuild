// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK || NETSTANDARD
using System;

namespace Microsoft.NET.StringTools;

public static class SpanExtensions
{
    public static bool StartsWith(this ReadOnlySpan<char> span, string s, StringComparison comparisonType)
    {
        return span.StartsWith(s.AsSpan(), comparisonType);
    }

    public static bool Equals(this ReadOnlySpan<char> span, string other, StringComparison comparisonType)
    {
        return span.Equals(other.AsSpan(), comparisonType);
    }
}
#endif
