// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Logging.SimpleErrorLogger
{
    public class SimpleErrorLogger : INodeLogger
    {
        public bool hasLoggedErrors = false;
        public SimpleErrorLogger()
        {
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
            Console.Error.Write("\x1b[31;1m");
            Console.Error.Write(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true));
            Console.Error.WriteLine("\x1b[m");
        }

        private void HandleWarningEvent(object sender, BuildWarningEventArgs e)
        {
            Console.Error.Write("\x1b[33;1m");
            Console.Error.Write(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true));
            Console.Error.WriteLine("\x1b[m");
        }

        public void Initialize(IEventSource eventSource)
        {
            Initialize(eventSource, 1);
        }

        public void Shutdown()
        {
        }
    }
}
