// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System;

namespace Microsoft.Build.BackEnd.Logging;

/// <summary>
/// Console configuration needed for proper Console logging.
/// </summary>
internal interface IConsoleConfiguration
{
    /// <summary>
    /// Buffer width of destination Console.
    /// Console loggers are supposed, on Windows OS, to be wrapping to avoid output trimming.
    /// -1 console buffer width can't be obtained.
    /// </summary>
    int BufferWidth { get; }

    /// <summary>
    /// True if console output accept ANSI colors codes.
    /// False if output is redirected to non screen type such as file or nul.
    /// </summary>
    bool AcceptAnsiColorCodes { get; }

    /// <summary>
    /// True if console output is screen. It is expected that non screen output is post-processed and often does not need wrapping and coloring.
    /// False if output is redirected to non screen type such as file or nul.
    /// </summary>
    bool OutputIsScreen { get; }

    /// <summary>
    /// Background color of client console, -1 if not detectable
    /// Some platforms do not allow getting current background color. There
    /// is not way to check, but not-supported exception is thrown. Assume
    /// black, but don't crash.
    /// </summary>
    ConsoleColor BackgroundColor { get; }
}
