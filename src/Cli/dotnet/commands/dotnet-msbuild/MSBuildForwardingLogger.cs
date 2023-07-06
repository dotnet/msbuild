// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public sealed class MSBuildForwardingLogger : IForwardingLogger
    {
        public LoggerVerbosity Verbosity { get; set; }

        public string Parameters { get; set; }

        public IEventRedirector BuildEventRedirector { get; set; }

        public int NodeId { get; set; }

        public void Initialize(IEventSource eventSource)
        {
            // Declare lack of dependency on having properties/items in ProjectStarted events
            // (since this logger doesn't ever care about those events it's irrelevant)
            if (eventSource is IEventSource4 eventSource4)
            {
                eventSource4.IncludeEvaluationPropertiesAndItems();
            }

            // Only forward telemetry events
            if (eventSource is IEventSource2 eventSource2)
            {
                eventSource2.TelemetryLogged += (sender, args) => BuildEventRedirector.ForwardEvent(args);
            }
        }

        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        public void Shutdown()
        {
        }
    }
}
