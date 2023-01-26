// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System;

namespace Microsoft.Build.BackEnd.Logging;

/// <summary>
/// Target console configuration.
/// If console output is redirected to other process console, like for example MSBuild Server does,
///    we need to know property of target/final console at which our output will be rendered.
/// If console is rendered at current process Console, we grab properties from Console and/or by WinAPI.
/// </summary>
internal static class ConsoleConfiguration
{
    /// <summary>
    /// Get or set current target console configuration provider.
    /// </summary>
    public static IConsoleConfiguration Provider
    {
        get { return Instance.s_instance; }
        set { Instance.s_instance = value; }
    }

    private static class Instance
    {
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static Instance()
        {
        }

        internal static IConsoleConfiguration s_instance = new InProcessConsoleConfiguration();
    }

    /// <summary>
    /// Buffer width of destination Console.
    /// Console loggers are supposed, on Windows OS, to be wrapping to avoid output trimming.
    /// -1 console buffer width can't be obtained.
    /// </summary>
    public static int BufferWidth => Provider.BufferWidth;

    /// <summary>
    /// True if console output accept ANSI colors codes.
    /// False if output is redirected to non screen type such as file or nul.
    /// </summary>
    public static bool AcceptAnsiColorCodes => Provider.AcceptAnsiColorCodes;

    /// <summary>
    /// Background color of client console, -1 if not detectable
    /// Some platforms do not allow getting current background color. There
    /// is not way to check, but not-supported exception is thrown. Assume
    /// black, but don't crash.
    /// </summary>
    public static ConsoleColor BackgroundColor => Provider.BackgroundColor;

    /// <summary>
    /// True if console output is screen. It is expected that non screen output is post-processed and often does not need wrapping and coloring.
    /// False if output is redirected to non screen type such as file or nul.
    /// </summary>
    public static bool OutputIsScreen => Provider.OutputIsScreen;
}
