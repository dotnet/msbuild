// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Will redirect events from forwarding loggers to the a IBuildEventSink, many redirectors may redirect to one sink.
    /// </summary>
    internal class EventRedirectorToSink : IEventRedirector
    {
        #region Data
        /// <summary>
        /// The Id of the central logger to which this event should be forwarded
        /// </summary>
        private int _centralLoggerId;

        /// <summary>
        /// The sink which will consume the messages
        /// </summary>
        private IBuildEventSink _sink;
        #endregion

        #region Constructors
        /// <summary>
        /// Initalize this class with a central logger id identifying the central logger to which
        /// these events should consumed by. The redirector will send the messages to the registered sink to 
        /// be consumed
        /// </summary>
        /// <param name="loggerId">Id which will be attached to the build event arguments to indicate which logger the events came from</param>
        /// <param name="eventSink">sink which will initially consume the events</param>
        /// <exception cref="InternalErrorException">Eventsink is null</exception>
        /// <exception cref="InternalErrorException">LoggerId is less than 0</exception>
        internal EventRedirectorToSink(int loggerId, IBuildEventSink eventSink)
        {
            ErrorUtilities.VerifyThrow(eventSink != null, "eventSink is null");
            ErrorUtilities.VerifyThrow(loggerId >= 0, "loggerId should be greater or equal to 0");
            _centralLoggerId = loggerId;
            _sink = eventSink;
        }
        #endregion

        #region IEventRedirector Methods

        /// <summary>
        /// This method is called by the node loggers to forward the events to cenral logger
        /// </summary>
        /// <param name="buildEvent">Build event to forward</param>
        /// <exception cref="InternalErrorException">BuildEvent is null</exception>
        void IEventRedirector.ForwardEvent(BuildEventArgs buildEvent)
        {
            ErrorUtilities.VerifyThrow(buildEvent != null, "buildEvent is null");
            _sink.Consume(buildEvent, _centralLoggerId);
        }
        #endregion
    }
}
