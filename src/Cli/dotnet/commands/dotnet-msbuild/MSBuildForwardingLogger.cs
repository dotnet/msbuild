// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
