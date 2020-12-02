// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// Delegate to a method which will transport the packet accross the wire.
    /// </summary>
    /// <param name="packetToSend">A node packet to send accross the wire</param>
    internal delegate void SendDataDelegate(INodePacket packetToSend);

    /// <summary>
    /// This class will consume the BuildEventArgs forwarded by the EventRedirectorToSink class.
    /// The sink will then create a packet and then pass this along to the transport layer to be
    /// sent back to the build manager.
    /// </summary>
    internal class BuildEventArgTransportSink : IBuildEventSink
    {
        #region Data
        /// <summary>
        /// Delegate to a method which accepts a INodePacket and send the packet to where it needs to go
        /// </summary>
        private SendDataDelegate _sendDataDelegate;
        #endregion

        #region Constructor

        /// <summary>
        /// Create the sink which will consume a buildEventArg
        /// Create a INodePacket and send it to the transport component
        /// </summary>
        /// <param name="sendData">A delegate which takes an INodePacket and sends the packet to where it needs to go</param>
        /// <exception cref="InternalErrorException">Send data delegate is null</exception>
        internal BuildEventArgTransportSink(SendDataDelegate sendData)
        {
            ErrorUtilities.VerifyThrow(sendData != null, "sendData delegate is null");
            _sendDataDelegate = sendData;
        }

        #endregion

        #region Properties
        /// <summary>
        /// Provide a friendly name for the sink to make it easier to differentiate during 
        /// debugging and display
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Has the sink logged the BuildStartedEvent. This is important to know because we only want to log the build started event once
        /// </summary>
        public bool HaveLoggedBuildStartedEvent
        {
            get;
            set;
        }

        /// <summary>
        /// Has the sink logged the BuildFinishedEvent. This is important to know because we only want to log the build finished event once
        /// </summary>
        public bool HaveLoggedBuildFinishedEvent
        {
            get;
            set;
        }

        /// <summary>
        /// This property is ignored by this event sink and relies on the receiver to treat warnings as errors.
        /// </summary>
        public ISet<string> WarningsAsErrors
        {
            get;
            set;
        }

        /// <summary>
        /// This property is ignored by this event sink and relies on the receiver to treat warnings as errors.
        /// </summary>
        public IDictionary<int, ISet<string>> WarningsAsErrorsByProject
        {
            get;
            set;
        }

        /// <summary>
        /// This property is ignored by this event sink and relies on the receiver to treat warnings as low importance messages.
        /// </summary>
        public ISet<string> WarningsAsMessages
        {
            get;
            set;
        }

        /// <summary>
        /// This property is ignored by this event sink and relies on the receiver to treat warnings as low importance messages.
        /// </summary>
        public IDictionary<int, ISet<string>> WarningsAsMessagesByProject
        {
            get;
            set;
        }


        /// <summary>
        /// This property is ignored by this event sink and relies on the receiver to keep track of whether or not any errors have been logged.
        /// </summary>
        public ISet<int> BuildSubmissionIdsThatHaveLoggedErrors { get; } = null;
        #endregion
        #region IBuildEventSink Methods

        /// <summary>
        /// This method should not be used since we need the sinkID
        /// </summary>
        public void Consume(BuildEventArgs buildEvent)
        {
            ErrorUtilities.VerifyThrow(false, "Do not use this method for the transport sink");
        }

        /// <summary>
        /// Consumes the buildEventArg and creates a logMessagePacket
        /// </summary>
        /// <param name="buildEvent">Build event to package into a INodePacket</param>
        /// <param name="sinkId">The sink identifier.</param>
        /// <exception cref="InternalErrorException">buildEvent is null</exception>
        public void Consume(BuildEventArgs buildEvent, int sinkId)
        {
            ErrorUtilities.VerifyThrow(buildEvent != null, "buildEvent is null");
            if (buildEvent is BuildStartedEventArgs)
            {
                HaveLoggedBuildStartedEvent = true;
                return;
            }
            else if (buildEvent is BuildFinishedEventArgs)
            {
                HaveLoggedBuildFinishedEvent = true;
                return;
            }

            LogMessagePacket logPacket = new LogMessagePacket(new KeyValuePair<int, BuildEventArgs>(sinkId, buildEvent));
            _sendDataDelegate(logPacket);
        }

        /// <summary>
        /// Dispose of any resources the sink is holding onto.
        /// </summary>
        public void ShutDown()
        {
            _sendDataDelegate = null;
        }
        #endregion
    }
}
