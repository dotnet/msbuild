// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.LiveLogger;

internal interface ITerminal : IDisposable
{
    void Write(string text);

    void WriteLine(string text);

    void WriteLine(ReadOnlySpan<char> text);

    void WriteLineFitToWidth(ReadOnlySpan<char> input);
}
