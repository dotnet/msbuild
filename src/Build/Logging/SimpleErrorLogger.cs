// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Logging.SimpleErrorLogger
{
    public class SimpleErrorLogger : INodeLogger
    {
        public StringBuilder errorList;

        public SimpleErrorLogger()
        {
            errorList = new StringBuilder();
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
        }

        private void HandleErrorEvent(object sender, BuildErrorEventArgs e)
        {
            errorList.AppendLine(EventArgsFormatting.FormatEventMessage(e, showProjectFile: true));
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
