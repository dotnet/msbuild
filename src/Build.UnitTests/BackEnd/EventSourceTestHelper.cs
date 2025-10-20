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
    /// <summary>
    /// This class provides the ability to fetch events emitted from the "Microsoft-Build" EventSource.
    /// The instance listens and saves emitted events in-memory.
    /// To fetch the events, use the <see cref="GetEvents"/> method.
    /// Note that the current implementation of this class does not have protection against concurrent usage in tests.
    /// If used in tests, ensure to rely on unique variables, names, or IDs. For example usage: <see cref="SdkResolverService_Tests.AssertSdkResolutionMessagesAreLoggedInEventSource"/>.
    /// 
    /// Reference: <see href="https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventlistener"/>
    /// <example>
    /// <code>
    /// // ...
    /// using var eventSourceTestListener = new EventSourceTestHelper();
    /// // ...
    /// var events = eventSourceTestListener.GetEvents();
    /// // verification
    /// </code>
    /// </example>
    /// </summary>
    internal sealed class EventSourceTestHelper : EventListener
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
            if (_eventSources  != null)
            {
                DisableEvents(_eventSources);
            }
            
            base.Dispose();
        }

        /// <summary>
        /// Returns the events that were emitted till invocation of this method.
        /// The events are cleared from the in-memory store and are populated again in <see cref="OnEventWritten"/>. 
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
