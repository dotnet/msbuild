// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET

using System;
using System.Text;

namespace Microsoft.Build;

internal static class StringBuilderExtensions
{
    /// <summary>
    ///  Adds the missing span-taking overload to <see cref="StringBuilder"/>.
    /// </summary>
    public static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
    {
        return sb.Append(value.ToString());
    }
}

#endif
