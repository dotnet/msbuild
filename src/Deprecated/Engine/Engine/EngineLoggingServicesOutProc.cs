// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class implements the out-of-proc (node process) logging services, provided by the engine
    /// for internal logging purposes.
    /// </summary>
    internal class EngineLoggingServicesOutProc : EngineLoggingServices
    {
        #region Constructors

        /// <summary>
        /// Private default constructor -- do not allow instantation w/o parameters.
        /// </summary>
        private EngineLoggingServicesOutProc()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class for the given engine event source.
        /// </summary>
        internal EngineLoggingServicesOutProc(Node parentNode, ManualResetEvent flushRequestEvent)
        {
            this.parentNode = parentNode;
#pragma warning disable 1717
            this.onlyLogCriticalEvents = onlyLogCriticalEvents;
#pragma warning restore 1717
            this.loggingQueueReadLock = new object();
            this.eventArray = new NodeLoggingEvent[eventArrayChunkSize];
            base.Initialize(flushRequestEvent);
        }
        #endregion

        #region Methods

        /// <summary>
        /// The out of proc logging service is concerned with flushing the events out to the node provider
        /// to be sent to the parent engine. Events which are not marked with a logger id end up being wrapped 
        /// in a NodeLoggingEvent which was a default loggerId of 0. All events posted as BuildEventArgs fall
        /// into this category. Events with a loggerId need be posted as NodeLoggerEventWithLoggerId objects.
        /// This function is thread safe and is called both from the engine thread and communication threads to 
        /// ensure that the events are delivered in coherent order.
        /// </summary>
        override internal bool ProcessPostedLoggingEvents()
        {
            lock (loggingQueueReadLock)
            {
                bool processedEvents = false;

                lastFlushTime = DateTime.Now.Ticks;

                // We use a local array to hold the items that will be dequeueed. We can't reuse the queues because
                // we give up the control of the data structure once we pass it to the node provider and we also need to maintain
                // order between buildEventArgs and nodeLoggingEvent.
                current = 0;

                // Process all the events in that have been already posted
                BuildEventArgs buildEventArgs = null;

                // Grab all the event args out of the queue
                while ((buildEventArgs = loggingQueueOfBuildEvents.Dequeue()) != null)
                {
                    AddToCurrentArray(new NodeLoggingEvent(buildEventArgs));
                    processedEvents = true;
                }

                // Grab all the forwarded events 
                NodeLoggingEvent nodeLoggingEvent = null;
                while ((nodeLoggingEvent = loggingQueueOfNodeEvents.Dequeue()) != null)
                {
                    AddToCurrentArray(nodeLoggingEvent);
                    processedEvents = true;
                }

                // If there are event - post them to the parent
                if (current != 0)
                {
                    NodeLoggingEvent [] trimmedEventArray = new NodeLoggingEvent[current];
                    Array.Copy(eventArray, trimmedEventArray, current);
                    parentNode.PostLoggingMessagesToHost(trimmedEventArray);
                    current = 0;
                }
                requestedQueueFlush = false;

                return processedEvents;
            }
        }

        /// <summary>
        /// Adds an event to an array. If the array is full it is posted to the parent and a new array is created
        /// </summary>
        /// <param name="e"></param>
        private void AddToCurrentArray(NodeLoggingEvent e)
        {
            eventArray[current] = e;
            current++;

            if (current == eventArrayChunkSize)
            {
                parentNode.PostLoggingMessagesToHost(eventArray);
                eventArray = new NodeLoggingEvent[eventArrayChunkSize];
                current = 0;
            }
        }

        /// <summary>
        /// Shutdown the logging service as appropriate
        /// </summary>
        override internal void Shutdown()
        {
            // Do nothing
        }

        /// <summary>
        /// Reports an exception thrown while sending logging event to the node
        /// </summary>
        internal void ReportLoggingFailure(Exception e)
        {
            parentNode.ReportUnhandledError(e);
        }

        #endregion

        #region Data
        /// <summary>
        /// This mutex protects the queue from multiple readers, which may happen in the
        /// out of proc implementation
        /// </summary>
        protected object loggingQueueReadLock;

        /// <summary>
        /// If this engine is running in child mode the parent node is used to post logging messages
        /// </summary>
        private Node parentNode;

        /// <summary>
        /// Current count of items in the array of events (access should be protected with loggingQueueReadLock)
        /// </summary>
        private int current;

        /// <summary>
        /// Array of events to be send to the parent (access should be protected with loggingQueueReadLock)
        /// </summary>
        private NodeLoggingEvent[] eventArray;

        /// <summary>
        /// The number of events in one array posted to the parent.
        /// </summary>
        const int eventArrayChunkSize = 100;
        #endregion
    }
}
