// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This is a small redirector that decorates the events that are forwarded by
    /// a particular node logger with the id of the central logger and passes them to the engine
    /// logging service
    /// </summary>
    internal class EventRedirector : IEventRedirector
    {
        #region Constructors
        /// <summary>
        /// Initalize this class with a central logger id identifying the central logger to which
        /// these events should be forwarded and a logging service that will do the forwarding
        /// </summary>
        /// <param name="loggerId">central logger id</param>
        /// <param name="loggingService">engine logging service</param>
        internal EventRedirector(int loggerId, EngineLoggingServices loggingService)
        {
            this.loggerId = loggerId;
            this.loggingService = loggingService;
        }
        #endregion

        #region Methods implementing ICentrolLogger

        /// <summary>
        /// This method is called by the node loggers to forward the events to cenral logger
        /// </summary>
        void IEventRedirector.ForwardEvent(BuildEventArgs buildEvent)
        {
            // Don't allow forwarding loggers to forward build started
            ErrorUtilities.VerifyThrowInvalidOperation(!(buildEvent is BuildStartedEventArgs), "DontForwardBuildStarted");
            // Don't allow forwarding loggers to forward build finished
            ErrorUtilities.VerifyThrowInvalidOperation(!(buildEvent is BuildFinishedEventArgs), "DontForwardBuildFinished");
            // Mark the event with the logger id metadata and post it to the queue
            NodeLoggingEventWithLoggerId loggingEvent = new NodeLoggingEventWithLoggerId(buildEvent, loggerId);
            loggingService.PostLoggingEvent(loggingEvent);
        }

        #endregion

        #region Data
        // The Id of the central logger to which this event should be forwarded
        private int loggerId;
        // The engine logging service that will forward the event to the right central logger
        private EngineLoggingServices loggingService;
        #endregion
    }
}
