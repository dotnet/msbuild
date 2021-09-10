// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Linq;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public class RazorEventListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-DotNet-SDK-Razor-SourceGenerator")
            {
                EnableEvents(source, EventLevel.Informational);
            }
        }

        public ConcurrentQueue<RazorEvent> Events { get; } = new();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var @event = new RazorEvent
            {
                EventId = eventData.EventId,
                EventName = eventData.EventName,
                Payload = eventData.Payload.ToArray(),
            };

            Events.Enqueue(@event);
        }

        public sealed class RazorEvent
        {
            public int EventId { get; init; }

            public string EventName { get; init; }

            public object[] Payload { get; init; }
        }
    }
}
