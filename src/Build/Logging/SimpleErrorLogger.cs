// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Logging;
using Microsoft.Build.Logging;
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
    public sealed class SimpleErrorLogger : INodeLogger
    {
        private readonly bool acceptAnsiColorCodes;
        private readonly uint? originalConsoleMode;
        public SimpleErrorLogger()
        {
            (acceptAnsiColorCodes, _, originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes(NativeMethods.StreamHandleType.StdErr);
        }

        public bool HasLoggedErrors { get; private set; } = false;

        public LoggerVerbosity Verbosity
        {
            get => LoggerVerbosity.Minimal;
            set { }
        }

        public string? Parameters
        {
            get => string.Empty;
            set { }
        }

        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            eventSource.ErrorRaised += HandleErrorEvent;
            eventSource.WarningRaised += HandleWarningEvent;

            // This needs to happen so binary loggers can get evaluation properties and items
            if (eventSource is IEventSource4 eventSource4)
            {
                eventSource4.IncludeEvaluationPropertiesAndItems();
            }
        }

        private void HandleErrorEvent(object sender, BuildErrorEventArgs e)
        {
            HasLoggedErrors = true;
            LogWithColor(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true),
                TerminalColor.Red);
        }

        private void HandleWarningEvent(object sender, BuildWarningEventArgs e)
        {
            LogWithColor(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true),
                TerminalColor.Yellow);
        }

        private void LogWithColor(string message, TerminalColor color)
        {
            if (acceptAnsiColorCodes)
            {
                Console.Error.WriteLine(AnsiCodes.Colorize(message, color));
            }
            else
            {
                Console.Error.WriteLine(message);
            }
        }

        public void Initialize(IEventSource eventSource)
        {
            Initialize(eventSource, 1);
        }

        public void Shutdown()
        {
            NativeMethods.RestoreConsoleMode(originalConsoleMode, NativeMethods.StreamHandleType.StdErr);
        }
    }
}
