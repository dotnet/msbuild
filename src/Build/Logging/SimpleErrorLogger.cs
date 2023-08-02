// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging.SimpleErrorLogger
{
    /// <summary>
    /// This logger ignores all message-level output, writing errors and warnings to
    /// standard error, colored red and yellow respectively.
    ///
    /// It is currently used only when the user requests information about specific
    /// properties, items, or target results. In that case, we write the desired output
    /// to standard out, but we do not want it polluted with any other kinds of information.
    /// Users still might want diagnostic information if something goes wrong, so still
    /// output that as necessary.
    /// </summary>
    public class SimpleErrorLogger : INodeLogger
    {
        public bool hasLoggedErrors = false;
        private bool acceptAnsiColorCodes;
        private uint? originalConsoleMode;
        private const int STD_ERROR_HANDLE = -12;
        public SimpleErrorLogger()
        {
            (acceptAnsiColorCodes, _, originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes(STD_ERROR_HANDLE);
        }

        public LoggerVerbosity Verbosity
        {
            get => LoggerVerbosity.Minimal;
            set { }
        }

        public string Parameters
        {
            get => string.Empty;
            set { }
        }

        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            eventSource.ErrorRaised += HandleErrorEvent;
            eventSource.WarningRaised += HandleWarningEvent;
        }

        private void HandleErrorEvent(object sender, BuildErrorEventArgs e)
        {
            hasLoggedErrors = true;
            if (acceptAnsiColorCodes)
            {
                Console.Error.Write("\x1b[31;1m");
                Console.Error.Write(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true));
                Console.Error.WriteLine("\x1b[m");
            }
            else
            {
                Console.Error.Write(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true));
            }
        }

        private void HandleWarningEvent(object sender, BuildWarningEventArgs e)
        {
            if (acceptAnsiColorCodes)
            {
                Console.Error.Write("\x1b[33;1m");
                Console.Error.Write(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true));
                Console.Error.WriteLine("\x1b[m");
            }
            else
            {
                Console.Error.Write(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true));
            }
        }

        public void Initialize(IEventSource eventSource)
        {
            Initialize(eventSource, 1);
        }

        public void Shutdown()
        {
            NativeMethods.RestoreConsoleMode(originalConsoleMode, STD_ERROR_HANDLE);
        }
    }
}
