// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    internal class EventSourceTestHelper : EventListener
    {
        private readonly string eventSourceName = "Microsoft-Build";
        private readonly List<EventWrittenEventArgs> emittedEvents;
        private object _eventListLock = new object();
        private EventSource? _eventSources = null;

        public EventSourceTestHelper()
        {
            emittedEvents = new List<EventWrittenEventArgs>();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == eventSourceName)
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
                _eventSources = eventSource;
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            lock (_eventListLock)
            {
                emittedEvents.Add(eventData);
            }
        }

        public override void Dispose() {
            DisableEvents(_eventSources);
            base.Dispose();
        }

        /// <summary>
        /// Returns the events that were emitted till invocation of this method.
        /// The events are cleared from the in-memory store and are populated again. 
        /// </summary>
        /// <returns>List of the events that were emitted for eventSource</returns>
        internal List<EventWrittenEventArgs> GetEvents()
        {
            var resultList = new List<EventWrittenEventArgs>();
            lock (_eventListLock)
            {
                resultList = new List<EventWrittenEventArgs>(emittedEvents);
                emittedEvents.Clear();
            }
            
            return resultList;
        }
    }
}
