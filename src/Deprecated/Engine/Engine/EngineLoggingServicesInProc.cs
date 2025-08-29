// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class implements the in-proc (engine process) logging services, provided by the engine for
    /// internal logging purposes.
    /// </summary>
    internal class EngineLoggingServicesInProc : EngineLoggingServices
    {
        #region Constructors

        /// <summary>
        /// Private default constructor -- do not allow instantation w/o parameters.
        /// </summary>
        private EngineLoggingServicesInProc()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class for the given engine event source.
        /// </summary>
        /// <param name="eventSource"></param>
        /// <param name="onlyLogCriticalEvents"></param>
        internal EngineLoggingServicesInProc(EventSource eventSource, bool onlyLogCriticalEvents, ManualResetEvent flushRequestEvent)
        {
            this.onlyLogCriticalEvents = onlyLogCriticalEvents;
            this.engineEventSource = eventSource;

            // Allocate the table mapping logger ids to event sources
            this.eventSources = new Dictionary<int, EventSource>();
            RegisterEventSource(CENTRAL_ENGINE_EVENTSOURCE, eventSource);

            base.Initialize(flushRequestEvent);
        }

        #endregion

        #region Methods

        /// <summary>
        /// This function logs out all the messages currently posted to the queue. The active queue is swapped
        /// with the secondary queue to enable posting of messages while this function is running
        /// </summary>
        internal override bool ProcessPostedLoggingEvents()
        {
            bool processedEvents = false;
            lastFlushTime = DateTime.Now.Ticks;

            // Process all the events posted with a logger Id
            NodeLoggingEvent nodeLoggingEvent;

            // We may get a single event for multiple messages
            while ((nodeLoggingEvent = loggingQueueOfNodeEvents.Dequeue()) != null)
            {
                int loggerId = nodeLoggingEvent.LoggerId;
                if (loggerId != ALL_PRIVATE_EVENTSOURCES)
                {
                    ErrorUtilities.VerifyThrow(eventSources.ContainsKey(loggerId), "Logger Id should be registered");
                    ErrorUtilities.VerifyThrow(loggerId != LOCAL_FORWARDING_EVENTSOURCE, "Should not use FWD loggers");
                    eventSources[loggerId].RaiseStronglyTypedEvent(nodeLoggingEvent.BuildEvent);
                }
                else
                {
                    // Post event to all central loggers
                    foreach (KeyValuePair<int, EventSource> eventSource in eventSources)
                    {
                        if (eventSource.Key >= FIRST_AVAILABLE_LOGGERID)
                        {
                            eventSource.Value.RaiseStronglyTypedEvent(nodeLoggingEvent.BuildEvent);
                        }
                    }
                }
                processedEvents = true;
            }

            // Process all the events in that have been already posted
            BuildEventArgs buildEventArgs;

            // We may get a single event for multiple messages
            while ((buildEventArgs = loggingQueueOfBuildEvents.Dequeue()) != null)
            {
                ProcessBuildEvent(buildEventArgs);
                processedEvents = true;
            }

            requestedQueueFlush = false;
            return processedEvents;
        }

        /// <summary>
        /// This method process a single BuildEvent argument, it will raise the event to registered loggers and 
        /// check to see if the there are forwarding loggers who need to see the event also, if so the message will
        /// be posted to another logger
        /// </summary>
        /// <param name="buildEventArgs"></param>
        internal override void ProcessBuildEvent(BuildEventArgs buildEventArgs)
        {
            engineEventSource.RaiseStronglyTypedEvent(buildEventArgs);

            // Check if there are local forwarding loggers that should see this event
            if (eventSources.ContainsKey(LOCAL_FORWARDING_EVENTSOURCE))
            {
                eventSources[LOCAL_FORWARDING_EVENTSOURCE].RaiseStronglyTypedEvent(buildEventArgs);
            }

            // Check if it necessary to forward the event to another logging service
            forwardingService?.PostLoggingEvent(buildEventArgs);
        }

        internal void RegisterEventSource(int loggerId, EventSource eventSource)
        {
            ErrorUtilities.VerifyThrow(!eventSources.ContainsKey(loggerId), "Should not see duplicate logger ids");
            eventSources[loggerId] = eventSource;
        }

        internal void UnregisterEventSource(int loggerId)
        {
            if (eventSources.ContainsKey(loggerId))
            {
                eventSources[loggerId].UnregisterAllLoggers();
                eventSources.Remove(loggerId);
            }
        }

        /// <summary>
        /// Shutdown the logging service as appropriate
        /// </summary>
        internal override void Shutdown()
        {
            foreach (EventSource eventSource in eventSources.Values)
            {
                eventSource.UnregisterAllLoggers();
            }
        }

        #endregion

        #region Data

        // A table mapping logger ids to event sources
        private Dictionary<int, EventSource> eventSources;
        // cached pointer to the engines main event source (also available in eventSources[0])
        private readonly EventSource engineEventSource;

        internal const int CENTRAL_ENGINE_EVENTSOURCE = 0;
        internal const int LOCAL_FORWARDING_EVENTSOURCE = 1;
        internal const int ALL_PRIVATE_EVENTSOURCES = 2;
        internal const int FIRST_AVAILABLE_LOGGERID = 3;

        #endregion
    }
}
