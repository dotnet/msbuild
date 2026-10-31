// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET

using System;
using System.IO;

namespace Microsoft.Build;

internal static class TextWriterExtensions
{
    /// <summary>
    ///  Adds the missing span-taking overload to <see cref="TextWriter"/>.
    /// </summary>
    public static void Write(this TextWriter writer, ReadOnlySpan<char> buffer)
    {
        writer.Write(buffer.ToString());
    }

    /// <summary>
    ///  Adds the missing span-taking overload to <see cref="TextWriter"/>.
    /// </summary>
    public static void WriteLine(this TextWriter writer, ReadOnlySpan<char> buffer)
    {
        writer.WriteLine(buffer.ToString());
    }
}

#endif
