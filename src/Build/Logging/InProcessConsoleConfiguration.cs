// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System;
#if FEATURE_WINDOWSINTEROP
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.System.Console;
#endif

namespace Microsoft.Build.BackEnd.Logging;

/// <summary>
/// Console configuration of current process Console.
/// </summary>
internal class InProcessConsoleConfiguration : IConsoleConfiguration
{
    /// <summary>
    /// When set, we'll try reading background color.
    /// </summary>
    private static bool s_supportReadingBackgroundColor = true;

    public int BufferWidth => Console.BufferWidth;

    public bool AcceptAnsiColorCodes
    {
        get
        {
            bool acceptAnsiColorCodes = false;
#if FEATURE_WINDOWSINTEROP
            if (NativeMethodsShared.IsWindows && !Console.IsOutputRedirected)
            {
                try
                {
                    HANDLE stdOut = PInvoke.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
                    if (PInvoke.GetConsoleMode(stdOut, out CONSOLE_MODE consoleMode))
                    {
                        acceptAnsiColorCodes = consoleMode.HasFlag(CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Assert(false, $"MSBuild client warning: problem during enabling support for VT100: {ex}.");
                }
            }
            else
#endif
            {
                // On posix OSes we expect console always supports VT100 coloring unless it is redirected
                acceptAnsiColorCodes = !Console.IsOutputRedirected;
            }

            return acceptAnsiColorCodes;
        }
    }

    public ConsoleColor BackgroundColor
    {
        get
        {
            if (s_supportReadingBackgroundColor)
            {
                try
                {
                    return Console.BackgroundColor;
                }
                catch (PlatformNotSupportedException)
                {
                    s_supportReadingBackgroundColor = false;
                }
            }

            return ConsoleColor.Black;
        }
    }

    public bool OutputIsScreen
    {
        get
        {
            bool isScreen = false;

#if FEATURE_WINDOWSINTEROP
            if (NativeMethodsShared.IsWindows)
            {
                // Get the std out handle
                HANDLE stdHandle = PInvoke.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);

                if (stdHandle != HANDLE.INVALID_HANDLE_VALUE)
                {
                    isScreen = PInvoke.GetFileType(stdHandle) == FILE_TYPE.FILE_TYPE_CHAR;
                }
            }
            else
#endif
            {
                isScreen = !Console.IsOutputRedirected;
            }

            return isScreen;
        }
    }
}
