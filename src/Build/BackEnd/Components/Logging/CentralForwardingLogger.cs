// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Logger that forwards events to loggers registered with the LoggingServices
    /// </summary>
    internal class CentralForwardingLogger : IForwardingLogger
    {
        #region Properties

        /// <summary>
        /// An IEventRedirector which will redirect any events forwarded from the logger. The eventRedirector determines where the events will 
        /// be redirected.
        /// </summary>
        public IEventRedirector BuildEventRedirector
        {
            get;
            set;
        }

        /// <summary>
        /// The nodeId of the node on which the logger is currently running.
        /// </summary>
        public int NodeId
        {
            get;
            set;
        }

        /// <summary>
        /// Verbosity of the logger, in the central forwarding logger this is currently ignored
        /// </summary>
        public LoggerVerbosity Verbosity
        {
            get;
            set;
        }

        /// <summary>
        /// Logging Parameters
        /// </summary>
        public string Parameters
        {
            get;
            set;
        }

        #endregion

        #region Members

        #region Public

        /// <summary>
        /// Initialize the logger with an eventSource and a node count. 
        /// The logger will register and listen to anyEvents on the eventSource.
        /// The node count is for informational purposes. The logger may want to take different
        /// actions depending on how many nodes there are in the system.
        /// </summary>
        /// <param name="eventSource">Event source which the logger will register with to receive events</param>
        /// <param name="nodeCount">Number of nodes the system was started with</param>
        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        /// <summary>
        /// Initialize the logger. The logger will register with AnyEventRaised on the eventSource
        /// </summary>
        /// <param name="eventSource">eventSource which the logger will register on to listen for events</param>
        /// <exception cref="InternalErrorException">EventSource is null</exception>
        public void Initialize(IEventSource eventSource)
        {
            ErrorUtilities.VerifyThrow(eventSource != null, "eventSource is null");
            eventSource.AnyEventRaised += EventSource_AnyEventRaised;

            IEventSource2 eventSource2 = eventSource as IEventSource2;
            if (eventSource2 != null)
            {
                // Telemetry events aren't part of "all" so they need to be forwarded separately
                eventSource2.TelemetryLogged += EventSource_AnyEventRaised;
            }
        }

        /// <summary>
        /// Shuts down the logger. This will null out the buildEventRedirector
        /// </summary>
        public void Shutdown()
        {
            BuildEventRedirector = null;
        }

        #endregion

        #region Private

        /// <summary>
        /// Forwards any event raised to the BuildEventRedirector, this redirector will send the event on a path which will 
        /// take it to a logger.
        /// </summary>
        /// <param name="sender">Who sent the message, this is not used</param>
        /// <param name="buildEvent">BuildEvent to forward</param>
        private void EventSource_AnyEventRaised(object sender, BuildEventArgs buildEvent)
        {
            // If no central logger was registered with the system
            // there will not be a build event redirector as there is 
            // nowhere to forward the events to.
            BuildEventRedirector?.ForwardEvent(buildEvent);
        }

        #endregion

        #endregion
    }
}
